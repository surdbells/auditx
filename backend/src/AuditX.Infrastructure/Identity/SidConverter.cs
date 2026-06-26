using System.Globalization;
using System.Text;

namespace AuditX.Infrastructure.Identity;

/// <summary>
/// Converts Windows security identifiers between their binary form (as returned by LDAP) and the
/// canonical string form (<c>S-1-5-21-...</c>). Implemented without platform-specific APIs so it works
/// on both Windows and Linux deployments.
/// </summary>
public static class SidConverter
{
    public static string ToStringSid(byte[] sid)
    {
        ArgumentNullException.ThrowIfNull(sid);
        if (sid.Length < 8)
        {
            throw new FormatException("Invalid binary SID.");
        }

        var revision = sid[0];
        int subAuthorityCount = sid[1];

        long identifierAuthority = 0;
        for (var i = 0; i < 6; i++)
        {
            identifierAuthority = (identifierAuthority << 8) | sid[2 + i];
        }

        var builder = new StringBuilder("S-");
        builder.Append(revision).Append('-').Append(identifierAuthority);

        var offset = 8;
        for (var i = 0; i < subAuthorityCount; i++, offset += 4)
        {
            var subAuthority = BitConverter.ToUInt32(sid, offset);
            builder.Append('-').Append(subAuthority);
        }

        return builder.ToString();
    }

    public static byte[] ToBytes(string sid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sid);
        var parts = sid.Split('-');
        if (parts.Length < 3 || !parts[0].Equals("S", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException("Invalid SID string.");
        }

        var revision = byte.Parse(parts[1], CultureInfo.InvariantCulture);
        var identifierAuthority = long.Parse(parts[2], CultureInfo.InvariantCulture);
        var subAuthorities = parts.Skip(3).Select(p => uint.Parse(p, CultureInfo.InvariantCulture)).ToArray();

        var result = new byte[8 + (subAuthorities.Length * 4)];
        result[0] = revision;
        result[1] = (byte)subAuthorities.Length;
        for (var i = 0; i < 6; i++)
        {
            result[2 + i] = (byte)((identifierAuthority >> (8 * (5 - i))) & 0xFF);
        }

        var offset = 8;
        foreach (var subAuthority in subAuthorities)
        {
            BitConverter.GetBytes(subAuthority).CopyTo(result, offset);
            offset += 4;
        }

        return result;
    }

    /// <summary>Escapes a binary SID for use in an LDAP search filter (each byte as <c>\xx</c>).</summary>
    public static string ToLdapFilterValue(string sid)
    {
        var bytes = ToBytes(sid);
        var builder = new StringBuilder(bytes.Length * 3);
        foreach (var b in bytes)
        {
            builder.Append('\\').Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
