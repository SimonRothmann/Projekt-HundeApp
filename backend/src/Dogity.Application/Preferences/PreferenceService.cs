using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Preferences;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Preferences;

/// <inheritdoc />
public class PreferenceService(IApplicationDbContext db) : IPreferenceService
{
    public async Task<Result<UserPreferenceDto>> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var eintrag = await db.UserPreferences
            .AsNoTracking()
            .Include(p => p.DisabledModules)
            .Include(p => p.Sports)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        // Ohne Zeile gilt die Vorgabe: alles an, keine Einschränkung. Es wird
        // bewusst NICHTS angelegt - wer nie etwas eingestellt hat, braucht
        // auch keine Zeile in der Datenbank.
        return Result<UserPreferenceDto>.Success(eintrag is null
            ? new UserPreferenceDto(null, null, [], [])
            : new UserPreferenceDto(
                eintrag.Locale,
                eintrag.Country,
                eintrag.DisabledModules.Select(m => m.ModuleKey).ToList(),
                eintrag.Sports.Select(s => s.SportId).ToList()));
    }

    public async Task<Result> UpdateModulesAsync(Guid userId, UpdateModulesRequest request, CancellationToken ct = default)
    {
        var eintrag = await LadenOderAnlegenAsync(userId, ct);

        // Unbekannte Schlüssel still verwerfen statt den ganzen Aufruf
        // abzulehnen: Ein älterer Client, der ein inzwischen entferntes Modul
        // mitschickt, soll nicht scheitern.
        var gewuenscht = request.DisabledModules
            .Where(k => Modules.Bekannt.Contains(k))
            .Distinct()
            .ToHashSet();

        db.UserDisabledModules.RemoveRange(
            eintrag.DisabledModules.Where(m => !gewuenscht.Contains(m.ModuleKey)));

        // Ausdrücklich über das DbSet anlegen, nicht nur an die Navigationsliste
        // hängen: Die Id wird im Client vergeben (siehe Entity), und an einem
        // bereits gespeicherten Elternobjekt hält EF ein Kind mit gesetzter Id
        // für vorhanden - es erzeugte ein UPDATE statt eines INSERT, das dann
        // null Zeilen traf und mit einem Nebenläufigkeitsfehler abbrach.
        foreach (var schluessel in gewuenscht.Where(k => eintrag.DisabledModules.All(m => m.ModuleKey != k)))
            db.UserDisabledModules.Add(new UserDisabledModule { UserPreferenceId = eintrag.Id, ModuleKey = schluessel });

        eintrag.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UpdateSportsAsync(Guid userId, UpdateSportsRequest request, CancellationToken ct = default)
    {
        var gueltig = await GueltigeSportartenAsync(userId, request.SportIds, ct);
        var eintrag = await LadenOderAnlegenAsync(userId, ct);

        db.UserSportSelections.RemoveRange(eintrag.Sports.Where(s => !gueltig.Contains(s.SportId)));
        // Ausdrücklich über das DbSet - siehe UpdateModulesAsync.
        foreach (var id in gueltig.Where(id => eintrag.Sports.All(s => s.SportId != id)))
            db.UserSportSelections.Add(new UserSportSelection { UserPreferenceId = eintrag.Id, SportId = id });

        eintrag.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UpdateLocaleAsync(Guid userId, UpdateLocaleRequest request, CancellationToken ct = default)
    {
        var sprache = request.Locale?.Trim();
        if (sprache is { Length: > 10 }) return Result.Failure("Sprachkürzel ist zu lang.");

        var eintrag = await LadenOderAnlegenAsync(userId, ct);
        eintrag.Locale = string.IsNullOrWhiteSpace(sprache) ? null : sprache;
        eintrag.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UpdateCountryAsync(Guid userId, UpdateCountryRequest request, CancellationToken ct = default)
    {
        var land = request.Country?.Trim().ToUpperInvariant();

        // Leer = zurück auf die Vorgabe. Ein unbekanntes Kürzel dagegen wird
        // abgelehnt und nicht stillschweigend verworfen: Wer ein Land wählt,
        // das es in der Liste nicht gibt, bekäme sonst einen leeren Katalog
        // und keine Erklärung dafür.
        if (!string.IsNullOrEmpty(land))
        {
            if (land.Length != 2)
                return Result.Failure("Länderkürzel muss zwei Buchstaben haben (ISO 3166-1 alpha-2).");
            if (!await db.Countries.AnyAsync(c => c.Code == land, ct))
                return Result.Failure("Dieses Land steht nicht zur Auswahl.");
        }

        var eintrag = await LadenOderAnlegenAsync(userId, ct);
        eintrag.Country = string.IsNullOrEmpty(land) ? null : land;
        eintrag.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<Guid>>> GetEffectiveDogSportsAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        if (!await db.HasDogAccessAsync(userId, dogId, ct))
            return Result<IReadOnlyList<Guid>>.NotFound("Hund nicht gefunden.");

        var hund = await db.Dogs
            .AsNoTracking()
            .Where(d => d.Id == dogId)
            .Select(d => new { d.UsesOwnSports })
            .FirstOrDefaultAsync(ct);

        if (hund is null) return Result<IReadOnlyList<Guid>>.NotFound("Hund nicht gefunden.");

        // Die Vererbungsregel steht NUR hier. Sie im Frontend nachzubauen
        // hieße, sie zweimal zu pflegen - und die beiden Fassungen liefen
        // beim ersten Sonderfall auseinander.
        if (hund.UsesOwnSports)
        {
            var eigene = await db.DogSportSelections
                .Where(s => s.DogId == dogId)
                .Select(s => s.SportId)
                .ToListAsync(ct);
            return Result<IReadOnlyList<Guid>>.Success(eigene);
        }

        var vomMenschen = await db.UserSportSelections
            .Where(s => s.UserPreference!.UserId == userId)
            .Select(s => s.SportId)
            .ToListAsync(ct);

        return Result<IReadOnlyList<Guid>>.Success(vomMenschen);
    }

    public async Task<Result> UpdateDogSportsAsync(Guid userId, Guid dogId, UpdateDogSportsRequest request, CancellationToken ct = default)
    {
        // Ändern darf nur, wer den Hund besitzt - ein zugewiesener Trainer
        // sieht ihn zwar, verwaltet ihn aber nicht (wie bei DogService).
        var istBesitzer = await db.DogOwners.AnyAsync(o => o.DogId == dogId && o.UserId == userId, ct);
        if (!istBesitzer) return Result.NotFound("Hund nicht gefunden.");

        var hund = await db.Dogs.FirstOrDefaultAsync(d => d.Id == dogId, ct);
        if (hund is null) return Result.NotFound("Hund nicht gefunden.");

        var gueltig = await GueltigeSportartenAsync(userId, request.SportIds, ct);
        var vorhanden = await db.DogSportSelections.Where(s => s.DogId == dogId).ToListAsync(ct);

        db.DogSportSelections.RemoveRange(vorhanden.Where(s => !gueltig.Contains(s.SportId)));
        foreach (var id in gueltig.Where(id => vorhanden.All(s => s.SportId != id)))
            db.DogSportSelections.Add(new DogSportSelection { DogId = dogId, SportId = id });

        hund.UsesOwnSports = request.UsesOwnSports;
        hund.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<UserPreference> LadenOderAnlegenAsync(Guid userId, CancellationToken ct)
    {
        var eintrag = await db.UserPreferences
            .Include(p => p.DisabledModules)
            .Include(p => p.Sports)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (eintrag is not null) return eintrag;

        eintrag = new UserPreference { UserId = userId };
        db.UserPreferences.Add(eintrag);
        return eintrag;
    }

    /// <summary>
    /// Filtert auf Sportarten, die es gibt und die der Nutzer sehen darf.
    ///
    /// Ohne diese Prüfung ließe sich über die Einstellung ermitteln, welche
    /// vereinseigenen Sportarten fremde Vereine führen: Wer eine geratene Id
    /// speichert und sie zurückbekommt, weiß, dass es sie gibt.
    /// </summary>
    private async Task<HashSet<Guid>> GueltigeSportartenAsync(Guid userId, IReadOnlyList<Guid> gewuenscht, CancellationToken ct)
    {
        if (gewuenscht.Count == 0) return [];

        var sichtbareVereine = await db.GetVisibleClubIdsAsync(userId, ct);
        var ids = gewuenscht.Distinct().ToList();

        return (await db.Sports
            .Where(s => ids.Contains(s.Id))
            .Where(s => s.ClubId == null || sichtbareVereine.Contains(s.ClubId.Value))
            .Select(s => s.Id)
            .ToListAsync(ct))
            .ToHashSet();
    }
}
