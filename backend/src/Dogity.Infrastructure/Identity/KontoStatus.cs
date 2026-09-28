using Dogity.Application.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Dogity.Infrastructure.Identity;

/// <summary>
/// Ob ein Konto mit gültigem Access-Token noch arbeiten darf: Es gibt es noch,
/// und der Admin hat es nicht gesperrt.
///
/// Vorher prüfte das nur die Token-Erneuerung. Ein gesperrtes Konto arbeitete
/// mit seinem Access-Token weiter, bis der ablief - bis zu 65 Minuten
/// (60 Laufzeit plus 5 Toleranz). Jetzt greift die Sperre binnen
/// <see cref="Merkdauer"/>, beim Sperren über die Verwaltung sofort
/// (<see cref="Vergessen"/>).
///
/// Bewusst NICHT die Sperre nach falschen Passwörtern (siehe
/// <see cref="Sperren"/>): Die kann jede:r mit der E-Mail-Adresse auslösen und
/// hätte hier jede laufende Sitzung sofort beendet.
/// </summary>
public class KontoStatus(UserManager<ApplicationUser> userManager, IMemoryCache cache, TimeProvider timeProvider)
{
    /// <summary>Eine Datenbankabfrage je Konto und halber Minute statt je Anfrage.</summary>
    private static readonly TimeSpan Merkdauer = TimeSpan.FromSeconds(30);

    private static string Schluessel(Guid userId) => $"konto-nutzbar:{userId}";

    public async Task<bool> IstNutzbarAsync(Guid userId, CancellationToken ct = default)
    {
        if (cache.TryGetValue(Schluessel(userId), out bool nutzbar))
            return nutzbar;

        var konto = await userManager.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.LockoutEnd })
            .FirstOrDefaultAsync(ct);
        nutzbar = konto is not null && Sperren.Bestimme(konto.LockoutEnd, timeProvider.GetUtcNow()) != Sperrart.Dauerhaft;

        cache.Set(Schluessel(userId), nutzbar, Merkdauer);
        return nutzbar;
    }

    /// <summary>Nach Sperren, Entsperren oder Löschen: sofort neu nachsehen.</summary>
    public void Vergessen(Guid userId) => cache.Remove(Schluessel(userId));
}
