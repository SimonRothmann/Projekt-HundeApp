using Dogity.Application.Abstractions;
using Dogity.Application.Community;
using Dogity.Application.Preferences;
using Dogity.Domain.Geography;
using Dogity.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Planning;

/// <summary>
/// Holt für eine Reihe von Zielen alles, was die Prüfungsreife braucht
/// (Übungen der Prüfungsordnung, Mastery der Hunde, Theorie der ansehenden
/// Person), und ruft die reine Regel (<see cref="Pruefungsreife"/>) auf.
///
/// Die Abfragen laufen EINMAL für alle übergebenen Ziele, nicht je Ziel: Die
/// Zielkarten der Startseite rechnen jedes Ziel mit, und Dutzende kleiner
/// Abfragen je Karte wären auf dem Handy spürbar. Es bleibt bei einer Handvoll
/// Abfragen, ganz gleich wie viele Ziele es sind (die Theorie kommt nur je
/// Katalog einmal dazu).
/// </summary>
public static class PruefungsreifeLader
{
    /// <summary>
    /// Die Prüfungsreife je Ziel-Id; Ziele ohne Prüfungsreife (eigene Ziele,
    /// erreichte, ohne Ordnung) fehlen im Ergebnis.
    /// </summary>
    /// <param name="betrachter">
    /// Wer schaut. Die Theoriezeile bekommt nur, wer den Hund besitzt (oder
    /// mitbesitzt): Der Sachkundestand ist Lerndaten der Person, nicht des Hundes
    /// - betreuende Trainer:innen bekommen ihn nicht, auch nicht über die Ampel.
    /// </param>
    public static async Task<Dictionary<Guid, PruefungsreifeDto>> LadeAsync(
        IApplicationDbContext db,
        IReadOnlyList<Goal> ziele,
        DateOnly heute,
        IReadOnlyDictionary<Guid, List<PausenZeitraum>> pausenJeHund,
        Guid? betrachter,
        CancellationToken ct = default)
    {
        var ergebnis = new Dictionary<Guid, PruefungsreifeDto>();
        var relevant = ziele.Where(Pruefungsreife.Gilt).ToList();
        if (relevant.Count == 0) return ergebnis;

        var ordnungIds = relevant.Select(z => z.RegulationId!.Value).Distinct().ToList();
        var hundIds = relevant.Select(z => z.DogId).Distinct().ToList();

        // Die gültige Fassung je Ordnung - dieselbe Regel wie beim Plan und bei den
        // Höchstpunkten (RegulationVersionQueries).
        var fassungJeOrdnung = (await db.AktuelleFassungenAsync(ordnungIds, heute, ct)).ToDictionary(kv => kv.Key, kv => kv.Value.Id);

        var fassungIds = fassungJeOrdnung.Values.ToList();
        var zeilen = await db.RegulationExercises
            .Where(re => fassungIds.Contains(re.RegulationVersionId))
            .Select(re => new
            {
                re.RegulationVersionId,
                re.ExerciseId,
                re.IsMandatory,
                re.MaxPoints,
                re.SortOrder,
                Name = re.Exercise!.Name,
                NameEn = re.Exercise!.NameEn
            })
            .AsNoTracking()
            .ToListAsync(ct);
        var uebungenJeFassung = zeilen
            .GroupBy(z => z.RegulationVersionId)
            .ToDictionary(g => g.Key, g => g.OrderBy(z => z.SortOrder).ToList());

        // Mastery aller betroffenen Hunde in einem Zug (nur deren Übungen).
        var uebungIds = zeilen.Select(z => z.ExerciseId).Distinct().ToList();
        var mastery = (await db.ExerciseMasteries
                .Where(m => hundIds.Contains(m.DogId) && uebungIds.Contains(m.ExerciseId))
                .AsNoTracking()
                .ToListAsync(ct))
            .ToDictionary(m => (m.DogId, m.ExerciseId));

        var theorie = await TheorienAsync(db, relevant, ordnungIds, hundIds, betrachter, ct);

        foreach (var ziel in relevant)
        {
            if (!fassungJeOrdnung.TryGetValue(ziel.RegulationId!.Value, out var fassungId)
                || !uebungenJeFassung.TryGetValue(fassungId, out var uebungen))
                continue;

            var eingabe = uebungen
                .Select(u =>
                {
                    mastery.TryGetValue((ziel.DogId, u.ExerciseId), out var m);
                    return new ReifeUebung(
                        u.ExerciseId, u.Name, u.NameEn, u.IsMandatory, u.MaxPoints,
                        m?.SessionCount ?? 0,
                        m?.Box ?? 1,
                        m?.RecentAvgRating ?? 0,
                        m?.LastTrainedAt is { } zuletzt ? Vereinszeit.Tag(zuletzt) : null);
                })
                .ToList();

            var reife = Pruefungsreife.Berechne(
                eingabe, heute, ziel.TargetDate,
                pausenJeHund.GetValueOrDefault(ziel.DogId) ?? [],
                theorie.GetValueOrDefault((ziel.DogId, ziel.RegulationId!.Value)));
            if (reife is not null) ergebnis[ziel.Id] = reife;
        }

        return ergebnis;
    }

