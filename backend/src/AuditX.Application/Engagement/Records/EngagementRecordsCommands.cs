using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Audits;
using AuditX.Application.Common.Concurrency;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Execution;
using AuditX.Domain.Audits;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Authorization;
using AuditX.Domain.Common;
using AuditX.Domain.Engagement;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;
using AuditX.Domain.Reports;
using FluentValidation;

namespace AuditX.Application.Engagement.Records;

/// <summary>What every engagement-record handler needs to load the audit and answer with the caller's view of it.</summary>
public sealed class EngagementRecordsContext(
    IAuditRepository audits,
    IEngagementLetterRepository letters,
    IAuditMeetingRepository meetings,
    IReportRepository reports,
    IInstitutionSettingsRepository settings,
    IPermissionResolver permissions,
    ICurrentUser currentUser)
{
    public IEngagementLetterRepository Letters => letters;

    public IAuditMeetingRepository Meetings => meetings;

    public IReportRepository Reports => reports;

    public IPermissionResolver Permissions => permissions;

    public Guid UserId => currentUser.UserId ?? throw new UnauthorizedException();

    public async Task<Audit> LoadAuditAsync(Guid auditId, CancellationToken cancellationToken)
        => await audits.GetByIdAsync(auditId, cancellationToken) ?? throw new NotFoundException("Audit", auditId);

    public Task<EngagementRecordsDto> ViewAsync(Audit audit, CancellationToken cancellationToken)
        => EngagementRecordsBuilder.BuildAsync(audit, UserId, letters, meetings, reports, settings, permissions, cancellationToken);

    /// <summary>The letter and the meetings belong to an independent audit; a self-assessment has neither.</summary>
    public static void EnsureNotSelfAssessment(Audit audit)
    {
        if (audit.IsSelfAssessment)
        {
            throw new DomainException("engagement.not_applicable",
                "A self-assessment is carried out by the area itself, so it has no engagement letter or meetings to record.");
        }
    }

    public static void EnsureAuditIn(Audit audit, AuditStatus[] allowed, string code, string message)
    {
        if (!allowed.Contains(audit.Status))
        {
            throw new InvalidStateTransitionException(code, message);
        }
    }

    public static void EnsureVersion(byte[] version, string? expected, string code, string what)
    {
        if (!RowVersionToken.Matches(version, expected))
        {
            throw new ConflictException(code, $"The {what} was changed by someone else; reload and try again.");
        }
    }
}

// ---- Engagement letter -------------------------------------------------------------------------------------------

/// <summary>Write the engagement letter (the first draft), or change it. <c>LetterVersion</c> is required once a letter exists.</summary>
public sealed record SaveEngagementLetterCommand(Guid AuditId, string Body, string? LetterVersion) : ICommand<EngagementRecordsDto>;

public sealed class SaveEngagementLetterCommandValidator : AbstractValidator<SaveEngagementLetterCommand>
{
    public SaveEngagementLetterCommandValidator()
    {
        RuleFor(c => c.Body).NotEmpty().MaximumLength(EngagementLetter.MaxBodyLength);
    }
}

