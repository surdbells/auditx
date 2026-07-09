using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Notifications;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Reports.Dtos;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Audits;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace AuditX.Application.Reports.Commands;

/// <summary>
/// Distributes a completed report to a set of recipients (M8). Resolves directory-user emails + ad-hoc email
/// addresses, dedupes, and for each recipient records a <c>ReportDistribution</c> row + sends the email DIRECTLY
/// via <see cref="IEmailSender"/> (NOT the M10 rule engine — it cannot do ad-hoc addresses / per-recipient sends —
/// A2). NO SMS. Distribution lists are deferred (A1 — no entity exists). Audits <c>report_distributed</c> once.
/// </summary>
public sealed record DistributeReportCommand(
    Guid ReportId, IReadOnlyList<Guid> RecipientUserIds, IReadOnlyList<string> RecipientEmailAddresses,
    IReadOnlyList<string>? RecipientRoleNames = null)
    : ICommand<ReportDistributionResultDto>;

public sealed class DistributeReportCommandValidator : AbstractValidator<DistributeReportCommand>
{
    public DistributeReportCommandValidator()
    {
        RuleFor(x => x.ReportId).NotEmpty();
        RuleFor(x => x)
            .Must(x => (x.RecipientUserIds?.Count ?? 0) + (x.RecipientEmailAddresses?.Count ?? 0) + (x.RecipientRoleNames?.Count ?? 0) > 0)
            .WithMessage("At least one recipient is required.")
            .WithErrorCode("report.recipients_required");
        RuleForEach(x => x.RecipientEmailAddresses).EmailAddress().When(x => x.RecipientEmailAddresses is not null);
    }
}

