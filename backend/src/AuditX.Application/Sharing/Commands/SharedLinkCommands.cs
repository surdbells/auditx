using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Reports;
using AuditX.Application.Sharing.Dtos;
using AuditX.Application.Sharing.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Sharing;
using FluentValidation;

namespace AuditX.Application.Sharing.Commands;

// ---- Create a shareable link to a report ----

public sealed record CreateReportShareLinkCommand(Guid ReportId, int? ExpiresInDays) : ICommand<SharedLinkDto>;

public sealed class CreateReportShareLinkCommandValidator : AbstractValidator<CreateReportShareLinkCommand>
{
    public CreateReportShareLinkCommandValidator()
    {
        RuleFor(x => x.ReportId).NotEmpty();
        RuleFor(x => x.ExpiresInDays).InclusiveBetween(1, 365).When(x => x.ExpiresInDays.HasValue);
    }
}

public sealed class CreateReportShareLinkCommandHandler(
    IReportRepository reports, IAuditRepository audits, ISharedLinkRepository links, IPermissionResolver permissions,
    ICurrentUser currentUser, ISlugGenerator slugs, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateReportShareLinkCommand, SharedLinkDto>
{
    public async Task<SharedLinkDto> Handle(CreateReportShareLinkCommand command, CancellationToken cancellationToken)
    {
        var report = await reports.GetByIdAsync(command.ReportId, cancellationToken) ?? throw new NotFoundException("Report", command.ReportId);
        // The creator must themselves be able to view the report — a link never grants access the creator lacks.
        await ReportAccess.EnsureCanReadAsync(report, audits, currentUser.UserId, permissions, cancellationToken);
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        var expiresAt = command.ExpiresInDays is { } days ? clock.UtcNow.AddDays(days) : (DateTimeOffset?)null;
        var link = SharedLink.Create(slugs.NewSlug(), SharedLinkTargetType.Report, report.Id, userId, expiresAt);
        links.Add(link);
        audit.Record(AuditEventTypes.ReportShareLinkCreated, AuditTargetTypes.SharedLink, link.Id,
            after: new { link.Slug, targetType = "report", link.TargetId, link.ExpiresAt });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return link.ToDto(clock.UtcNow);
    }
}

// ---- Revoke a shareable link (creator only) ----

public sealed record RevokeSharedLinkCommand(Guid Id, string Version) : ICommand<Unit>;

public sealed class RevokeSharedLinkCommandHandler(
    ISharedLinkRepository links, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RevokeSharedLinkCommand, Unit>
{
    public async Task<Unit> Handle(RevokeSharedLinkCommand command, CancellationToken cancellationToken)
    {
        var link = await links.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Shared link", command.Id);
        if (currentUser.UserId is not { } uid || link.CreatedByUserId != uid)
        {
            throw new ForbiddenAccessException("Only the link's creator can revoke it.");
        }

        if (!string.Equals(RowVersionToken.Encode(link.Version), command.Version, StringComparison.Ordinal))
        {
            throw new ConflictException("shared_link.concurrency_conflict", "The shared link was modified elsewhere; reload and retry.");
        }

        link.Revoke(uid, clock.UtcNow);
        audit.Record(AuditEventTypes.ReportShareLinkRevoked, AuditTargetTypes.SharedLink, link.Id, before: new { link.Slug });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
