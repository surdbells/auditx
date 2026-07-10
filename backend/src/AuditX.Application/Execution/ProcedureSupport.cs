using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Domain.Audits;
using AuditX.Domain.Authorization;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Execution;

namespace AuditX.Application.Execution;

internal static class ProcedureParsing
{
    public static ProcedureType ParseType(string? value)
        => EnumExtensions.TryParseSnake<ProcedureType>(value, out var t)
            ? t
            : throw new DomainException("procedure.invalid_type", $"Unknown procedure type '{value}'.");

    public static SamplingMethod? ParseMethod(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return EnumExtensions.TryParseSnake<SamplingMethod>(value, out var m)
            ? m
            : throw new DomainException("procedure.invalid_method", $"Unknown sampling method '{value}'.");
    }
}

internal static class ProcedureConcurrency
{
    public static void EnsureVersion(this AuditProcedure procedure, string expectedVersion)
    {
        if (!string.Equals(Convert.ToBase64String(procedure.Version ?? []), expectedVersion, StringComparison.Ordinal))
        {
            throw new ConflictException("procedure.concurrency_conflict", "The procedure was modified by someone else; reload and retry.");
        }
    }
}

internal static class ProcedureAccess
{
    /// <summary>Fieldwork phases in which a procedure may be recorded.</summary>
    private static readonly AuditStatus[] Recordable = [AuditStatus.InProgress, AuditStatus.UnderReview];

    private static bool IsTeamMember(Audit audit, Guid uid)
        => audit.TeamMembers.Any(m => m.IsActive && m.UserId == uid);

    /// <summary>Record authorization: an active team member or an audit manager, and the audit is in a fieldwork phase.</summary>
    public static async Task EnsureCanRecordAsync(Audit audit, Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        await EnsureTeamOrManagerAsync(audit, userId, permissions, cancellationToken);
        if (!Recordable.Contains(audit.Status))
        {
            throw new ConflictException("procedure.audit_not_recordable", "Procedures can only be recorded while the audit is in fieldwork or under review.");
        }
    }

    /// <summary>View authorization: an active team member or an audit manager.</summary>
    public static Task EnsureCanViewAsync(Audit audit, Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
        => EnsureTeamOrManagerAsync(audit, userId, permissions, cancellationToken);

    /// <summary>Delete authorization: only the performer or an audit manager may remove a procedure.</summary>
    public static async Task EnsureCanModifyAsync(AuditProcedure procedure, Audit audit, Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (userId is not { } uid)
        {
            throw new ForbiddenAccessException();
        }

        if (procedure.PerformedByUserId == uid)
        {
            return;
        }

        if (await permissions.HasPermissionAsync(uid, PermissionKeys.ManageAudit, audit.Id.ToString(), cancellationToken))
        {
            return;
        }

        throw new ForbiddenAccessException("Only the performer or an audit manager can modify this procedure.");
    }

    private static async Task EnsureTeamOrManagerAsync(Audit audit, Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (userId is not { } uid)
        {
            throw new ForbiddenAccessException();
        }

        if (IsTeamMember(audit, uid))
        {
            return;
        }

        if (await permissions.HasPermissionAsync(uid, PermissionKeys.ManageAudit, audit.Id.ToString(), cancellationToken))
        {
            return;
        }

        throw new ForbiddenAccessException("You are not a member of this audit.");
    }
}