public sealed class DistributeReportCommandHandler(
    IReportRepository reports, IAuditRepository audits, IExceptionRepository exceptions, IUserRepository users,
    IEmailSender emailSender, IPermissionResolver permissions, ICurrentUser currentUser,
    IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork, ILogger<DistributeReportCommandHandler> logger)
    : ICommandHandler<DistributeReportCommand, ReportDistributionResultDto>
{
    public async Task<ReportDistributionResultDto> Handle(DistributeReportCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();

        var report = await reports.GetByIdAsync(command.ReportId, cancellationToken) ?? throw new NotFoundException("Report", command.ReportId);

        Audit? auditEntity = null;
        if (report.AuditId is { } auditId)
        {
            auditEntity = await audits.GetByIdAsync(auditId, cancellationToken) ?? throw new NotFoundException("Audit", auditId);
            await ReportAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);
        }
        else
        {
            await ReportAccess.EnsureCanAccessStandaloneAsync(currentUser.UserId, permissions, cancellationToken, report.Kind);
        }

        if (report.Status != ReportStatus.Completed)
        {
            throw new DomainException("report.not_distributable", "Only a completed report can be distributed.");
        }

        // Resolve and dedupe recipients: directory users (by id → email) + ad-hoc external addresses.
        var resolvedUsers = await users.GetByIdsAsync(command.RecipientUserIds?.Distinct().ToArray() ?? [], cancellationToken);
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var recipients = new List<(Guid? UserId, string? AdHocEmail, string Email)>();

        foreach (var user in resolvedUsers)
        {
            var userEmail = user.Email?.Trim();
            if (!string.IsNullOrWhiteSpace(userEmail) && seenEmails.Add(userEmail))
            {
                recipients.Add((user.Id, null, userEmail));
            }
        }

        // Role targets: expand each role to its active members (reuses the M10 recipient resolver), deduped by email.
        foreach (var roleName in (command.RecipientRoleNames ?? []).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var member in await users.GetActiveByRoleNameAsync(roleName, cancellationToken))
            {
                var memberEmail = member.Email?.Trim();
                if (!string.IsNullOrWhiteSpace(memberEmail) && seenEmails.Add(memberEmail))
                {
                    recipients.Add((member.Id, null, memberEmail));
                }
            }
        }

        foreach (var raw in command.RecipientEmailAddresses ?? [])
        {
            var email = raw?.Trim();
            if (!string.IsNullOrWhiteSpace(email) && seenEmails.Add(email))
            {
                recipients.Add((null, email, email));
            }
        }

        if (recipients.Count == 0)
        {
            throw new DomainException("report.no_resolvable_recipients", "None of the supplied recipients could be resolved to an email address.");
        }

        // Counts for the email body + the report_distributed event payload (E1). Standalone (cross-audit) reports
        // have no single audit — the body names the report kind and carries no per-audit checklist/exception counts.
        string reportName;
        int totalCount;
        int exceptionCount;
        string severitySummary;
        string subject;
        string body;
        if (auditEntity is not null)
        {
            var exceptionEntities = await exceptions.ListByAuditAsync(auditEntity.Id, status: null, cancellationToken);
            exceptionCount = exceptionEntities.Count;
            totalCount = auditEntity.ChecklistItems.Count;
            severitySummary = BuildSeveritySummary(exceptionEntities.GroupBy(e => e.Severity).ToDictionary(g => g.Key, g => g.Count()));
            reportName = auditEntity.Name;
            subject = $"Audit report issued: {auditEntity.Name}";
            body =
                $"Audit report \"{auditEntity.Name}\" (version {report.VersionNumber}) has been issued.\n\n" +
                $"Conducted {auditEntity.StartDate:yyyy-MM-dd} to {auditEntity.ActualEndDate ?? auditEntity.TargetEndDate:yyyy-MM-dd}.\n" +
                $"{totalCount} checks performed. {exceptionCount} exception(s) raised ({severitySummary}).";
        }
        else
        {
            reportName = Generation.StandaloneReportModel.TitleFor(report.Kind);
            totalCount = 0;
            exceptionCount = 0;
            severitySummary = "no exceptions";
            subject = $"Report issued: {reportName}";
            body = $"The {reportName} report (version {report.VersionNumber}) has been issued.";
        }

        var recorded = 0;
        var failed = 0;
        foreach (var (userId, adHocEmail, email) in recipients)
        {
            // Send FIRST and only record a distribution row for a delivery that actually left the building, so the
            // distribution log never overstates delivery (the SMTP sender returns a result instead of throwing).
            ChannelSendResult result;
            try
            {
                result = await emailSender.SendAsync(email, subject, body, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Report {ReportId} distribution to a recipient threw.", report.Id);
                failed++;
                continue;
            }

            if (!result.Success)
            {
                logger.LogWarning("Report {ReportId} distribution to a recipient failed: {Error}", report.Id, result.Error);
                failed++;
                continue;
            }

            report.RecordDistribution(
                userId, adHocEmail, email, actorId, clock.UtcNow,
                reportName, totalCount, exceptionCount, severitySummary);

            // Commit per recipient so a later recipient's failure can never roll back / lose an already-sent row.
            await unitOfWork.SaveChangesAsync(cancellationToken);
            recorded++;
        }

        if (recorded > 0)
        {
            audit.Record(AuditEventTypes.ReportDistributed, AuditTargetTypes.Report, report.Id,
                after: new { report.VersionNumber, recipientCount = recorded, failedCount = failed });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ReportDistributionResultDto(recorded);
    }

    private static string BuildSeveritySummary(IReadOnlyDictionary<ExceptionSeverity, int> counts)
    {
        if (counts.Count == 0)
        {
            return "no exceptions";
        }

        var parts = new[] { ExceptionSeverity.Critical, ExceptionSeverity.High, ExceptionSeverity.Medium, ExceptionSeverity.Low }
            .Where(s => counts.TryGetValue(s, out var c) && c > 0)
            .Select(s => $"{counts[s]} {s.ToString().ToLowerInvariant()}");
        return string.Join(", ", parts);
    }
}
