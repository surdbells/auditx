using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AuditX.Domain.Reports;

/// <summary>
/// The institution branding a report is ISSUED under (M8): display name, primary/accent colours and logo. Captured
/// from the institution settings at generation time and rendered into every artefact, so an issued report keeps
/// the look it was issued with — a later rebrand never reaches back into it.
///
/// Every member is sanitised on construction (<see cref="Create"/>): colours must be <c>#rrggbb</c>, the logo must
/// be a base64 <c>data:image/*</c> URI of a known raster/vector type. Renderers can therefore place these values in
/// CSS / attributes / document parts without re-validating them.
/// </summary>
public sealed partial record ReportBranding
{
    public const string DefaultInstitutionName = "AuditX";
    public const string DefaultPrimaryColor = "#4f46e5";
    public const string DefaultAccentColor = "#7c3aed";

    /// <summary>Upper bound on the logo data URI (mirrors the institution-settings cap, with headroom).</summary>
    private const int MaxLogoChars = 800_000;

    private const int MaxNameLength = 200;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private ReportBranding(string institutionName, string primaryColor, string accentColor, string? logoDataUri)
    {
        InstitutionName = institutionName;
        PrimaryColor = primaryColor;
        AccentColor = accentColor;
        LogoDataUri = logoDataUri;
    }

    public string InstitutionName { get; }

    /// <summary>Primary brand colour, always <c>#rrggbb</c> (lower-case).</summary>
    public string PrimaryColor { get; }

    /// <summary>Accent brand colour, always <c>#rrggbb</c> (lower-case).</summary>
    public string AccentColor { get; }

    /// <summary>The logo as a base64 <c>data:image/*</c> URI, or null when the institution has none.</summary>
    public string? LogoDataUri { get; }

    /// <summary>The neutral branding used when nothing is configured (and for reports issued before snapshots existed).</summary>
    public static ReportBranding Default { get; } = new(DefaultInstitutionName, DefaultPrimaryColor, DefaultAccentColor, null);

    /// <summary>Build a sanitised branding; anything malformed falls back to the neutral default for that member.</summary>
    public static ReportBranding Create(string? institutionName, string? primaryColor, string? accentColor, string? logoDataUri)
    {
        var name = string.IsNullOrWhiteSpace(institutionName) ? DefaultInstitutionName : institutionName.Trim();
        if (name.Length > MaxNameLength)
        {
            name = name[..MaxNameLength];
        }

        return new ReportBranding(
            name,
            NormaliseColor(primaryColor) ?? DefaultPrimaryColor,
            NormaliseColor(accentColor) ?? DefaultAccentColor,
            NormaliseLogo(logoDataUri));
    }

    /// <summary>The logo's media type (e.g. <c>image/png</c>) and decoded bytes, or null when there is no logo.</summary>
    public (string MediaType, byte[] Bytes)? DecodeLogo()
    {
        if (LogoDataUri is null)
        {
            return null;
        }

        var match = LogoPattern().Match(LogoDataUri);
        if (!match.Success)
        {
            return null;
        }

        try
        {
            return (match.Groups["type"].Value.ToLowerInvariant(), Convert.FromBase64String(match.Groups["data"].Value));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// The compact, persisted record of this branding: name, colours and the logo's SHA-256 (not its bytes — the
    /// logo itself is embedded in the stored, hash-verified artefacts, so the row stays small).
    /// </summary>
    public string ToSnapshotJson()
    {
        var logo = DecodeLogo();
        return JsonSerializer.Serialize(
            new ReportBrandingSnapshot(
                InstitutionName, PrimaryColor, AccentColor,
                logo is { } l ? Convert.ToHexStringLower(SHA256.HashData(l.Bytes)) : null),
            JsonOptions);
    }

    /// <summary>Read a persisted snapshot; null (or malformed) yields null so callers can show "not recorded".</summary>
    public static ReportBrandingSnapshot? ReadSnapshot(string? snapshotJson)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ReportBrandingSnapshot>(snapshotJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? NormaliseColor(string? value)
    {
        var trimmed = value?.Trim();
        return trimmed is not null && ColorPattern().IsMatch(trimmed) ? trimmed.ToLower(CultureInfo.InvariantCulture) : null;
    }

    private static string? NormaliseLogo(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLogoChars || !LogoPattern().IsMatch(trimmed))
        {
            return null;
        }

        return trimmed;
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColorPattern();

    // Base64 only (no percent-encoded payloads), and only image types a browser renders inside <img>.
    [GeneratedRegex(@"^data:(?<type>image/(?:png|jpeg|jpg|gif|webp|svg\+xml));base64,(?<data>[A-Za-z0-9+/]+={0,2})$", RegexOptions.IgnoreCase)]
    private static partial Regex LogoPattern();
}

/// <summary>
/// The persisted record of the branding a report was issued under (see <see cref="ReportBranding.ToSnapshotJson"/>).
/// <paramref name="LogoSha256"/> is null when the report was issued without a logo.
/// </summary>
public sealed record ReportBrandingSnapshot(string InstitutionName, string PrimaryColor, string AccentColor, string? LogoSha256);
