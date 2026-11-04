using System.Security.Cryptography;
using Dogity.Application.Abstractions;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dogity.Infrastructure.Identity;

/// <summary>
/// Refresh-Token-Verwaltung mit Rotation und Reuse-Erkennung (siehe
/// <see cref="RefreshToken"/> / TODO.md Roadmap 6). Der Rohwert des Tokens
/// wird nie gespeichert - nur sein SHA-256-Hash; das Nachschlagen erfolgt
/// über den Hash des vorgelegten Rohwerts.
///
/// Die Rotation verzeiht eine verlorene Antwort: Erreicht der neue Token das
/// Gerät nie (Netz weg, App vom Betriebssystem eingefroren), legt es den alten
/// noch einmal vor - das ist kein Diebstahl, solange der Nachfolger unbenutzt
/// ist. Jede Ablehnung und jede Toleranz steht mit dem Präfix "Sitzung:" im
/// Log (nie Token, Hash oder E-Mail), damit sich Abmeldungen nachvollziehen lassen.
/// </summary>
public class RefreshTokenService(
    ApplicationDbContext db,
    IOptions<JwtSettings> options,
    ILogger<RefreshTokenService> logger) : IRefreshTokenService
{
    private readonly JwtSettings _settings = options.Value;

    /// <summary>
    /// Toleranzfenster: So lange nach der Rotation darf der alte Token noch
    /// einmal vorgelegt werden (verlorene Antwort), und so lange bleibt ein
    /// rotierter Token in der Tabelle, damit er überhaupt wiedererkannt wird.
    /// Ein Gerät, das tagelang nicht online war (Handy im Urlaub ohne Netz),
    /// soll nicht abgemeldet werden - nach einer Woche gilt der Token als
    /// verbraucht.
    /// </summary>
    private static readonly TimeSpan Toleranzfenster = TimeSpan.FromDays(7);

    public async Task<string> IssueAsync(Guid userId, CancellationToken ct = default)
    {
        // Abgelaufene Tokens und längst widerrufene des Nutzers opportunistisch
        // aufräumen, damit die Tabelle nicht unbegrenzt wächst (kein separater
        // Cron nötig). Frisch widerrufene bleiben im Toleranzfenster stehen:
        // Meldet sich jemand auf Gerät B an, während Gerät A noch einen
        // rotierten Token hält, muss der bei der Wiedervorlage noch bekannt sein.
        var cutoff = DateTimeOffset.UtcNow;
        var toleranzEnde = cutoff - Toleranzfenster;
        var stale = await db.RefreshTokens
            .Where(t => t.UserId == userId && (t.ExpiresAt < cutoff || (t.RevokedAt != null && t.RevokedAt < toleranzEnde)))
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
        if (existing is null)
        {
            logger.LogInformation("Sitzung: unbekannter Token");
            return new RefreshRotationResult(false);
        }

        var jetzt = DateTimeOffset.UtcNow;

        // Wird ein bereits ROTIERTER Token (hat einen Nachfolger) erneut
        // vorgelegt, gibt es zwei Lesarten. Hat das Gerät die Antwort der
        // Erneuerung nie erhalten, ist der Nachfolger noch unbenutzt: dann
        // bekommt es einen frischen Token, alle anderen Sitzungen bleiben. Ist
        // der Nachfolger dagegen schon benutzt (oder das Fenster vorbei), wurde
        // der Token mehrfach abgespielt oder gestohlen - aus Vorsicht alle
        // Tokens des Nutzers widerrufen (erzwingt Neu-Login überall). Ein nur
        // per Logout widerrufener Token (kein Nachfolger) oder ein abgelaufener
        // Token ist KEIN Diebstahl-Signal: dann still scheitern, ohne andere
        // Geräte des Nutzers mit abzumelden.
        RefreshToken? tolerierterNachfolger = null;
        if (existing.RevokedAt is not null && existing.ReplacedByTokenHash is not null)
        {
            var nachfolger = await db.RefreshTokens.FirstOrDefaultAsync(
                t => t.TokenHash == existing.ReplacedByTokenHash && t.UserId == existing.UserId, ct);
            if (nachfolger is null
                || nachfolger.RevokedAt is not null
                || nachfolger.ExpiresAt <= jetzt
                || existing.RevokedAt < jetzt - Toleranzfenster)
            {
                logger.LogWarning("Sitzung: Wiederverwendung – alle Sitzungen widerrufen (Konto {KontoId})", existing.UserId);
                await RevokeAllForUserAsync(existing.UserId, ct);
                return new RefreshRotationResult(false);
            }

            tolerierterNachfolger = nachfolger;
        }
        else if (existing.RevokedAt is not null)
        {
            logger.LogInformation("Sitzung: abgemeldet (widerrufen ohne Nachfolger) (Konto {KontoId})", existing.UserId);
            return new RefreshRotationResult(false);
        }
        else if (existing.ExpiresAt <= jetzt)
        {
            logger.LogInformation("Sitzung: abgelaufen (Konto {KontoId})", existing.UserId);
            return new RefreshRotationResult(false);
        }

        var newRaw = GenerateRawToken();
        var newToken = new RefreshToken
        {
            UserId = existing.UserId,
            TokenHash = Hash(newRaw),
            ExpiresAt = jetzt.AddDays(_settings.RefreshExpiryDays),
        };
        if (tolerierterNachfolger is not null)
        {
            // Der unbenutzte Nachfolger wird mit verbraucht: Er steht jetzt
            // selbst in der Kette und zeigt auf den neuen Token, ebenso der
            // vorgelegte. Wer einen von beiden später vorlegt, wird nach
            // derselben Regel behandelt - eine Stufe genügt.
            tolerierterNachfolger.RevokedAt = jetzt;
            tolerierterNachfolger.ReplacedByTokenHash = newToken.TokenHash;
            logger.LogInformation("Sitzung: verlorene Antwort toleriert (Konto {KontoId})", existing.UserId);
        }
        else
        {
            existing.RevokedAt = jetzt;
        }
        existing.ReplacedByTokenHash = newToken.TokenHash;
        db.RefreshTokens.Add(newToken);
        await db.SaveChangesAsync(ct);

        return new RefreshRotationResult(true, existing.UserId, newRaw);
    }

    public async Task<RefreshRotationResult> RefreshAsync(string rawToken, CancellationToken ct = default)
    {
        var rotation = await RotateAsync(rawToken, ct);
        if (!rotation.Succeeded) return rotation;

        // Nutzer inzwischen vom Admin gesperrt oder gelöscht: kein neuer
        // Access-Token, alle Sitzungen enden. Die kurze Sperre nach falschen
        // Passwörtern zählt hier NICHT - die kann jede:r mit der E-Mail-Adresse
        // auslösen, und der Refresh-Token belegt eine frühere erfolgreiche
        // Anmeldung (siehe Sperren).
        var konto = await db.Users
            .Where(u => u.Id == rotation.UserId)
            .Select(u => new { u.LockoutEnd })
            .FirstOrDefaultAsync(ct);
        if (konto is not null
            && Sperren.Bestimme(konto.LockoutEnd, DateTimeOffset.UtcNow) != Sperrart.Dauerhaft)
            return rotation;

        logger.LogWarning("Sitzung: Konto gesperrt/gelöscht (Konto {KontoId})", rotation.UserId);

        // Ohne Konto gibt es nichts mehr zu widerrufen, die Zeilen sind
        // Altlast (z. B. nach einer Löschung, die mittendrin abbrach).
        if (konto is null)
            await DeleteAllForUserAsync(rotation.UserId, ct);
        else
            await RevokeAllForUserAsync(rotation.UserId, ct);
        return new RefreshRotationResult(false);
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

    public Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default) =>
        RevokeAllExceptAsync(userId, keepRawToken: null, ct);

    public async Task RevokeAllExceptAsync(Guid userId, string? keepRawToken, CancellationToken ct = default)
    {
        // Der verschonte Token muss zu DIESEM Nutzer gehören - das stellt die
        // Bedingung auf UserId sicher: Ein fremder Hash kommt in der Menge
        // ohnehin nicht vor.
        var keepHash = string.IsNullOrWhiteSpace(keepRawToken) ? "" : Hash(keepRawToken);
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.TokenHash != keepHash)
            .ToListAsync(ct);
        if (active.Count == 0) return;
        var now = DateTimeOffset.UtcNow;
        foreach (var token in active) token.RevokedAt = now;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await db.RefreshTokens.Where(t => t.UserId == userId).ToListAsync(ct);
        if (tokens.Count == 0) return;
        db.RefreshTokens.RemoveRange(tokens);
        await db.SaveChangesAsync(ct);
    }

    public Task<int> CountAccountsWithTokenSinceAsync(DateTimeOffset since, CancellationToken ct = default) =>
        db.RefreshTokens
            .Where(t => t.CreatedAt >= since && db.Users.Any(u => u.Id == t.UserId))
            .Select(t => t.UserId)
            .Distinct()
            .CountAsync(ct);

    /// <summary>
    /// So viele Tokens je Durchgang: Ein Konto bringt je Gerät eine Zeile mit,
    /// der Altbestand ist überschaubar - Portionen halten die einzelne
    /// Abfrage und den Speichervorgang trotzdem klein.
    /// </summary>
    private const int Portion = 500;

    public async Task<int> DeleteOrphanedAsync(CancellationToken ct = default)
    {
        var gesamt = 0;
        while (true)
        {
            var waisen = await db.RefreshTokens
                .Where(t => !db.Users.Any(u => u.Id == t.UserId))
                .OrderBy(t => t.CreatedAt)
                .Take(Portion)
                .ToListAsync(ct);
            if (waisen.Count == 0) return gesamt;

            db.RefreshTokens.RemoveRange(waisen);
            await db.SaveChangesAsync(ct);
            gesamt += waisen.Count;
        }
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
