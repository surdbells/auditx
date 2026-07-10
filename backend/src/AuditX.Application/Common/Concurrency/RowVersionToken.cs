using System.Buffers.Text;

namespace AuditX.Application.Common.Concurrency;

/// <summary>
/// Encodes an EF Core rowversion (<c>byte[]</c>) as an opaque, URL-safe optimistic-concurrency token, and compares an
/// incoming token against a live rowversion. Uses Base64Url (RFC 4648 §5: <c>-</c>/<c>_</c>, no padding) rather than
/// standard Base64 so the token survives a query string unchanged: standard Base64's <c>+</c> is decoded to a space by
/// ASP.NET Core query parsing, which would spuriously fail the comparison and 409 forever for a client that sends the
/// raw (unencoded) token on a <c>?version=</c> delete. The token is opaque to clients (the Angular models treat it as a
/// plain string), so switching the encoding needs no client change — provided every emit AND every compare route
/// through this one boundary.
/// </summary>
public static class RowVersionToken
{
    /// <summary>The URL-safe token for a rowversion (empty string for a null/empty version).</summary>
    public static string Encode(byte[]? version)
        => version is null || version.Length == 0 ? string.Empty : Base64Url.EncodeToString(version);

    /// <summary>True when <paramref name="token"/> is the current token for <paramref name="version"/> (ordinal).</summary>
    public static bool Matches(byte[]? version, string? token)
        => string.Equals(Encode(version), token, StringComparison.Ordinal);
}
