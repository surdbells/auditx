using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Identity;
using AuditX.Infrastructure.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AuditX.Infrastructure.Identity;

/// <summary>
/// Redis-backed denylist of revoked session tokens. Each entry lives only until the token's natural
/// expiry, so the set stays bounded (US-M1-009).
/// </summary>
public sealed class RedisTokenDenylist(IConnectionMultiplexer redis, IOptions<RedisOptions> options, IClock clock)
    : ITokenDenylist
{
    private readonly string _prefix = $"{options.Value.InstanceName}denylist:";

    public async Task RevokeAsync(string tokenId, DateTimeOffset tokenExpiresAt, CancellationToken cancellationToken = default)
    {
        var ttl = tokenExpiresAt - clock.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        await redis.GetDatabase().StringSetAsync(Key(tokenId), "revoked", ttl);
    }

    public async Task<bool> IsRevokedAsync(string tokenId, CancellationToken cancellationToken = default)
        => await redis.GetDatabase().KeyExistsAsync(Key(tokenId));

    private RedisKey Key(string tokenId) => $"{_prefix}{tokenId}";
}
