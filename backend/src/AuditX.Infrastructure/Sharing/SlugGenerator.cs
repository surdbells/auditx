using System.Security.Cryptography;
using AuditX.Application.Abstractions;

namespace AuditX.Infrastructure.Sharing;

/// <summary>
/// Cryptographically-strong, URL-safe slug generator (D3-B): 16 random bytes (128 bits) rendered as base64url
/// without padding (22 chars). Enough entropy that slugs are unguessable and collisions are negligible.
/// </summary>
public sealed class SlugGenerator : ISlugGenerator
{
    public string NewSlug()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
