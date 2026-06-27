namespace AuditX.Application.Sanctions;

/// <summary>
/// Names of the seeded, non-builtin, bank-reconfigurable roles for the M7 HR/DC/Appeals cohorts. Referenced both by
/// the seeder (which creates them) and by handlers that resolve recipients/authorities by role name.
/// </summary>
public static class SanctionsRoles
{
    public const string HrRepresentative = "HR Representative";
    public const string DisciplinaryCommitteeMember = "Disciplinary Committee Member";
    public const string AppealsAuthority = "Appeals Authority";
}
