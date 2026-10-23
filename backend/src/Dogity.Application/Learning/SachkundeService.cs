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
                c.ExamQuestionCount,
                // Nur nach Fragenart gezählt (billig, eine Abfrage). Die Prüfung selbst
                // entscheidet mit SachkundePruefung.IstWertbar, das zusätzlich eine
                // richtige Antwort bzw. Begriffe verlangt; die Verwaltung lässt
                // Fragen ohne beides nicht zu, die Zahlen stimmen also überein.
                Auswertbar = c.Questions.Count(q => q.Kind != QuizQuestionKind.FreeText),
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
                    .ToList(),
                PruefungsInfo(c.ExamQuestionCount, c.Auswertbar)))
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

    // ---------- Übungsprüfung ----------

    public async Task<Result<QuizExamDto>> StartExamAsync(string catalogCode, CancellationToken ct = default)
    {
        var katalog = await PruefungsKatalogAsync(catalogCode, ct);
        if (katalog.Fehler is not null)
            return Result<QuizExamDto>.NotFound(katalog.Fehler);

        var fragen = await FragenAbfrage(katalog.Id, section: null).ToListAsync(ct);
        var auswahl = SachkundePruefung.WaehleFragen(fragen, katalog.Anzahl, Random.Shared);
        if (auswahl.Count == 0)
            return Result<QuizExamDto>.NotFound("Für diesen Fragenkatalog gibt es keine Übungsprüfung.");

        var hoechst = auswahl.Sum(SachkundePruefung.HoechstPunkte);

        return Result<QuizExamDto>.Success(new QuizExamDto(
            katalog.Code,
            auswahl.Count,
            hoechst,
            SachkundePruefung.Bestehensgrenze(hoechst),
            SachkundePruefung.BestehensProzent,
            auswahl.Select(ZuPruefungsDto).ToList()));
    }

    public async Task<Result<QuizExamResultDto>> SubmitExamAsync(
        Guid? userId, string catalogCode, IReadOnlyList<QuizExamAnswerDto> answers, CancellationToken ct = default)
    {
        var katalog = await PruefungsKatalogAsync(catalogCode, ct);
        if (katalog.Fehler is not null)
            return Result<QuizExamResultDto>.NotFound(katalog.Fehler);

        // Der Server merkt sich die gezogene Prüfung nicht (kein Zustand, der
        // bei anonymer Nutzung aufräumt werden müsste). Er prüft stattdessen,
        // dass die Abgabe die Form einer Prüfung hat: richtige Anzahl, nur
        // Fragen dieses Katalogs, keine doppelt, nichts Unwertbares.
        //
        // Was sich ohne den Katalog entscheiden lässt, kommt VOR dem Laden: Die
        // Endpunkte sind anonym, und eine leere, überlange oder doppelte Abgabe
        // soll keine Katalogabfrage kosten. Die Prüfungsgröße ist die Obergrenze
        // (weniger wertbare Fragen als verlangt senken sie nur).
        var abgegeben = answers?.Count ?? 0;
        var hoechstens = katalog.Anzahl;
        if (answers is null || abgegeben == 0 || abgegeben > hoechstens)
            return Result<QuizExamResultDto>.Failure(
                $"Die Übungsprüfung hat höchstens {hoechstens} Fragen – es wurden {abgegeben} Antworten abgegeben.");

        if (answers.Select(a => a.QuestionId).Distinct().Count() != answers.Count)
            return Result<QuizExamResultDto>.Failure("Eine Frage wurde doppelt abgegeben.");

        var wertbar = (await FragenAbfrage(katalog.Id, section: null).ToListAsync(ct))
            .Where(SachkundePruefung.IstWertbar)
            .ToDictionary(q => q.Id);

        var erwartet = Math.Min(katalog.Anzahl, wertbar.Count);
        if (abgegeben != erwartet)
            return Result<QuizExamResultDto>.Failure(
                $"Die Übungsprüfung hat {erwartet} Fragen – es wurden {abgegeben} Antworten abgegeben.");

        if (answers.Any(a => !wertbar.ContainsKey(a.QuestionId)))
            return Result<QuizExamResultDto>.Failure("Eine Frage gehört nicht zu dieser Übungsprüfung.");

        var fragenErgebnisse = new List<(QuizQuestion Frage, QuizExamQuestionResultDto Ergebnis)>();
        foreach (var antwort in answers)
        {
            var frage = wertbar[antwort.QuestionId];
            var bewertet = Bewerten(frage, antwort);
            if (bewertet.Fehler is not null)
                return Result<QuizExamResultDto>.Failure(bewertet.Fehler);
            fragenErgebnisse.Add((frage, bewertet.Ergebnis!));
        }

        var punkte = fragenErgebnisse.Sum(f => f.Ergebnis.Points);
        var hoechst = fragenErgebnisse.Sum(f => f.Ergebnis.MaxPoints);
        var richtig = fragenErgebnisse.Count(f => f.Ergebnis.Correct);
        var bestanden = SachkundePruefung.Bestanden(punkte, hoechst);

        var abschnitte = fragenErgebnisse
            .GroupBy(f => new { f.Frage.Section, f.Frage.SectionName })
            .OrderBy(g => g.Key.Section, StringComparer.Ordinal)
            .Select(g => new QuizExamSectionResultDto(
                g.Key.Section, g.Key.SectionName, g.Sum(f => f.Ergebnis.Points), g.Sum(f => f.Ergebnis.MaxPoints)))
            .ToList();

        var gespeichert = false;
        if (userId is { } nutzer)
        {
            var jetzt = clock.GetUtcNow();

            db.QuizExamAttempts.Add(new QuizExamAttempt
            {
                UserId = nutzer,
                CatalogId = katalog.Id,
                SubmittedAt = jetzt,
                Points = punkte,
                MaxPoints = hoechst,
                Passed = bestanden,
                QuestionCount = fragenErgebnisse.Count,
                CorrectCount = richtig,
                SectionResultsJson = SachkundePruefung.SchreibeAbschnitte(abschnitte)
            });

            // Fehlerspeicher: was nicht voll richtig war (auch Unbeantwortetes -
            // gelernt werden muss es ja trotzdem), wirkt wie eine falsche Antwort
            // im Lernmodus: Fach 1, sofort wieder fällig. Richtig Beantwortetes
            // ändert den Lernstand bewusst NICHT - eine Prüfung soll nicht
            // nebenbei Fächer hochstufen.
            var falscheIds = fragenErgebnisse.Where(f => !f.Ergebnis.Correct).Select(f => f.Frage.Id).ToList();
            var stand = await db.QuizMasteries
                .Where(m => m.UserId == nutzer && falscheIds.Contains(m.QuestionId))
                .ToDictionaryAsync(m => m.QuestionId, ct);

            foreach (var id in falscheIds)
            {
                if (!stand.TryGetValue(id, out var mastery))
                {
                    mastery = new QuizMastery { UserId = nutzer, QuestionId = id };
                    db.QuizMasteries.Add(mastery);
                }
                ApplyOutcome(mastery, correct: false, jetzt);
            }

            await db.SaveChangesAsync(ct);
            gespeichert = true;
        }

        return Result<QuizExamResultDto>.Success(new QuizExamResultDto(
            punkte,
            hoechst,
            SachkundePruefung.Bestehensgrenze(hoechst),
            SachkundePruefung.BestehensProzent,
            SachkundePruefung.Prozent(punkte, hoechst),
            bestanden,
            fragenErgebnisse.Count,
            richtig,
            gespeichert,
            abschnitte,
            fragenErgebnisse.Select(f => f.Ergebnis).ToList()));
    }

    public async Task<Result<QuizExamHistoryDto>> GetExamHistoryAsync(
        Guid userId, string catalogCode, CancellationToken ct = default)
    {
        var katalogId = await KatalogIdAsync(catalogCode, ct);
        if (katalogId is null)
            return Result<QuizExamHistoryDto>.NotFound("Fragenkatalog nicht gefunden.");

        var versuche = await db.QuizExamAttempts
            .Where(a => a.UserId == userId && a.CatalogId == katalogId.Value)
            .OrderByDescending(a => a.SubmittedAt)
            .Take(SachkundePruefung.VerlaufLaenge)
            .AsNoTracking()
            .ToListAsync(ct);

        var letzte = versuche.Take(SachkundePruefung.BilanzLaenge).ToList();

        return Result<QuizExamHistoryDto>.Success(new QuizExamHistoryDto(
            catalogCode,
            versuche.Select(a => new QuizExamAttemptDto(
                a.Id, a.SubmittedAt, a.Points, a.MaxPoints, SachkundePruefung.Prozent(a.Points, a.MaxPoints),
                a.Passed, a.QuestionCount, a.CorrectCount, SachkundePruefung.LeseAbschnitte(a.SectionResultsJson))).ToList(),
            letzte.Count(a => a.Passed),
            letzte.Count));
    }

    /// <summary>Die Prüfungsgröße des Katalogs, oder warum es keine Übungsprüfung gibt.</summary>
    private async Task<(Guid Id, string Code, int Anzahl, string? Fehler)> PruefungsKatalogAsync(
        string code, CancellationToken ct)
    {
        var normalisiert = (code ?? string.Empty).Trim().ToUpperInvariant();
        var katalog = await db.QuizCatalogs
            .Where(c => c.Code.ToUpper() == normalisiert)
            .Select(c => new { c.Id, c.Code, c.ExamQuestionCount })
            .FirstOrDefaultAsync(ct);

        if (katalog is null)
            return (Guid.Empty, string.Empty, 0, "Fragenkatalog nicht gefunden.");
        if (katalog.ExamQuestionCount is not > 0)
            return (katalog.Id, katalog.Code, 0, "Für diesen Fragenkatalog gibt es keine Übungsprüfung.");

        return (katalog.Id, katalog.Code, katalog.ExamQuestionCount.Value, null);
    }

    /// <summary>Wertet eine einzelne Frage der Übungsprüfung; Fehler nur bei unpassenden Ids.</summary>
    private static (QuizExamQuestionResultDto? Ergebnis, string? Fehler) Bewerten(
        QuizQuestion frage, QuizExamAnswerDto antwort)
    {
        var hoechst = SachkundePruefung.HoechstPunkte(frage);

        if (frage.Kind == QuizQuestionKind.Assignment)
        {
            var begriffe = frage.Options.Where(o => o.Kind == QuizOptionKind.Term).OrderBy(o => o.SortOrder).ToList();
            var zuordnung = antwort.Assignments;
            if (zuordnung is not null && zuordnung.Keys.Any(id => begriffe.All(b => b.Id != id)))
                return (null, "Ein zugeordneter Begriff gehört nicht zu dieser Frage.");

            var punkteZuordnung = SachkundePruefung.PunkteZuordnung(begriffe, zuordnung);
            return (new QuizExamQuestionResultDto(
                frage.Id, punkteZuordnung == hoechst, punkteZuordnung, hoechst,
                [], begriffe.ToDictionary(b => b.Id, b => b.MatchKey ?? string.Empty)), null);
        }

        var richtige = SachkundePruefung.RichtigeAntworten(frage);
        var angekreuzt = (antwort.SelectedOptionIds ?? []).Distinct().ToList();
        if (angekreuzt.Any(id => frage.Options.All(o => o.Id != id || o.Kind != QuizOptionKind.Answer)))
            return (null, "Eine gewählte Antwort gehört nicht zu dieser Frage.");

        var punkte = SachkundePruefung.PunkteAuswahl(richtige, angekreuzt);
        return (new QuizExamQuestionResultDto(
            frage.Id, punkte == hoechst, punkte, hoechst, richtige, new Dictionary<Guid, string>()), null);
    }

    /// <summary>Die Frage ohne jeden Hinweis auf die Lösung.</summary>
    private static QuizExamQuestionDto ZuPruefungsDto(QuizQuestion frage)
    {
        var zeilen = frage.Options.OrderBy(o => o.SortOrder).ToList();

        return new QuizExamQuestionDto(
            frage.Id,
            frage.Number,
            frage.Section,
            frage.SectionName,
            frage.Kind.ToString(),
            frage.Text,
            frage.ImageName,
            // Reihenfolge der Antworten bleibt die des Katalogs (a/b/c).
            zeilen.Where(o => o.Kind == QuizOptionKind.Answer)
                  .Select(o => new QuizExamOptionDto(o.Id, o.Text, o.ImageName)).ToList(),
            zeilen.Where(o => o.Kind == QuizOptionKind.Term).Select(o => new QuizExamTermDto(o.Id, o.Text)).ToList(),
            Schluessel(zeilen));
    }

    private static QuizExamInfoDto? PruefungsInfo(int? gewuenscht, int auswertbar)
    {
        var anzahl = Math.Min(gewuenscht ?? 0, auswertbar);
        if (anzahl <= 0) return null;

        var hoechst = anzahl * SachkundePruefung.PunkteJeAntwort;
        return new QuizExamInfoDto(
            anzahl, hoechst, SachkundePruefung.Bestehensgrenze(hoechst), SachkundePruefung.BestehensProzent);
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

    /// <summary>
    /// Die wählbaren Schlüssel einer Zuordnung. Ohne Beschriftungen stehen sie in
    /// der Abbildung (A2: die Ziffern 1-5). Dann werden sie aus den Begriffen
    /// abgeleitet - das verrät nichts, weil die Zuordnung eineindeutig ist:
    /// jeder Schlüssel kommt genau einmal vor, gesucht ist die Reihenfolge.
    /// </summary>
    private static List<QuizKeyDto> Schluessel(List<QuizOption> zeilen)
    {
        var beschriftungen = zeilen
            .Where(o => o.Kind == QuizOptionKind.Label && o.MatchKey is not null)
            .Select(o => new QuizKeyDto(o.MatchKey!, o.Text))
            .ToList();

        return beschriftungen.Count > 0
            ? beschriftungen
            : zeilen
                .Where(o => o.Kind == QuizOptionKind.Term)
                .Select(o => o.MatchKey ?? string.Empty)
                .Where(k => !string.IsNullOrEmpty(k))
                .Distinct()
                .OrderBy(k => k, StringComparer.Ordinal)
                .Select(k => new QuizKeyDto(k, null))
                .ToList();
    }

    private static QuizQuestionDto ZuDto(QuizQuestion frage, QuizMastery? stand)
    {
        var zeilen = frage.Options.OrderBy(o => o.SortOrder).ToList();

        var begriffe = zeilen
            .Where(o => o.Kind == QuizOptionKind.Term)
            .Select(o => new QuizTermDto(o.Id, o.Text, o.MatchKey ?? string.Empty))
            .ToList();

        var schluessel = Schluessel(zeilen);

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
