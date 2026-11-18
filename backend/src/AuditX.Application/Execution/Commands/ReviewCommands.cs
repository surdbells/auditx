using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Audits;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Execution.Dtos;
using AuditX.Application.Execution.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Audits;
using FluentValidation;

namespace AuditX.Application.Execution.Commands;

/// <summary>
/// Working-paper review. The audit's lead and its reviewers sign answered checklist items off and raise review notes;
/// the team answers the notes and a reviewer clears them. Nobody signs off work they answered themselves, and the audit
/// cannot complete until every item is signed off and every note cleared. Who may do what is decided by the audit's
/// own team (the domain enforces it); the audit's auditee is refused upstream by the independence filter. Every change
/// is written to the audit trail against the audit, so it shows in the audit's history.
/// </summary>
internal static class ReviewCommandSupport
{
    public static async Task<(Audit Audit, Guid UserId)> LoadAsync(
        Guid auditId, string version, IAuditRepository audits, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var entity = await audits.GetByIdAsync(auditId, cancellationToken) ?? throw new NotFoundException("Audit", auditId);
        entity.EnsureVersion(version);
        return (entity, userId);
    }
}

public sealed record SignOffItemCommand(Guid AuditId, Guid ItemId, string Version) : ICommand<WorkingPaperReviewDto>;

public sealed class SignOffItemCommandValidator : AbstractValidator<SignOffItemCommand>
{
    public SignOffItemCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class SignOffItemCommandHandler(
    IAuditRepository audits, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<SignOffItemCommand, WorkingPaperReviewDto>
{
    public async Task<WorkingPaperReviewDto> Handle(SignOffItemCommand command, CancellationToken cancellationToken)
    {
        var (entity, userId) = await ReviewCommandSupport.LoadAsync(command.AuditId, command.Version, audits, currentUser, cancellationToken);
        entity.SignOffItem(command.ItemId, userId, clock.UtcNow);
        audit.Record(AuditEventTypes.ItemSignedOff, AuditTargetTypes.Audit, entity.Id, after: new { itemId = command.ItemId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToReviewDto(userId);
    }
}

public sealed record RaiseReviewNoteCommand(Guid AuditId, Guid? ItemId, string Body, string Version) : ICommand<WorkingPaperReviewDto>;

public sealed class RaiseReviewNoteCommandValidator : AbstractValidator<RaiseReviewNoteCommand>
{
    public RaiseReviewNoteCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEqual(Guid.Empty).When(x => x.ItemId is not null);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(ReviewNote.MaxLength);
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class RaiseReviewNoteCommandHandler(
    IAuditRepository audits, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RaiseReviewNoteCommand, WorkingPaperReviewDto>
{
    public async Task<WorkingPaperReviewDto> Handle(RaiseReviewNoteCommand command, CancellationToken cancellationToken)
    {
        var (entity, userId) = await ReviewCommandSupport.LoadAsync(command.AuditId, command.Version, audits, currentUser, cancellationToken);
        var note = entity.RaiseReviewNote(command.ItemId, command.Body, userId, clock.UtcNow);
        audit.Record(AuditEventTypes.ReviewNoteRaised, AuditTargetTypes.Audit, entity.Id,
            after: new { noteId = note.Id, itemId = note.ChecklistItemId, body = note.Body });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToReviewDto(userId);
    }
}

public sealed record AnswerReviewNoteCommand(Guid AuditId, Guid NoteId, string Answer, string Version) : ICommand<WorkingPaperReviewDto>;

public sealed class AnswerReviewNoteCommandValidator : AbstractValidator<AnswerReviewNoteCommand>
{
    public AnswerReviewNoteCommandValidator()
    {
        RuleFor(x => x.NoteId).NotEmpty();
        RuleFor(x => x.Answer).NotEmpty().MaximumLength(ReviewNote.MaxLength);
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class AnswerReviewNoteCommandHandler(
    IAuditRepository audits, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<AnswerReviewNoteCommand, WorkingPaperReviewDto>
{
    public async Task<WorkingPaperReviewDto> Handle(AnswerReviewNoteCommand command, CancellationToken cancellationToken)
    {
        var (entity, userId) = await ReviewCommandSupport.LoadAsync(command.AuditId, command.Version, audits, currentUser, cancellationToken);
        var note = entity.AnswerReviewNote(command.NoteId, command.Answer, userId, clock.UtcNow);
        audit.Record(AuditEventTypes.ReviewNoteAnswered, AuditTargetTypes.Audit, entity.Id,
            after: new { noteId = note.Id, itemId = note.ChecklistItemId, answer = note.Answer });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToReviewDto(userId);
    }
}

public sealed record ClearReviewNoteCommand(Guid AuditId, Guid NoteId, string Version) : ICommand<WorkingPaperReviewDto>;

public sealed class ClearReviewNoteCommandValidator : AbstractValidator<ClearReviewNoteCommand>
{
    public ClearReviewNoteCommandValidator()
    {
        RuleFor(x => x.NoteId).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class ClearReviewNoteCommandHandler(
    IAuditRepository audits, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ClearReviewNoteCommand, WorkingPaperReviewDto>
{
    public async Task<WorkingPaperReviewDto> Handle(ClearReviewNoteCommand command, CancellationToken cancellationToken)
    {
        var (entity, userId) = await ReviewCommandSupport.LoadAsync(command.AuditId, command.Version, audits, currentUser, cancellationToken);
        var note = entity.ClearReviewNote(command.NoteId, userId, clock.UtcNow);
        audit.Record(AuditEventTypes.ReviewNoteCleared, AuditTargetTypes.Audit, entity.Id,
            after: new { noteId = note.Id, itemId = note.ChecklistItemId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToReviewDto(userId);
    }
}

public sealed record ReopenReviewNoteCommand(Guid AuditId, Guid NoteId, string Version) : ICommand<WorkingPaperReviewDto>;

public sealed class ReopenReviewNoteCommandValidator : AbstractValidator<ReopenReviewNoteCommand>
{
    public ReopenReviewNoteCommandValidator()
    {
        RuleFor(x => x.NoteId).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class ReopenReviewNoteCommandHandler(
    IAuditRepository audits, ICurrentUser currentUser, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<ReopenReviewNoteCommand, WorkingPaperReviewDto>
{
    public async Task<WorkingPaperReviewDto> Handle(ReopenReviewNoteCommand command, CancellationToken cancellationToken)
    {
        var (entity, userId) = await ReviewCommandSupport.LoadAsync(command.AuditId, command.Version, audits, currentUser, cancellationToken);
        var note = entity.ReopenReviewNote(command.NoteId, userId);
        audit.Record(AuditEventTypes.ReviewNoteReopened, AuditTargetTypes.Audit, entity.Id,
            after: new { noteId = note.Id, itemId = note.ChecklistItemId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToReviewDto(userId);
    }
}
