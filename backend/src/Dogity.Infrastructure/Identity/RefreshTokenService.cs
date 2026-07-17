using System.Security.Cryptography;
using Dogity.Application.Abstractions;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dogity.Infrastructure.Identity;

/// <summary>
/// Refresh-Token-Verwaltung mit Rotation und Reuse-Erkennung (siehe
/// <see cref="RefreshToken"/> / TODO.md Roadmap 6). Der Rohwert des Tokens
/// wird nie gespeichert - nur sein SHA-256-Hash; das Nachschlagen erfolgt
/// über den Hash des vorgelegten Rohwerts.
/// </summary>
public class RefreshTokenService(ApplicationDbContext db, IOptions<JwtSettings> options) : IRefreshTokenService
{
    private readonly JwtSettings _settings = options.Value;

    public async Task<string> IssueAsync(Guid userId, CancellationToken ct = default)
    {
        // Abgelaufene/verbrauchte Tokens des Nutzers opportunistisch aufräumen,
        // damit die Tabelle nicht unbegrenzt wächst (kein separater Cron nötig).
        var cutoff = DateTimeOffset.UtcNow;
        var stale = await db.RefreshTokens
            .Where(t => t.UserId == userId && (t.ExpiresAt < cutoff || t.RevokedAt != null))
            .ToListAsync(ct);
        if (stale.Count > 0) db.RefreshTokens.RemoveRange(stale);

        var raw = GenerateRawToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(raw),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_settings.RefreshExpiryDays),
        });
        await db.SaveChangesAsync(ct);
        return raw;
    }

    public async Task<RefreshRotationResult> RotateAsync(string rawToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return new RefreshRotationResult(false);

        var hash = Hash(rawToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (existing is null) return new RefreshRotationResult(false);

        // Reuse-Erkennung: Wird ein bereits ROTIERTER Token (hat einen
        // Nachfolger) erneut vorgelegt, wurde er entweder gestohlen oder
        // mehrfach abgespielt - aus Vorsicht alle aktiven Tokens des Nutzers
        // widerrufen (erzwingt Neu-Login überall). Ein nur per Logout
        // widerrufener Token (kein Nachfolger) oder ein abgelaufener Token ist
        // dagegen KEIN Diebstahl-Signal: dann still scheitern, ohne andere
        // Geräte des Nutzers mit abzumelden.
        if (existing.RevokedAt is not null && existing.ReplacedByTokenHash is not null)
        {
            await RevokeAllForUserAsync(existing.UserId, ct);
            return new RefreshRotationResult(false);
        }
        if (existing.RevokedAt is not null || existing.ExpiresAt <= DateTimeOffset.UtcNow)
            return new RefreshRotationResult(false);

        var newRaw = GenerateRawToken();
        var newToken = new RefreshToken
        {
            UserId = existing.UserId,
            TokenHash = Hash(newRaw),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_settings.RefreshExpiryDays),
        };
        existing.RevokedAt = DateTimeOffset.UtcNow;
        existing.ReplacedByTokenHash = newToken.TokenHash;
        db.RefreshTokens.Add(newToken);
        await db.SaveChangesAsync(ct);

        return new RefreshRotationResult(true, existing.UserId, newRaw);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return;
        var hash = Hash(rawToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null, ct);
        if (token is null) return;
        token.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);
        if (active.Count == 0) return;
        var now = DateTimeOffset.UtcNow;
        foreach (var token in active) token.RevokedAt = now;
        await db.SaveChangesAsync(ct);
    }

    // 32 Byte Zufall, Base64Url-kodiert - genug Entropie, dass der Token nicht
    // erraten werden kann, und URL-/JSON-sicher ohne Sonderzeichen.
    private static string GenerateRawToken() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
