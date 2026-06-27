using System.Text.RegularExpressions;
using AuditX.Application.Abstractions.Notifications;

namespace AuditX.Infrastructure.Notifications;

/// <summary>
/// Lightweight <c>{{ Field }}</c> substitution renderer for notification templates (M10). Replaces each
/// placeholder with the matching model value; unknown placeholders render empty. A heavier templating
/// engine (loops/conditionals) is a later enhancement — the seeded templates are plain substitutions.
/// </summary>
public sealed partial class SimpleTemplateRenderer : ITemplateRenderer
{
    [GeneratedRegex(@"\{\{\s*(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();

    public (string? Subject, string Body) Render(string? subjectTemplate, string bodyTemplate, IReadOnlyDictionary<string, object?> model)
    {
        var body = Substitute(bodyTemplate, model);
        var subject = subjectTemplate is null ? null : Substitute(subjectTemplate, model);
        return (subject, body);
    }

    private static string Substitute(string template, IReadOnlyDictionary<string, object?> model)
        => PlaceholderRegex().Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            return model.TryGetValue(key, out var value) && value is not null ? value.ToString() ?? string.Empty : string.Empty;
        });
}
