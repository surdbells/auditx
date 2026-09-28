namespace AuditX.Application.Abstractions;

/// <summary>
/// Captures the active configuration versions for the given domains as a small JSON map (M12, S5), e.g.
/// <c>{"exception_defaults":3}</c>. Used at exception raise to stamp <c>AuditException.ConfigurationVersionsJson</c>
/// with honest provenance of the config that produced the stored target date. SINGLE-STORE only: it reads the
/// institution_configurations store, never sanctions_grid / notification_rules (those live in other stores with no integer
/// version — stamping them would be fictional). A domain with no active version is omitted from the map.
/// </summary>
public interface IConfigurationSnapshotter
{
    string Capture(params string[] domains);
}