public sealed class SaveEngagementLetterCommandHandler(EngagementRecordsContext context, IAuditRecorder trail, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<SaveEngagementLetterCommand, EngagementRecordsDto>
{
    public async Task<EngagementRecordsDto> Handle(SaveEngagementLetterCommand command, CancellationToken cancellationToken)
    {
        var audit = await context.LoadAuditAsync(command.AuditId, cancellationToken);
        await AuditManageAuthorization.EnsureCanManageAsync(audit, context.UserId, context.Permissions, cancellationToken);
        EngagementRecordsContext.EnsureNotSelfAssessment(audit);
        EngagementRecordsContext.EnsureAuditIn(audit, EngagementRecordsBuilder.LetterOpen, "engagement_letter.locked",
            "The engagement letter is written before and during fieldwork; once the audit is under review it is settled.");

        var letter = await context.Letters.GetByAuditAsync(audit.Id, cancellationToken);
        if (letter is null)
        {
            letter = EngagementLetter.Draft(audit.Id, command.Body, context.UserId, clock.UtcNow);
            context.Letters.Add(letter);
        }
        else
        {
            EngagementRecordsContext.EnsureVersion(letter.Version, command.LetterVersion, "engagement_letter.concurrency_conflict", "engagement letter");
            letter.Revise(command.Body, context.UserId, clock.UtcNow);
        }

        trail.Record(AuditEventTypes.EngagementLetterSaved, AuditTargetTypes.Audit, audit.Id,
            after: new { letterId = letter.Id, letter.Revision, status = letter.Status.ToSnake() });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await context.ViewAsync(audit, cancellationToken);
    }
}

public sealed record IssueEngagementLetterCommand(Guid AuditId, string LetterVersion) : ICommand<EngagementRecordsDto>;

public sealed class IssueEngagementLetterCommandValidator : AbstractValidator<IssueEngagementLetterCommand>
{
    public IssueEngagementLetterCommandValidator() => RuleFor(c => c.LetterVersion).NotEmpty();
}

public sealed class IssueEngagementLetterCommandHandler(EngagementRecordsContext context, IAuditRecorder trail, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<IssueEngagementLetterCommand, EngagementRecordsDto>
{
    public async Task<EngagementRecordsDto> Handle(IssueEngagementLetterCommand command, CancellationToken cancellationToken)
    {
        var audit = await context.LoadAuditAsync(command.AuditId, cancellationToken);
        await AuditManageAuthorization.EnsureCanManageAsync(audit, context.UserId, context.Permissions, cancellationToken);
        EngagementRecordsContext.EnsureNotSelfAssessment(audit);
        EngagementRecordsContext.EnsureAuditIn(audit, EngagementRecordsBuilder.LetterOpen, "engagement_letter.locked",
            "The engagement letter is issued before or during fieldwork.");

        var letter = await context.Letters.GetByAuditAsync(audit.Id, cancellationToken)
            ?? throw new DomainException("engagement_letter.missing", "Write the engagement letter before issuing it.");
        EngagementRecordsContext.EnsureVersion(letter.Version, command.LetterVersion, "engagement_letter.concurrency_conflict", "engagement letter");
        letter.Issue(context.UserId, clock.UtcNow, audit.Name, audit.AuditeeUserId);

        trail.Record(AuditEventTypes.EngagementLetterIssued, AuditTargetTypes.Audit, audit.Id,
            after: new { letterId = letter.Id, letter.Revision, issuedTo = audit.AuditeeUserId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await context.ViewAsync(audit, cancellationToken);
    }
}

/// <summary>
/// The auditee acknowledges the issued letter. Addressed by the letter, not the audit, because acting on the audit
/// itself is closed to its auditee; only the audit's auditee may acknowledge.
/// </summary>
public sealed record AcknowledgeEngagementLetterCommand(Guid LetterId, string? Comment, string LetterVersion) : ICommand<EngagementRecordsDto>;

public sealed class AcknowledgeEngagementLetterCommandValidator : AbstractValidator<AcknowledgeEngagementLetterCommand>
{
    public AcknowledgeEngagementLetterCommandValidator()
    {
        RuleFor(c => c.LetterVersion).NotEmpty();
        RuleFor(c => c.Comment).MaximumLength(EngagementLetter.MaxCommentLength);
    }
}

public sealed class AcknowledgeEngagementLetterCommandHandler(EngagementRecordsContext context, IAuditRecorder trail, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<AcknowledgeEngagementLetterCommand, EngagementRecordsDto>
{
    public async Task<EngagementRecordsDto> Handle(AcknowledgeEngagementLetterCommand command, CancellationToken cancellationToken)
    {
        var letter = await context.Letters.GetByIdAsync(command.LetterId, cancellationToken)
            ?? throw new NotFoundException("EngagementLetter", command.LetterId);
        var audit = await context.LoadAuditAsync(letter.AuditId, cancellationToken);
        if (!audit.IsAuditee(context.UserId))
        {
            throw new ForbiddenAccessException("Only the audit's auditee acknowledges its engagement letter.");
        }

        if (audit.Status == AuditStatus.Cancelled)
        {
            throw new InvalidStateTransitionException("engagement_letter.locked", "The audit was cancelled.");
        }

        EngagementRecordsContext.EnsureVersion(letter.Version, command.LetterVersion, "engagement_letter.concurrency_conflict", "engagement letter");
        letter.Acknowledge(context.UserId, command.Comment, clock.UtcNow, audit.Name);

        trail.Record(AuditEventTypes.EngagementLetterAcknowledged, AuditTargetTypes.Audit, audit.Id,
            after: new { letterId = letter.Id, letter.Revision, hasComment = letter.AcknowledgementComment is not null });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await context.ViewAsync(audit, cancellationToken);
    }
}

// ---- Independence ------------------------------------------------------------------------------------------------

/// <summary>The signed-in team member declares their independence, or discloses a conflict of interest.</summary>
public sealed record DeclareIndependenceCommand(Guid AuditId, bool HasConflict, string? ConflictDetails, string Version) : ICommand<EngagementRecordsDto>;

public sealed class DeclareIndependenceCommandValidator : AbstractValidator<DeclareIndependenceCommand>
{
    public DeclareIndependenceCommandValidator()
    {
        RuleFor(c => c.Version).NotEmpty();
        RuleFor(c => c.ConflictDetails).NotEmpty().When(c => c.HasConflict)
            .WithMessage("Describe the conflict of interest so a manager can decide on it.");
        RuleFor(c => c.ConflictDetails).MaximumLength(IndependenceDeclaration.MaxLength);
    }
}

public sealed class DeclareIndependenceCommandHandler(EngagementRecordsContext context, IAuditRecorder trail, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<DeclareIndependenceCommand, EngagementRecordsDto>
{
    public async Task<EngagementRecordsDto> Handle(DeclareIndependenceCommand command, CancellationToken cancellationToken)
    {
        var audit = await context.LoadAuditAsync(command.AuditId, cancellationToken);
        audit.EnsureVersion(command.Version);
        var declaration = audit.DeclareIndependence(context.UserId, command.HasConflict, command.ConflictDetails, clock.UtcNow);

        // The trail records that a conflict was disclosed, not what it is: the details stay with the declaration.
        trail.Record(AuditEventTypes.IndependenceDeclared, AuditTargetTypes.Audit, audit.Id,
            after: new { declarationId = declaration.Id, userId = context.UserId, declaration.HasConflict });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await context.ViewAsync(audit, cancellationToken);
    }
}

/// <summary>A manager of the audit accepts a disclosed conflict and records the safeguards.</summary>
public sealed record AcceptIndependenceConflictCommand(Guid AuditId, Guid DeclarationId, string Safeguards, string Version) : ICommand<EngagementRecordsDto>;

public sealed class AcceptIndependenceConflictCommandValidator : AbstractValidator<AcceptIndependenceConflictCommand>
{
    public AcceptIndependenceConflictCommandValidator()
    {
        RuleFor(c => c.Version).NotEmpty();
        RuleFor(c => c.Safeguards).NotEmpty().MaximumLength(IndependenceDeclaration.MaxLength);
    }
}

public sealed class AcceptIndependenceConflictCommandHandler(EngagementRecordsContext context, IAuditRecorder trail, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<AcceptIndependenceConflictCommand, EngagementRecordsDto>
{
    public async Task<EngagementRecordsDto> Handle(AcceptIndependenceConflictCommand command, CancellationToken cancellationToken)
    {
        var audit = await context.LoadAuditAsync(command.AuditId, cancellationToken);
        AuditIndependence.EnsureNotAuditee(audit, context.UserId);
        if (!await context.Permissions.HasPermissionAsync(context.UserId, PermissionKeys.ManageAudit, audit.Id.ToString(), cancellationToken))
        {
            throw new ForbiddenAccessException("Only a manager of this audit decides on a disclosed conflict of interest.");
        }

        audit.EnsureVersion(command.Version);
        var declaration = audit.AcceptIndependenceConflict(command.DeclarationId, context.UserId, command.Safeguards, clock.UtcNow);

        trail.Record(AuditEventTypes.IndependenceConflictAccepted, AuditTargetTypes.Audit, audit.Id,
            after: new { declarationId = declaration.Id, declarantUserId = declaration.UserId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await context.ViewAsync(audit, cancellationToken);
    }
}

// ---- Meetings ----------------------------------------------------------------------------------------------------

/// <summary>
/// Record (or correct) the minutes of the kickoff or the exit meeting. <c>MeetingVersion</c> is required when the
/// record exists. The exit meeting names the completed draft report it discussed.
/// </summary>
public sealed record RecordAuditMeetingCommand(
    Guid AuditId,
    string Kind,
    DateTimeOffset HeldAt,
    IReadOnlyList<Guid> AttendeeUserIds,
    IReadOnlyList<string> ExternalAttendees,
    string Minutes,
    IReadOnlyList<string> AgreedPoints,
    Guid? ReportId,
    string? MeetingVersion) : ICommand<EngagementRecordsDto>;

public sealed class RecordAuditMeetingCommandValidator : AbstractValidator<RecordAuditMeetingCommand>
{
    public RecordAuditMeetingCommandValidator()
    {
        RuleFor(c => c.Kind).Must(k => k is "kickoff" or "exit").WithMessage("A meeting is the kickoff or the exit meeting.");
        RuleFor(c => c.Minutes).NotEmpty().MaximumLength(AuditMeeting.MaxMinutesLength);
        RuleFor(c => c.AttendeeUserIds).NotNull();
        RuleFor(c => c.ExternalAttendees).NotNull();
        RuleFor(c => c.AgreedPoints).NotNull();
        RuleFor(c => c).Must(c => (c.AttendeeUserIds?.Count ?? 0) + (c.ExternalAttendees?.Count(a => !string.IsNullOrWhiteSpace(a)) ?? 0) > 0)
            .WithName("attendees").WithMessage("Record who attended the meeting.");
        RuleFor(c => c.AttendeeUserIds.Count + c.ExternalAttendees.Count).LessThanOrEqualTo(AuditMeeting.MaxAttendees)
            .When(c => c.AttendeeUserIds is not null && c.ExternalAttendees is not null).WithName("attendees");
        RuleForEach(c => c.ExternalAttendees).MaximumLength(AuditMeeting.MaxExternalAttendeeLength);
        RuleFor(c => c.AgreedPoints.Count).LessThanOrEqualTo(AuditMeeting.MaxPoints).When(c => c.AgreedPoints is not null).WithName("agreedPoints");
        RuleForEach(c => c.AgreedPoints).MaximumLength(AuditMeeting.MaxPointLength);
    }
}

public sealed class RecordAuditMeetingCommandHandler(
    EngagementRecordsContext context, IUserRepository users, IAuditRecorder trail, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RecordAuditMeetingCommand, EngagementRecordsDto>
{
    private static readonly AuditStatus[] KickoffOpen = [AuditStatus.Planned, AuditStatus.InProgress, AuditStatus.UnderReview, AuditStatus.Completed];
    private static readonly AuditStatus[] ExitOpen = [AuditStatus.UnderReview, AuditStatus.Completed];

    public async Task<EngagementRecordsDto> Handle(RecordAuditMeetingCommand command, CancellationToken cancellationToken)
    {
        var audit = await context.LoadAuditAsync(command.AuditId, cancellationToken);
        await AuditManageAuthorization.EnsureCanManageAsync(audit, context.UserId, context.Permissions, cancellationToken);
        EngagementRecordsContext.EnsureNotSelfAssessment(audit);

        var kind = command.Kind == "exit" ? MeetingKind.Exit : MeetingKind.Kickoff;
        if (kind == MeetingKind.Kickoff)
        {
            EngagementRecordsContext.EnsureAuditIn(audit, KickoffOpen, "meeting.not_yet",
                "The kickoff meeting is recorded once the audit is planned.");
        }
        else
        {
            EngagementRecordsContext.EnsureAuditIn(audit, ExitOpen, "meeting.not_yet",
                "The exit meeting is held on the draft report, once the audit is under review.");
            await EnsureDraftReportAsync(audit.Id, command.ReportId, cancellationToken);
        }

        await EnsureAttendeesAsync(command.AttendeeUserIds, cancellationToken);

        var existing = (await context.Meetings.ListByAuditAsync(audit.Id, cancellationToken)).FirstOrDefault(m => m.Kind == kind);
        if (existing is null)
        {
            existing = AuditMeeting.Record(
                audit.Id, kind, command.HeldAt, command.AttendeeUserIds, command.ExternalAttendees, command.Minutes,
                command.AgreedPoints, command.ReportId, context.UserId, clock.UtcNow, audit.Name, audit.AuditeeUserId);
            context.Meetings.Add(existing);
        }
        else
        {
            EngagementRecordsContext.EnsureVersion(existing.Version, command.MeetingVersion, "meeting.concurrency_conflict", "meeting record");
            existing.Correct(
                command.HeldAt, command.AttendeeUserIds, command.ExternalAttendees, command.Minutes, command.AgreedPoints,
                command.ReportId, context.UserId, clock.UtcNow, audit.Name, audit.AuditeeUserId);
        }

        trail.Record(AuditEventTypes.AuditMeetingRecorded, AuditTargetTypes.Audit, audit.Id,
            after: new
            {
                meetingId = existing.Id,
                kind = kind.ToSnake(),
                existing.HeldAt,
                attendees = existing.AttendeeUserIds.Count + existing.ExternalAttendees.Count,
                agreedPoints = existing.AgreedPoints.Count,
                existing.ReportId,
                isCorrection = existing.LastEditedAt is not null,
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await context.ViewAsync(audit, cancellationToken);
    }

    private async Task EnsureDraftReportAsync(Guid auditId, Guid? reportId, CancellationToken cancellationToken)
    {
        if (reportId is not { } id)
        {
            throw new DomainException("meeting.draft_report_required", "Choose the draft report the exit meeting discussed.");
        }

        var report = await context.Reports.GetByIdAsync(id, cancellationToken);
        if (report is null || report.AuditId != auditId || report.IsDeleted
            || report.Stage != ReportStage.Draft || report.Status != ReportStatus.Completed)
        {
            throw new DomainException("meeting.report_not_draft", "The exit meeting must name a completed draft report of this audit.");
        }
    }

    private async Task EnsureAttendeesAsync(IReadOnlyList<Guid> attendeeUserIds, CancellationToken cancellationToken)
    {
        var ids = attendeeUserIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var found = await users.GetByIdsAsync(ids, cancellationToken);
        if (found.Count != ids.Length || found.Any(u => u.Status == UserStatus.Deactivated))
        {
            throw new DomainException("meeting.attendee_unknown", "Every attendee chosen from the directory must be an active user.");
        }
    }
}
