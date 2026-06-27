using System.Text.Json;
using AuditX.Application.Abstractions;

namespace AuditX.Infrastructure.Configuration;

/// <summary>
/// Captures the active configuration versions for the given domains as a small JSON map, e.g.
/// <c>{"exception_defaults":3}</c> (M12, S5). SINGLE-STORE only (bank_configurations via the active-config
/// provider). A domain with no active version is omitted. Used at exception raise to stamp honest config provenance.
/// </summary>
public sealed class ConfigurationSnapshotter(IActiveConfigurationProvider activeProvider) : IConfigurationSnapshotter
{
    public string Capture(params string[] domains)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var domain in domains)
        {
            if (activeProvider.GetActiveVersion(domain) is { } version)
            {
                map[domain] = version;
            }
        }

        return JsonSerializer.Serialize(map);
    }
}
