using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Audits.Dtos;
using AuditX.Application.Audits.Mapping;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.Audits;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;

namespace AuditX.Application.Audits.Commands;

internal static class AuditParsing
{
    public static TeamRole ParseTeamRole(string? value)
        => Enum.TryParse<TeamRole>((value ?? string.Empty).Replace("_", string.Empty), ignoreCase: true, out var r)
            ? r
            : throw new ConflictException("audit.invalid_team_role", $"Unknown team role '{value}'.");

    public static ResponseType ParseResponseType(string? value)
        => Enum.TryParse<ResponseType>((value ?? string.Empty).Replace("_", string.Empty), ignoreCase: true, out var r)
            ? r
            : throw new ConflictException("audit.invalid_response_type", $"Unknown response type '{value}'.");

    /// <summary>
    /// A checklist item may only be assigned to a user who exists, is not deactivated, and is an active
    /// member of the audit's team — keeping assignments consistent with the team-removal guard (US-M4-006).
    /// </summary>
    public static async Task EnsureAssigneeAsync(Audit audit, Guid? assignedUserId, IUserRepository users, CancellationToken cancellationToken)
    {
        if (assignedUserId is not { } userId)
        {
            return;
        }

        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.Status == UserStatus.Deactivated)
        {
            throw new ConflictException("audit.user_not_assignable", "The assignee does not exist or is deactivated.");
        }

        if (!audit.TeamMembers.Any(m => m.IsActive && m.UserId == userId))
        {
            throw new ConflictException("audit.assignee_not_team_member", "A checklist item can only be assigned to an active team member.");
        }
    }
}

public sealed record AddAuditTeamMemberCommand(Guid AuditId, Guid UserId, string TeamRole, string Version) : ICommand<AuditDto>;

public sealed class AddAuditTeamMemberCommandHandler(
    IAuditRepository audits, IUserRepository users, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<AddAuditTeamMemberCommand, AuditDto>
{
    public async Task<AuditDto> Handle(AddAuditTeamMemberCommand command, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(command.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", command.AuditId);
        entity.EnsureVersion(command.Version);
        var user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null || user.Status == UserStatus.Deactivated)
        {
            throw new ConflictException("audit.user_not_assignable", "The user does not exist or is deactivated.");
        }

        entity.AddTeamMember(command.UserId, AuditParsing.ParseTeamRole(command.TeamRole), addedBy: null, clock.UtcNow);
        audit.Record(AuditEventTypes.AuditTeamMemberAdded, AuditTargetTypes.AuditTeamMember, entity.Id, payload: new { command.UserId, command.TeamRole });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

public sealed record RemoveAuditTeamMemberCommand(Guid AuditId, Guid MembershipId, string Version) : ICommand<Unit>;

public sealed class RemoveAuditTeamMemberCommandHandler(IAuditRepository audits, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveAuditTeamMemberCommand, Unit>
{
    public async Task<Unit> Handle(RemoveAuditTeamMemberCommand command, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(command.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", command.AuditId);
        entity.EnsureVersion(command.Version);
        entity.RemoveTeamMember(command.MembershipId, clock.UtcNow);
        audit.Record(AuditEventTypes.AuditTeamMemberRemoved, AuditTargetTypes.AuditTeamMember, entity.Id, payload: new { command.MembershipId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record TransferAuditLeadCommand(Guid AuditId, Guid NewLeadUserId, bool RemoveOutgoing, string Version) : ICommand<AuditDto>;

public sealed class TransferAuditLeadCommandHandler(
    IAuditRepository audits, IUserRepository users, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<TransferAuditLeadCommand, AuditDto>
{
    public async Task<AuditDto> Handle(TransferAuditLeadCommand command, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(command.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", command.AuditId);
        entity.EnsureVersion(command.Version);
        var user = await users.GetByIdAsync(command.NewLeadUserId, cancellationToken);
        if (user is null || user.Status == UserStatus.Deactivated)
        {
            throw new ConflictException("audit.user_not_assignable", "The new lead does not exist or is deactivated.");
        }

        entity.TransferLead(command.NewLeadUserId, command.RemoveOutgoing, clock.UtcNow);
        audit.Record(AuditEventTypes.AuditLeadTransferred, AuditTargetTypes.Audit, entity.Id, payload: new { command.NewLeadUserId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

public sealed record AddAuditChecklistItemCommand(
    Guid AuditId, string Prompt, string? ReferenceNotes, string ResponseType, string? SectionName, bool IsRequired, Guid? AssignedUserId, string Version) : ICommand<AuditDto>;

public sealed class AddAuditChecklistItemCommandHandler(IAuditRepository audits, IUserRepository users, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<AddAuditChecklistItemCommand, AuditDto>
{
    public async Task<AuditDto> Handle(AddAuditChecklistItemCommand command, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(command.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", command.AuditId);
        entity.EnsureVersion(command.Version);
        await AuditParsing.EnsureAssigneeAsync(entity, command.AssignedUserId, users, cancellationToken);
        var item = entity.AddChecklistItem(command.Prompt, command.ReferenceNotes, AuditParsing.ParseResponseType(command.ResponseType), command.SectionName, command.IsRequired, command.AssignedUserId);
        audit.Record(AuditEventTypes.AuditChecklistItemAdded, AuditTargetTypes.AuditChecklistItem, entity.Id, payload: new { itemId = item.Id });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

public sealed record EditAuditChecklistItemCommand(
    Guid AuditId, Guid ItemId, string Prompt, string? ReferenceNotes, string? SectionName, bool IsRequired, Guid? AssignedUserId, string Version) : ICommand<AuditDto>;

public sealed class EditAuditChecklistItemCommandHandler(IAuditRepository audits, IUserRepository users, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<EditAuditChecklistItemCommand, AuditDto>
{
    public async Task<AuditDto> Handle(EditAuditChecklistItemCommand command, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(command.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", command.AuditId);
        entity.EnsureVersion(command.Version);
        await AuditParsing.EnsureAssigneeAsync(entity, command.AssignedUserId, users, cancellationToken);
        entity.EditChecklistItem(command.ItemId, command.Prompt, command.ReferenceNotes, command.SectionName, command.IsRequired, command.AssignedUserId);
        audit.Record(AuditEventTypes.AuditChecklistItemEdited, AuditTargetTypes.AuditChecklistItem, entity.Id, payload: new { command.ItemId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

public sealed record RemoveAuditChecklistItemCommand(Guid AuditId, Guid ItemId, string Version) : ICommand<Unit>;

public sealed class RemoveAuditChecklistItemCommandHandler(IAuditRepository audits, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveAuditChecklistItemCommand, Unit>
{
    public async Task<Unit> Handle(RemoveAuditChecklistItemCommand command, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(command.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", command.AuditId);
        entity.EnsureVersion(command.Version);
        entity.RemoveChecklistItem(command.ItemId);
        audit.Record(AuditEventTypes.AuditChecklistItemRemoved, AuditTargetTypes.AuditChecklistItem, entity.Id, payload: new { command.ItemId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
