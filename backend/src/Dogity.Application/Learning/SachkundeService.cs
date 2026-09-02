using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Learning;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Learning;

/// <inheritdoc />
public class SachkundeService(IApplicationDbContext db, TimeProvider clock) : ISachkundeService
{
    /// <summary>
    /// Wiedervorlage je Leitner-Fach in Tagen. Kürzer als die Intervalle des
    /// Trainingsplans ([2,4,7,14,28]): eine Übung baut man über Monate auf, die
    /// Sachkunde lernt man in den Wochen vor der Prüfung. Fach 1 kommt am
    /// nächsten Tag wieder, Fach 5 nach drei Wochen.
    /// </summary>
    private static readonly int[] IntervalDaysByBox = [1, 2, 4, 9, 21];

    /// <summary>Ab diesem Fach gilt eine Frage als gekonnt.</summary>
    private const int MasteredBox = 4;

    /// <summary>Wie viele Fragen eine Lernrunde höchstens vorlegt.</summary>
    private const int MaxSessionSize = 50;

    // ---------- Lesen ----------

    public async Task<Result<IReadOnlyList<QuizCatalogDto>>> GetCatalogsAsync(CancellationToken ct = default)
    {
        var kataloge = await db.QuizCatalogs
            .OrderBy(c => c.SortOrder)
            .Select(c => new
            {
                c.Id, c.Code, c.Name, c.Description, c.Publisher, c.SourceUrl, c.Edition, c.Audience,
                Sections = c.Questions
                    .GroupBy(q => new { q.Section, q.SectionName })
                    .Select(g => new { g.Key.Section, g.Key.SectionName, Count = g.Count() })
                    .ToList()
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var dtos = kataloge
            .Select(c => new QuizCatalogDto(
                c.Id, c.Code, c.Name, c.Description, c.Publisher, c.SourceUrl, c.Edition,
                c.Audience.ToString(),
                c.Sections.Sum(s => s.Count),
                c.Sections
                    .OrderBy(s => s.Section, StringComparer.Ordinal)
                    .Select(s => new QuizSectionDto(s.Section, s.SectionName, s.Count))
                    .ToList()))
            .ToList();

        return Result<IReadOnlyList<QuizCatalogDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<QuizQuestionDto>>> GetQuestionsAsync(
        string catalogCode, string? section, Guid? userId, CancellationToken ct = default)
    {
        var katalogId = await KatalogIdAsync(catalogCode, ct);
        if (katalogId is null)
            return Result<IReadOnlyList<QuizQuestionDto>>.NotFound("Fragenkatalog nicht gefunden.");

        var fragen = await FragenAbfrage(katalogId.Value, section)
            .OrderBy(q => q.SortOrder)
            .ToListAsync(ct);

        var stand = userId is null ? [] : await LernstandAsync(userId.Value, katalogId.Value, ct);

        return Result<IReadOnlyList<QuizQuestionDto>>.Success(
            fragen.Select(q => ZuDto(q, stand.GetValueOrDefault(q.Id))).ToList());
    }

    public async Task<Result<QuizSessionDto>> GetSessionAsync(
        Guid userId, string catalogCode, string mode, int limit, CancellationToken ct = default)
    {
        var katalogId = await KatalogIdAsync(catalogCode, ct);
        if (katalogId is null)
            return Result<QuizSessionDto>.NotFound("Fragenkatalog nicht gefunden.");

        var normalisiert = (mode ?? "learn").Trim().ToLowerInvariant();
        if (normalisiert is not ("learn" or "mistakes" or "all"))
            return Result<QuizSessionDto>.Failure("Unbekannter Lernmodus. Erlaubt sind learn, mistakes und all.");

        var anzahl = Math.Clamp(limit <= 0 ? 20 : limit, 1, MaxSessionSize);

        var alle = await FragenAbfrage(katalogId.Value, section: null).ToListAsync(ct);
        var stand = await LernstandAsync(userId, katalogId.Value, ct);
        var jetzt = clock.GetUtcNow();

        var auswahl = normalisiert switch
        {
            "mistakes" => alle
                .Where(q => stand.TryGetValue(q.Id, out var m) && Falsch(m))
                .OrderBy(q => stand[q.Id].LastAnsweredAt)
                .ToList(),

            "all" => alle.OrderBy(q => q.SortOrder).ToList(),

            // Lernmodus: erst was fällig ist (am längsten überfällig zuerst),
            // dann was noch nie dran war. Beides zusammen ergibt den Ablauf,
            // den man von den Führerschein-Trainern kennt: neue Fragen kommen
            // nach, falsch beantwortete drängeln sich dazwischen.
            _ => alle
                .Where(q => !stand.TryGetValue(q.Id, out var m) || m.DueAt is null || m.DueAt <= jetzt)
                .OrderBy(q => stand.TryGetValue(q.Id, out var m) && Beantwortet(m) ? 0 : 1)
                .ThenBy(q => stand.TryGetValue(q.Id, out var m) ? m.DueAt : null)
                .ThenBy(q => q.SortOrder)
                .ToList()
        };

        // "Runde durch": im Lernmodus ist gerade nichts fällig und nichts mehr
        // offen. Das ist der Moment, in dem die Oberfläche "von vorne anfangen"
        // anbietet - und nicht etwa ein leerer Bildschirm.
        var rundeDurch = normalisiert == "learn" && auswahl.Count == 0;

        var fortschritt = Fortschritt(catalogCode, alle, stand, jetzt);

        return Result<QuizSessionDto>.Success(new QuizSessionDto(
            catalogCode,
            normalisiert,
            auswahl.Take(anzahl).Select(q => ZuDto(q, stand.GetValueOrDefault(q.Id))).ToList(),
            fortschritt,
            rundeDurch));
    }

    public async Task<Result<QuizProgressDto>> GetProgressAsync(
        Guid userId, string catalogCode, CancellationToken ct = default)
    {
        var katalogId = await KatalogIdAsync(catalogCode, ct);
        if (katalogId is null)
            return Result<QuizProgressDto>.NotFound("Fragenkatalog nicht gefunden.");

        var alle = await FragenAbfrage(katalogId.Value, section: null).ToListAsync(ct);
        var stand = await LernstandAsync(userId, katalogId.Value, ct);

        return Result<QuizProgressDto>.Success(Fortschritt(catalogCode, alle, stand, clock.GetUtcNow()));
    }

    // ---------- Schreiben ----------

    public async Task<Result<QuizAnswerResultDto>> SubmitAnswerAsync(
        Guid userId, Guid questionId, IReadOnlyList<Guid>? selectedOptionIds, bool? selfAssessedCorrect,
        IReadOnlyDictionary<Guid, string>? assignments, CancellationToken ct = default)
    {
        var frage = await db.QuizQuestions
            .Include(q => q.Options)
            .FirstOrDefaultAsync(q => q.Id == questionId, ct);

        if (frage is null)
            return Result<QuizAnswerResultDto>.NotFound("Frage nicht gefunden.");

        var richtigeIds = frage.Options.Where(o => o.IsCorrect).Select(o => o.Id).OrderBy(id => id).ToList();
        var begriffe = frage.Options.Where(o => o.Kind == QuizOptionKind.Term).ToList();
        var begriffErgebnisse = new Dictionary<Guid, bool>();

        bool richtig;
        if (frage.Kind == QuizQuestionKind.Assignment && begriffe.Count > 0)
        {
            // Eine Zuordnung wird zugeordnet, nicht selbst eingeschätzt: jeder
            // Begriff bekommt einen Schlüssel, und die Zuordnung stimmt nur,
            // wenn ALLE stimmen. (Vorher lag hier nur eine Selbsteinschätzung -
            // die Aufgabe war in der Oberfläche gar nicht lösbar.)
            if (assignments is null || assignments.Count == 0)
                return Result<QuizAnswerResultDto>.Failure("Bitte allen Begriffen etwas zuordnen.");

            var fremd = assignments.Keys.Where(id => begriffe.All(b => b.Id != id)).ToList();
            if (fremd.Count > 0)
                return Result<QuizAnswerResultDto>.Failure("Ein zugeordneter Begriff gehört nicht zu dieser Frage.");

            if (begriffe.Any(b => !assignments.ContainsKey(b.Id)))
                return Result<QuizAnswerResultDto>.Failure("Bitte allen Begriffen etwas zuordnen.");

            foreach (var begriff in begriffe)
                begriffErgebnisse[begriff.Id] =
                    string.Equals(assignments[begriff.Id]?.Trim(), begriff.MatchKey, StringComparison.OrdinalIgnoreCase);

            richtig = begriffErgebnisse.Values.All(x => x);
        }
        else if (frage.Kind is QuizQuestionKind.Assignment or QuizQuestionKind.FreeText)
        {
            // Offene Fragen prüft niemand automatisch - hier zählt, was der
            // Lernende selbst sagt. Ohne Angabe ist die Antwort unbrauchbar,
            // deshalb ein Eingabefehler und kein stilles "falsch".
            if (selfAssessedCorrect is null)
                return Result<QuizAnswerResultDto>.Failure(
                    "Für Freitextfragen wird die Selbsteinschätzung erwartet.");
            richtig = selfAssessedCorrect.Value;
        }
        else
        {
            // Bei Auswahlfragen entscheidet der Server. Die Antwort muss die
            // richtigen Optionen genau treffen: eine zusätzlich angekreuzte
            // falsche Antwort ist ein Fehler, nicht "fast richtig".
            var gewaehlt = (selectedOptionIds ?? []).Distinct().OrderBy(id => id).ToList();
            if (gewaehlt.Count == 0)
                return Result<QuizAnswerResultDto>.Failure("Bitte eine Antwort auswählen.");
            if (gewaehlt.Any(id => frage.Options.All(o => o.Id != id)))
                return Result<QuizAnswerResultDto>.Failure("Eine gewählte Antwort gehört nicht zu dieser Frage.");

            richtig = gewaehlt.SequenceEqual(richtigeIds);
        }

        var mastery = await db.QuizMasteries
            .FirstOrDefaultAsync(m => m.UserId == userId && m.QuestionId == questionId, ct);

        if (mastery is null)
        {
            mastery = new QuizMastery { UserId = userId, QuestionId = questionId };
            db.QuizMasteries.Add(mastery);
        }

        ApplyOutcome(mastery, richtig, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);

        // Den Stand gleich mitschicken: sonst müsste die Oberfläche ihn
        // nachladen, und genau das tat sie nicht - der Balken stand die ganze
        // Runde still.
        var katalogId = frage.CatalogId;
        var alle = await FragenAbfrage(katalogId, section: null).ToListAsync(ct);
        var code = await db.QuizCatalogs.Where(c => c.Id == katalogId).Select(c => c.Code).FirstOrDefaultAsync(ct);
        var fortschritt = Fortschritt(
            code ?? string.Empty, alle, await LernstandAsync(userId, katalogId, ct), clock.GetUtcNow());

        return Result<QuizAnswerResultDto>.Success(
            new QuizAnswerResultDto(richtig, mastery.Box, mastery.DueAt, richtigeIds, begriffErgebnisse, fortschritt));
    }

    public async Task<Result> ResetAsync(Guid userId, string catalogCode, CancellationToken ct = default)
    {
        var katalogId = await KatalogIdAsync(catalogCode, ct);
        if (katalogId is null)
            return Result.NotFound("Fragenkatalog nicht gefunden.");

        var stand = await db.QuizMasteries
            .Where(m => m.UserId == userId && m.Question!.CatalogId == katalogId.Value)
            .ToListAsync(ct);

        // Zurücksetzen statt löschen. Zum einen bleibt der eindeutige Index auf
        // (UserId, QuestionId) damit außer Gefahr - weich gelöschte Zeilen
        // stehen ihm im Weg, sobald dieselbe Frage wieder beantwortet wird.
        // Zum anderen ist "von vorne" ein Zustand, kein Löschvorgang.
        foreach (var eintrag in stand)
        {
            eintrag.Box = 1;
            eintrag.DueAt = null;
            eintrag.LastAnsweredAt = null;
            eintrag.CorrectCount = 0;
            eintrag.WrongCount = 0;
            eintrag.LastWasCorrect = false;
            eintrag.UpdatedAt = clock.GetUtcNow();
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// Leitner-Schritt für eine beantwortete Frage.
    ///
    /// Richtig hebt um ein Fach, falsch setzt auf Fach 1 zurück - nicht nur um
    /// eines herunter wie beim Trainingsplan. Eine Übung, die heute schlechter
    /// lief, ist deshalb nicht verlernt; eine falsch beantwortete Frage war
    /// dagegen schlicht nicht gewusst und muss von vorne sitzen.
    ///
    /// Falsch beantwortet heißt außerdem: sofort wieder fällig. Zusammen mit
    /// der Sortierung im Lernmodus kommt die Frage dadurch in derselben Runde
    /// erneut - das ist der Ablauf, den man von den Führerschein-Trainern kennt.
    /// </summary>
    public static void ApplyOutcome(QuizMastery m, bool correct, DateTimeOffset now)
    {
        if (correct)
        {
            m.Box = Math.Min(5, m.Box + 1);
            m.CorrectCount += 1;
            m.DueAt = now.AddDays(IntervalDaysByBox[m.Box - 1]);
        }
        else
        {
            m.Box = 1;
            m.WrongCount += 1;
            m.DueAt = now;
        }

        m.LastWasCorrect = correct;
        m.LastAnsweredAt = now;
        m.UpdatedAt = now;
    }

    // ---------- Hilfen ----------

    /// <summary>
    /// Ob die Frage überhaupt schon beantwortet wurde.
    ///
    /// Nicht am Vorhandensein der Zeile ablesbar: "von vorne anfangen" setzt
    /// den Lernstand zurück, statt ihn zu löschen (siehe <see cref="ResetAsync"/>),
    /// die Zeile bleibt also stehen. Wer nur auf die Zeile schaut, meldet nach
    /// einem Neustart weiter "72 von 72 beantwortet".
    /// </summary>
    private static bool Beantwortet(QuizMastery m) => m.LastAnsweredAt is not null;

    /// <summary>
    /// Ob die Frage im Fehlerspeicher steht: beantwortet, und zuletzt falsch.
    /// Der zweite Teil allein reicht nicht - <c>LastWasCorrect</c> ist auch bei
    /// einer nie beantworteten oder zurückgesetzten Frage <c>false</c>, und dann
    /// läge der ganze Katalog im Fehlerspeicher.
    /// </summary>
    private static bool Falsch(QuizMastery m) => Beantwortet(m) && !m.LastWasCorrect;

    private async Task<Guid?> KatalogIdAsync(string code, CancellationToken ct)
    {
        var normalisiert = (code ?? string.Empty).Trim().ToUpperInvariant();
        var treffer = await db.QuizCatalogs
            .Where(c => c.Code.ToUpper() == normalisiert)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);
        return treffer;
    }

    private IQueryable<QuizQuestion> FragenAbfrage(Guid katalogId, string? section)
    {
        var abfrage = db.QuizQuestions
            .Include(q => q.Options)
            .Where(q => q.CatalogId == katalogId);

        if (!string.IsNullOrWhiteSpace(section))
        {
            var normalisiert = section.Trim().ToUpperInvariant();
            abfrage = abfrage.Where(q => q.Section.ToUpper() == normalisiert);
        }

        return abfrage.AsNoTracking();
    }

    private async Task<Dictionary<Guid, QuizMastery>> LernstandAsync(Guid userId, Guid katalogId, CancellationToken ct) =>
        await db.QuizMasteries
            .Where(m => m.UserId == userId && m.Question!.CatalogId == katalogId)
            .AsNoTracking()
            .ToDictionaryAsync(m => m.QuestionId, ct);

    private QuizProgressDto Fortschritt(
        string catalogCode, List<QuizQuestion> fragen, Dictionary<Guid, QuizMastery> stand, DateTimeOffset jetzt)
    {
        int Beantwortet(IEnumerable<QuizQuestion> menge) =>
            menge.Count(q => stand.TryGetValue(q.Id, out var m) && SachkundeService.Beantwortet(m));
        int Richtig(IEnumerable<QuizQuestion> menge) =>
            menge.Count(q => stand.TryGetValue(q.Id, out var m) && SachkundeService.Beantwortet(m) && m.LastWasCorrect);
        int Gekonnt(IEnumerable<QuizQuestion> menge) =>
            menge.Count(q => stand.TryGetValue(q.Id, out var m) && m.Box >= MasteredBox && m.LastWasCorrect);
        int ImFehlerspeicher(IEnumerable<QuizQuestion> menge) =>
            menge.Count(q => stand.TryGetValue(q.Id, out var m) && Falsch(m));

        var abschnitte = fragen
            .GroupBy(q => new { q.Section, q.SectionName })
            .OrderBy(g => g.Key.Section, StringComparer.Ordinal)
            .Select(g => new QuizSectionProgressDto(
                g.Key.Section, g.Key.SectionName, g.Count(), Beantwortet(g), Richtig(g), Gekonnt(g),
                ImFehlerspeicher(g)))
            .ToList();

        var gesamt = fragen.Count;
        var richtig = Richtig(fragen);
        var gekonnt = Gekonnt(fragen);

        return new QuizProgressDto(
            catalogCode,
            gesamt,
            Beantwortet(fragen),
            richtig,
            gekonnt,
            ImFehlerspeicher(fragen),
            fragen.Count(q => stand.TryGetValue(q.Id, out var m) && m.DueAt is not null && m.DueAt <= jetzt),
            fragen.Count(q => !stand.TryGetValue(q.Id, out var m) || !SachkundeService.Beantwortet(m)),
            gesamt == 0 ? 0 : Math.Round(richtig * 100.0 / gesamt, 1),
            gesamt == 0 ? 0 : Math.Round(gekonnt * 100.0 / gesamt, 1),
            abschnitte);
    }

    private static QuizQuestionDto ZuDto(QuizQuestion frage, QuizMastery? stand)
    {
        var zeilen = frage.Options.OrderBy(o => o.SortOrder).ToList();

        var begriffe = zeilen
            .Where(o => o.Kind == QuizOptionKind.Term)
            .Select(o => new QuizTermDto(o.Id, o.Text, o.MatchKey ?? string.Empty))
            .ToList();

        var beschriftungen = zeilen
            .Where(o => o.Kind == QuizOptionKind.Label && o.MatchKey is not null)
            .Select(o => new QuizKeyDto(o.MatchKey!, o.Text))
            .ToList();

        // Ohne Beschriftungen stehen die Schlüssel in der Abbildung (A2: die
        // Ziffern 1-5). Dann werden sie aus den Begriffen abgeleitet - das
        // verrät nichts, weil die Zuordnung eineindeutig ist: jeder Schlüssel
        // kommt genau einmal vor, gesucht ist die Reihenfolge.
        var schluessel = beschriftungen.Count > 0
            ? beschriftungen
            : begriffe
                .Select(b => b.SolutionKey)
                .Where(k => !string.IsNullOrEmpty(k))
                .Distinct()
                .OrderBy(k => k, StringComparer.Ordinal)
                .Select(k => new QuizKeyDto(k, null))
                .ToList();

        return new QuizQuestionDto(
            frage.Id,
            frage.Number,
            frage.Section,
            frage.SectionName,
            frage.Kind.ToString(),
            frage.Text,
            frage.ImageName,
            frage.SampleSolution,
            zeilen.Where(o => o.Kind == QuizOptionKind.Answer)
                  .Select(o => new QuizOptionDto(o.Id, o.Text, o.IsCorrect, o.ImageName)).ToList(),
            begriffe,
            schluessel,
            stand is null
                ? null
                : new QuizQuestionStateDto(stand.Box, stand.LastWasCorrect, stand.CorrectCount, stand.WrongCount, stand.DueAt));
    }
}
