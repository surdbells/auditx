namespace AuditX.Application.Ac;

/// <summary>
/// Names of the seeded, non-builtin, least-privilege M13 roles. Referenced both by the seeder (which creates them
/// via <c>Role.CreateCustom</c> + <c>SetPermissions</c>) and by handlers that resolve the AC cohort / CIA by role
/// name (M10-style recipient resolution).
/// </summary>
public static class AcRoles
{
    public const string AuditCommitteeMember = "Audit Committee Member";
    public const string AuditCommitteeChair = "Audit Committee Chair";
    public const string ChiefInternalAuditor = "Chief Internal Auditor";
}