    /// <summary>
    /// Die Theoriezeile je (Hund, Ordnung) für den Betrachter - nur dort, wo die
    /// Ordnung einen Theorie-Katalog hat UND der Betrachter den Hund besitzt.
    /// </summary>
    private static async Task<Dictionary<(Guid DogId, Guid RegulationId), TheorieZeileDto>> TheorienAsync(
        IApplicationDbContext db, List<Goal> ziele, List<Guid> ordnungIds, List<Guid> hundIds, Guid? betrachter, CancellationToken ct)
    {
        var ergebnis = new Dictionary<(Guid, Guid), TheorieZeileDto>();
        if (betrachter is not { } nutzer) return ergebnis;

        // Die Theorie fordert nur, wer die Sachkunde auch nutzen kann: Sie ist ein
        // deutsches Angebot (Country.Vorgabe) und lässt sich abschalten. Wer sie
        // nicht hat (Österreich, Modul aus), könnte nie eine Übungsprüfung ablegen -
        // seine Ampel bliebe sonst dauerhaft höchstens gelb.
        if (!await SachkundeNutzbarAsync(db, nutzer, ct)) return ergebnis;

        var codes = await db.Regulations
            .Where(r => ordnungIds.Contains(r.Id) && r.TheoryCatalogCode != null)
            .Select(r => new { r.Id, Code = r.TheoryCatalogCode! })
            .AsNoTracking()
            .ToListAsync(ct);
        if (codes.Count == 0) return ergebnis;

        // Nur eigene Hunde: Wer den Hund bloß betreut, sieht die Theorie nicht.
        var eigeneHunde = (await db.DogOwners
                .Where(o => o.UserId == nutzer && hundIds.Contains(o.DogId))
                .Select(o => o.DogId)
                .ToListAsync(ct))
            .ToHashSet();
        if (eigeneHunde.Count == 0) return ergebnis;

        var zeileJeCode = new Dictionary<string, TheorieZeileDto?>();
        foreach (var code in codes.Select(c => c.Code).Distinct())
            zeileJeCode[code] = await TheorieAsync(db, nutzer, code, ct);

        var codeJeOrdnung = codes.ToDictionary(c => c.Id, c => c.Code);
        foreach (var ziel in ziele.Where(z => eigeneHunde.Contains(z.DogId)))
        {
            if (codeJeOrdnung.TryGetValue(ziel.RegulationId!.Value, out var code) && zeileJeCode[code] is { } zeile)
                ergebnis[(ziel.DogId, ziel.RegulationId.Value)] = zeile;
        }
        return ergebnis;
    }

    /// <summary>
    /// Ob die Person das Sachkunde-Modul überhaupt hat - dieselbe Regel wie
    /// <c>moduleEnabled</c> im Frontend: nicht abgeschaltet UND Geltungsbereich
    /// Deutschland (ohne gespeicherte Wahl gilt die Vorgabe).
    /// </summary>
    private static async Task<bool> SachkundeNutzbarAsync(IApplicationDbContext db, Guid nutzer, CancellationToken ct)
    {
        var einstellung = await db.UserPreferences
            .Where(p => p.UserId == nutzer)
            .Select(p => new { p.Country, Aus = p.DisabledModules.Any(m => m.ModuleKey == Modules.Sachkunde) })
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
        if (einstellung is null) return true;

        var land = string.IsNullOrWhiteSpace(einstellung.Country) ? Country.Vorgabe : einstellung.Country.Trim().ToUpperInvariant();
        return !einstellung.Aus && land == Country.Vorgabe;
    }

    /// <summary>
    /// Die Theoriezeile eines Nutzers zu einem Katalog: Stand aus den letzten
    /// Übungsprüfungen im Erwachsenenkatalog und - zählt gleich - in der
    /// Jugendfassung desselben Katalogs (gleicher Code bis auf die Endung, z. B.
    /// SWHV-BHVT-ERW und SWHV-BHVT-JGD). Null, wenn der Katalog hier nicht
    /// existiert oder keine Übungsprüfung hat: dann gibt es keine Theorie zu fordern.
    /// </summary>
    private static async Task<TheorieZeileDto?> TheorieAsync(IApplicationDbContext db, Guid nutzer, string code, CancellationToken ct)
    {
        var strich = code.LastIndexOf('-');
        var stamm = strich > 0 ? code[..(strich + 1)] : code;

        var kataloge = await db.QuizCatalogs
            .Where(c => c.ExamQuestionCount != null && (c.Code == code || c.Code.StartsWith(stamm)))
            .Select(c => new { c.Id, c.Code })
            .AsNoTracking()
            .ToListAsync(ct);
        if (kataloge.Count == 0) return null;
        var katalogIds = kataloge.Select(k => k.Id).ToList();

        var letzte = await db.QuizExamAttempts
            .Where(a => a.UserId == nutzer && katalogIds.Contains(a.CatalogId))
            .OrderByDescending(a => a.SubmittedAt)
            .Take(Pruefungsreife.TheorieBilanzLaenge)
            .Select(a => new { a.Passed, a.CatalogId })
            .AsNoTracking()
            .ToListAsync(ct);

        var bestanden = letzte.Select(a => a.Passed).ToList();
        var fehler = await db.QuizMasteries
            .CountAsync(m => m.UserId == nutzer && m.LastAnsweredAt != null && !m.LastWasCorrect
                && katalogIds.Contains(m.Question!.CatalogId), ct);

        // Link: der Katalog der letzten Übungsprüfung (wer als Jugendliche:r geübt
        // hat, soll nicht beim Erwachsenenkatalog landen), sonst der genannte.
        var linkCode = letzte.Count > 0
            ? kataloge.First(k => k.Id == letzte[0].CatalogId).Code
            : kataloge.FirstOrDefault(k => k.Code == code)?.Code ?? kataloge[0].Code;

        return new TheorieZeileDto(
            Pruefungsreife.TheorieZustand(bestanden), linkCode, bestanden.Count, bestanden.Count(b => b), fehler);
    }
}
