using System.Text.Json;

namespace AuditX.Application.Scheduling;

/// <summary>The resolved recipients of a scheduled report — directory users (by id) plus ad-hoc email addresses.</summary>
public sealed record ReportScheduleRecipients(IReadOnlyList<Guid> UserIds, IReadOnlyList<string> Emails)
{
    private sealed record Payload(List<Guid> UserIds, List<string> Emails);

    // Web defaults: camelCase output AND case-insensitive input, so the opaque payload round-trips regardless of the
    // casing any producer used (a silent case mismatch here would drop every recipient and deliver to no-one).
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static readonly ReportScheduleRecipients Empty = new([], []);

    /// <summary>Serialises to the opaque <c>RecipientsJson</c> payload stored on the schedule.</summary>
    public string ToJson() => JsonSerializer.Serialize(new Payload(
        UserIds.Distinct().ToList(),
        Emails.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()),
        JsonOptions);

    /// <summary>Parses the stored payload, tolerating null/blank into empty lists.</summary>
    public static ReportScheduleRecipients Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Empty;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(json, JsonOptions);
            return payload is null
                ? Empty
                : new ReportScheduleRecipients(payload.UserIds ?? [], payload.Emails ?? []);
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    public int Count => UserIds.Count + Emails.Count;
}
