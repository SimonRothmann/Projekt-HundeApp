using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Betrieb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Dogity.Application.Betrieb;

/// <summary>
/// Der Wartungshinweis (siehe <see cref="Wartungshinweis"/>). Die Rechteprüfung für
/// das Setzen sitzt am AdminController; hier liegt nur, was der öffentliche Abruf
/// und die Eingabeprüfung brauchen.
/// </summary>
public class WartungshinweisService(IApplicationDbContext db, IMemoryCache cache, TimeProvider timeProvider) : IWartungshinweisService
{
    /// <summary>Wie lange die Antwort im Speicher bleibt - jede Seite der App fragt sie beim Laden ab.</summary>
    public static readonly TimeSpan Zwischenspeicher = TimeSpan.FromSeconds(30);

    private const string CacheSchluessel = "wartungshinweis";

    /// <summary>Der Stand, der im Speicher liegt: nur ein eingeschalteter Hinweis, ohne Entity-Verweis.</summary>
    private sealed record Stand(string? Text, DateTimeOffset Beginn, DateTimeOffset? Ende, DateTimeOffset GeaendertAm);

    public async Task<Result<WartungshinweisDto?>> GetAktuellenAsync(CancellationToken ct = default)
    {
        // Gemerkt wird der Stand, nicht die fertige Antwort: "läuft" und "abgelaufen"
        // hängen an der Uhr und sollen auch innerhalb der 30 Sekunden stimmen.
        var stand = await cache.GetOrCreateAsync(CacheSchluessel, async eintrag =>
        {
            eintrag.AbsoluteExpirationRelativeToNow = Zwischenspeicher;
            return await db.Wartungshinweise
                .Where(h => h.Aktiv)
                .Select(h => new Stand(h.Text, h.Beginn, h.Ende, h.GeaendertAm))
                .FirstOrDefaultAsync(ct);
        });

        var jetzt = timeProvider.GetUtcNow();
        if (stand is null || (stand.Ende is not null && jetzt >= stand.Ende))
            return Result<WartungshinweisDto?>.Success(null);

        return Result<WartungshinweisDto?>.Success(
            new WartungshinweisDto(stand.Text, stand.Beginn, stand.Ende, jetzt >= stand.Beginn, stand.GeaendertAm));
    }

    public async Task<Result<WartungshinweisAdminDto>> GetGespeicherteAsync(CancellationToken ct = default)
    {
        var hinweis = await db.Wartungshinweise.AsNoTracking().FirstOrDefaultAsync(ct);
        return Result<WartungshinweisAdminDto>.Success(hinweis is null
            ? new WartungshinweisAdminDto(null, null, null, false, null)
            : ZuAdminDto(hinweis));
    }

    public async Task<Result<WartungshinweisAdminDto>> SetzenAsync(SetWartungshinweisRequest request, CancellationToken ct = default)
    {
        var text = string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim();

        if (Textlaengen.ZuLang(text, Textlaengen.WartungshinweisText, "Der Text") is { } zuLang)
            return Result<WartungshinweisAdminDto>.Failure(zuLang);
        if (request.Ende is not null && request.Ende <= request.Beginn)
            return Result<WartungshinweisAdminDto>.Failure("Das Ende muss nach dem Beginn liegen.");

        var hinweis = await db.Wartungshinweise.FirstOrDefaultAsync(ct);
        if (hinweis is null)
        {
            hinweis = new Wartungshinweis();
            db.Wartungshinweise.Add(hinweis);
        }

        hinweis.Text = text;
        hinweis.Beginn = request.Beginn;
        hinweis.Ende = request.Ende;
        hinweis.Aktiv = request.Aktiv;
        hinweis.GeaendertAm = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);

        // Der Betreiber soll die Änderung sofort sehen, nicht erst nach 30 Sekunden.
        cache.Remove(CacheSchluessel);
        return Result<WartungshinweisAdminDto>.Success(ZuAdminDto(hinweis));
    }

    public async Task<Result> AusschaltenAsync(CancellationToken ct = default)
    {
        var hinweis = await db.Wartungshinweise.FirstOrDefaultAsync(ct);
        if (hinweis is { Aktiv: true })
        {
            hinweis.Aktiv = false;
            hinweis.GeaendertAm = timeProvider.GetUtcNow();
            await db.SaveChangesAsync(ct);
            cache.Remove(CacheSchluessel);
        }

        return Result.Success();
    }

    private static WartungshinweisAdminDto ZuAdminDto(Wartungshinweis h) =>
        new(h.Text, h.Beginn, h.Ende, h.Aktiv, h.GeaendertAm);
}
