using System.Text;

namespace AuditX.Application.Notifications;

/// <summary>
/// Derives a stable snake_case event-type key from a domain-event record type name (e.g.
/// <c>AuditTeamMemberAddedEvent</c> → <c>audit_team_member_added</c>) and lists the catalogue of event
/// types M10 knows about. The mapping is by convention so M1–M6 event records need no changes.
/// </summary>
public static class NotificationEvents
{
    public static string Derive(string eventTypeName)
    {
        var name = eventTypeName.EndsWith("Event", StringComparison.Ordinal) ? eventTypeName[..^"Event".Length] : eventTypeName;
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && (!char.IsUpper(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>The catalogue surfaced by GET /admin/events/catalogue and seeded with default rules.</summary>
    public static readonly IReadOnlyList<string> Catalogue =
    [
        // M1 identity
        "role_granted", "role_revoked", "delegation_started", "delegation_ended",
        "maker_checker_submitted", "maker_checker_rejected",
        // M4 audit lifecycle
        "audit_team_member_added", "audit_lead_transferred", "audit_transitioned", "audit_completed", "audit_cancelled",
        // M5 execution
        "item_assigned",
        // M6 exceptions
        "exception_raised", "exception_owner_reassigned", "map_submitted", "map_approved", "map_rejected",
        "map_completed", "exception_pending_cia", "exception_closed", "exception_cancelled",
        // M3 planning
        "plan_submitted",
        // M8 reports
        "report_generated", "report_distributed", "report_hash_mismatch",
        // M9 analytics
        "recurrence_cluster_detected",
    ];
}
