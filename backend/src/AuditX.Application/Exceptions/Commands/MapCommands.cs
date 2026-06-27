using System.Security.Cryptography;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.MakerChecker;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Abstractions.Storage;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Json;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Exceptions.Dtos;
using AuditX.Application.Exceptions.Mapping;
using AuditX.Application.Execution.Dtos;
using AuditX.Application.Execution.Mapping;
using AuditX.Application.Identity.MakerChecker;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Evidence;
using AuditX.Domain.Identity;
using FluentValidation;

namespace AuditX.Application.Exceptions.Commands;

public sealed record MapActionInput(string Description, Guid OwnerUserId, DateOnly TargetDate, string? ExpectedEvidenceType);

public sealed record SubmitMapCommand(Guid ExceptionId, IReadOnlyList<MapActionInput> Actions, string Version) : ICommand<ExceptionDto>;

public sealed class SubmitMapCommandValidator : AbstractValidator<SubmitMapCommand>
{
    public SubmitMapCommandValidator()
    {
        RuleFor(x => x.Actions).NotEmpty();
        RuleForEach(x => x.Actions).ChildRules(a => a.RuleFor(i => i.Description).NotEmpty());
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class SubmitMapCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<SubmitMapCommand, ExceptionDto>
{
    public async Task<ExceptionDto> Handle(SubmitMapCommand command, CancellationToken cancellationToken)
    {
        var (exception, _) = await MapHandlerSupport.LoadScopedAsync(exceptions, audits, permissions, currentUser, command.ExceptionId, cancellationToken);
        exception.EnsureVersion(command.Version);

        // On a resubmission after rejection, replace the prior MAP rather than appending to it.
        if (exception.Status == ExceptionStatus.MapRejected)
        {
            exception.ClearMapActions();
        }

        foreach (var input in command.Actions)
        {
            exception.AddMapAction(input.Description, input.OwnerUserId, input.TargetDate, input.ExpectedEvidenceType);
        }

        exception.SubmitMap(currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        audit.Record(AuditEventTypes.MapSubmitted, AuditTargetTypes.Exception, exception.Id, payload: new { actions = command.Actions.Count });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return exception.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }
}

/// <summary>The serialized intent captured when a MAP approval is held by a maker-checker gate.</summary>
public sealed record MapApprovalPayload(Guid ExceptionId);

public sealed record ApproveMapCommand(Guid ExceptionId, string Version) : ICommand<ExceptionActionResult>;

public sealed class ApproveMapCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IPermissionResolver permissions, MakerCheckerGateService gateService, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ApproveMapCommand, ExceptionActionResult>
{
    public async Task<ExceptionActionResult> Handle(ApproveMapCommand command, CancellationToken cancellationToken)
    {
        var (exception, _) = await MapHandlerSupport.LoadScopedAsync(exceptions, audits, permissions, currentUser, command.ExceptionId, cancellationToken);
        exception.EnsureVersion(command.Version);

        if (exception.Status != ExceptionStatus.MapSubmitted)
        {
            throw new InvalidStateTransitionException("exception.map_not_submitted", "Only a submitted MAP can be approved.");
        }

        var payload = AppJson.Serialize(new MapApprovalPayload(exception.Id));
        var pendingId = await gateService.TryCaptureAsync(MakerCheckerActionTypes.MapApproval, AuditTargetTypes.Exception, exception.Id, payload, cancellationToken);
        if (pendingId is { } id)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new ExceptionActionResult(null, id);
        }

        exception.ApproveMap(currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        audit.Record(AuditEventTypes.MapApproved, AuditTargetTypes.Exception, exception.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ExceptionActionResult(exception.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)), null);
    }
}

/// <summary>Replays an approved <c>map_approval</c> action, attributing it to the maker (US-M6-010).</summary>
public sealed class MapApprovalExecutor(IExceptionRepository exceptions, IAuditRecorder audit, IClock clock) : IPendingActionExecutor
{
    public string ActionType => MakerCheckerActionTypes.MapApproval;

    public async Task ExecuteAsync(string pendingPayloadJson, Guid makerUserId, CancellationToken cancellationToken)
    {
        var payload = AppJson.Deserialize<MapApprovalPayload>(pendingPayloadJson);
        var exception = await exceptions.GetByIdAsync(payload.ExceptionId, cancellationToken) ?? throw new NotFoundException("Exception", payload.ExceptionId);
        exception.ApproveMap(makerUserId, clock.UtcNow);
        audit.RecordAs(ActorType.User, actorSystemLabel: null, actorUserId: makerUserId,
            AuditEventTypes.MapApproved, AuditTargetTypes.Exception, exception.Id);
    }
}

public sealed record RejectMapCommand(Guid ExceptionId, string Reason, string Version) : ICommand<ExceptionDto>;

public sealed class RejectMapCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RejectMapCommand, ExceptionDto>
{
    public async Task<ExceptionDto> Handle(RejectMapCommand command, CancellationToken cancellationToken)
    {
        var (exception, _) = await MapHandlerSupport.LoadScopedAsync(exceptions, audits, permissions, currentUser, command.ExceptionId, cancellationToken);
        exception.EnsureVersion(command.Version);
        exception.RejectMap(command.Reason, currentUser.UserId ?? Guid.Empty);
        audit.Record(AuditEventTypes.MapRejected, AuditTargetTypes.Exception, exception.Id, payload: new { command.Reason });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return exception.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }
}

public sealed record MarkMapActionCompleteCommand(Guid ExceptionId, Guid ActionId, string Version) : ICommand<ExceptionDto>;

public sealed class MarkMapActionCompleteCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IEvidenceRepository evidence, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<MarkMapActionCompleteCommand, ExceptionDto>
{
    public async Task<ExceptionDto> Handle(MarkMapActionCompleteCommand command, CancellationToken cancellationToken)
    {
        var (exception, _) = await MapHandlerSupport.LoadScopedAsync(exceptions, audits, permissions, currentUser, command.ExceptionId, cancellationToken);
        exception.EnsureVersion(command.Version);

        var files = await evidence.ListForContextAsync(exception.AuditId, EvidenceContextType.MapAction, command.ActionId, cancellationToken);
        exception.MarkMapActionComplete(command.ActionId, requireEvidence: true, hasEvidence: files.Count > 0, completedBy: currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        audit.Record(AuditEventTypes.MapActionCompleted, AuditTargetTypes.MapAction, command.ActionId, payload: new { command.ExceptionId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return exception.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }
}

public sealed record MarkMapCompleteCommand(Guid ExceptionId, string Version) : ICommand<ExceptionDto>;

public sealed class MarkMapCompleteCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<MarkMapCompleteCommand, ExceptionDto>
{
    public async Task<ExceptionDto> Handle(MarkMapCompleteCommand command, CancellationToken cancellationToken)
    {
        var (exception, _) = await MapHandlerSupport.LoadScopedAsync(exceptions, audits, permissions, currentUser, command.ExceptionId, cancellationToken);
        exception.EnsureVersion(command.Version);
        exception.MarkMapComplete(currentUser.UserId ?? Guid.Empty);
        audit.Record(AuditEventTypes.MapCompleted, AuditTargetTypes.Exception, exception.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return exception.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }
}

public sealed record ReturnForEvidenceCommand(Guid ExceptionId, string Reason, string Version) : ICommand<ExceptionDto>;

public sealed class ReturnForEvidenceCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ReturnForEvidenceCommand, ExceptionDto>
{
    public async Task<ExceptionDto> Handle(ReturnForEvidenceCommand command, CancellationToken cancellationToken)
    {
        var (exception, _) = await MapHandlerSupport.LoadScopedAsync(exceptions, audits, permissions, currentUser, command.ExceptionId, cancellationToken);
        exception.EnsureVersion(command.Version);
        exception.ReturnForEvidence(command.Reason, currentUser.UserId ?? Guid.Empty);
        audit.Record(AuditEventTypes.MapReturnedForEvidence, AuditTargetTypes.Exception, exception.Id, payload: new { command.Reason });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return exception.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }
}

public sealed record CloseExceptionCommand(Guid ExceptionId, string? ClosureNote, string Version) : ICommand<ExceptionDto>;

public sealed class CloseExceptionCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IEvidenceRepository evidence, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<CloseExceptionCommand, ExceptionDto>
{
    public async Task<ExceptionDto> Handle(CloseExceptionCommand command, CancellationToken cancellationToken)
    {
        var (exception, _) = await MapHandlerSupport.LoadScopedAsync(exceptions, audits, permissions, currentUser, command.ExceptionId, cancellationToken);
        exception.EnsureVersion(command.Version);
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        // Segregation of duties (FR-M6-006 / US-M6-014): the closer cannot be the raiser or the owner.
        if (userId == exception.RaisedByUserId || userId == exception.OwnerUserId)
        {
            throw new ForbiddenAccessException("Segregation of duties: the raiser or owner cannot close the exception.");
        }

        if (!await MapHandlerSupport.HasAnyMapEvidenceAsync(exception, evidence, cancellationToken))
        {
            throw new DomainException("exception.evidence_required", "At least one remediation evidence file is required before closure.");
        }

        exception.Close(command.ClosureNote, userId, clock.UtcNow);
        audit.Record(exception.CiaPending ? AuditEventTypes.ExceptionPendingCia : AuditEventTypes.ExceptionClosed,
            AuditTargetTypes.Exception, exception.Id, payload: new { exception.CiaPending });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return exception.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }
}

public sealed record CiaCountersignCommand(Guid ExceptionId, string Version) : ICommand<ExceptionDto>;

public sealed class CiaCountersignCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<CiaCountersignCommand, ExceptionDto>
{
    public async Task<ExceptionDto> Handle(CiaCountersignCommand command, CancellationToken cancellationToken)
    {
        var (exception, _) = await MapHandlerSupport.LoadScopedAsync(exceptions, audits, permissions, currentUser, command.ExceptionId, cancellationToken);
        exception.EnsureVersion(command.Version);
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        // CIA is the independent second-line control: the countersigner must differ from the closer too,
        // not just the raiser/owner (otherwise one person could close AND countersign a Critical exception).
        if (userId == exception.RaisedByUserId || userId == exception.OwnerUserId || userId == exception.ClosedBy)
        {
            throw new ForbiddenAccessException("Segregation of duties: the closer, raiser, or owner cannot countersign.");
        }

        exception.CiaCountersign(userId, clock.UtcNow);
        audit.Record(AuditEventTypes.ExceptionClosed, AuditTargetTypes.Exception, exception.Id, payload: new { ciaCountersignedBy = userId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return exception.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }
}

public sealed record UploadMapActionEvidenceCommand(Guid ExceptionId, Guid ActionId, byte[] Content, string Filename, string ContentType) : ICommand<EvidenceFileDto>;

public sealed class UploadMapActionEvidenceCommandHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IEvidenceRepository evidence, IFileStorage storage, IFileSignatureInspector inspector,
    IBankSettingsRepository bankSettings, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<UploadMapActionEvidenceCommand, EvidenceFileDto>
{
    public async Task<EvidenceFileDto> Handle(UploadMapActionEvidenceCommand command, CancellationToken cancellationToken)
    {
        var (exception, _) = await MapHandlerSupport.LoadScopedAsync(exceptions, audits, permissions, currentUser, command.ExceptionId, cancellationToken);
        var uploaderId = currentUser.UserId ?? throw new UnauthorizedException();
        if (exception.MapActions.All(a => a.Id != command.ActionId))
        {
            throw new NotFoundException("Remediation action", command.ActionId);
        }

        if (command.Content.LongLength == 0)
        {
            throw new DomainException("evidence.empty_file", "The uploaded file is empty.");
        }

        var settings = await bankSettings.GetAsync(cancellationToken);
        if (command.Content.LongLength > settings.MaxEvidenceFileMb * 1024L * 1024L)
        {
            throw new PayloadTooLargeException("evidence.file_too_large", $"The file exceeds the {settings.MaxEvidenceFileMb} MB per-file limit.");
        }

        var existing = await evidence.SumSizeForAuditAsync(exception.AuditId, cancellationToken);
        if (existing + command.Content.LongLength > settings.MaxAuditEvidenceGb * 1024L * 1024L * 1024L)
        {
            throw new PayloadTooLargeException("evidence.audit_quota_exceeded", $"This audit's evidence would exceed the {settings.MaxAuditEvidenceGb} GB limit.");
        }

        if (!inspector.IsAllowed(command.Content, command.ContentType))
        {
            throw new DomainException("evidence.mime_not_allowed", "The file type is not permitted, or its contents do not match its declared type.");
        }

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(command.Content));
        var key = $"audits/{exception.AuditId}/map-actions/{command.ActionId}/{Guid.NewGuid():N}";
        var storagePath = await storage.SaveAsync(key, command.Content, cancellationToken);

        var file = EvidenceFile.Create(exception.AuditId, EvidenceContextType.MapAction, command.ActionId, storagePath,
            command.Filename, command.ContentType, command.Content.LongLength, sha256, uploaderId, clock.UtcNow);
        evidence.Add(file);
        audit.Record(AuditEventTypes.EvidenceUploaded, AuditTargetTypes.EvidenceFile, file.Id, payload: new { command.ActionId, file.OriginalFilename });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return file.ToDto();
    }
}

internal static class MapHandlerSupport
{
    public static async Task<(Domain.Exceptions.AuditException Exception, Domain.Audits.Audit Audit)> LoadScopedAsync(
        IExceptionRepository exceptions, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser, Guid exceptionId, CancellationToken cancellationToken)
    {
        var exception = await exceptions.GetByIdAsync(exceptionId, cancellationToken) ?? throw new NotFoundException("Exception", exceptionId);
        var auditEntity = await audits.GetByIdAsync(exception.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", exception.AuditId);
        await ExceptionAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);
        return (exception, auditEntity);
    }

    public static async Task<bool> HasAnyMapEvidenceAsync(Domain.Exceptions.AuditException exception, IEvidenceRepository evidence, CancellationToken cancellationToken)
    {
        foreach (var action in exception.MapActions)
        {
            var files = await evidence.ListForContextAsync(exception.AuditId, EvidenceContextType.MapAction, action.Id, cancellationToken);
            if (files.Count > 0)
            {
                return true;
            }
        }

        return false;
    }
}
