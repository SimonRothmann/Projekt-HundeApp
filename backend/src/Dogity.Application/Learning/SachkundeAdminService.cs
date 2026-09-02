using System.Text.RegularExpressions;
using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Learning;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Learning;

/// <inheritdoc />
public partial class SachkundeAdminService(IApplicationDbContext db, TimeProvider clock) : ISachkundeAdminService
{
    public async Task<Result<IReadOnlyList<AdminQuizQuestionDto>>> GetQuestionsAsync(
        string? catalogCode, string? section, string? search, bool onlyEdited, bool onlyFlagged,
        CancellationToken ct = default)
    {
        var abfrage = db.QuizQuestions.Include(q => q.Options).Include(q => q.Catalog).AsQueryable();

        if (!string.IsNullOrWhiteSpace(catalogCode))
        {
            var code = catalogCode.Trim().ToUpperInvariant();
            abfrage = abfrage.Where(q => q.Catalog!.Code.ToUpper() == code);
        }

        if (!string.IsNullOrWhiteSpace(section))
        {
            var komplex = section.Trim().ToUpperInvariant();
            abfrage = abfrage.Where(q => q.Section.ToUpper() == komplex);
        }

        if (onlyEdited)
            abfrage = abfrage.Where(q => q.EditedAt != null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Über Frage, Musterlösung UND Antworttexte - wer einen Tippfehler
            // sucht, weiß meist nur, wie das falsche Wort aussah, nicht wo es
            // stand.
            // Bewusst ToUpper statt EF.Functions.ILike: ILike gehört Npgsql, und
            // Application kennt keinen Datenbankanbieter (siehe ARCHITECTURE.md).
            var begriff = search.Trim().ToUpperInvariant();
            abfrage = abfrage.Where(q =>
                q.Text.ToUpper().Contains(begriff) ||
                q.Number.ToUpper().Contains(begriff) ||
                (q.SampleSolution != null && q.SampleSolution.ToUpper().Contains(begriff)) ||
                q.Options.Any(o => o.Text.ToUpper().Contains(begriff)));
        }

        var fragen = await abfrage
            .OrderBy(q => q.Catalog!.SortOrder)
            .ThenBy(q => q.SortOrder)
            .AsNoTracking()
            .ToListAsync(ct);

        var dtos = fragen.Select(ZuDto).ToList();

        if (onlyFlagged)
            dtos = dtos.Where(q => q.Flags.Count > 0 || q.Options.Any(o => o.Flags.Count > 0)).ToList();

        return Result<IReadOnlyList<AdminQuizQuestionDto>>.Success(dtos);
    }

    public async Task<Result<AdminQuizQuestionDto>> UpdateQuestionAsync(
        Guid userId, Guid questionId, UpdateQuizQuestionRequest request, CancellationToken ct = default)
    {
        var frage = await db.QuizQuestions
            .Include(q => q.Options)
            .Include(q => q.Catalog)
            .FirstOrDefaultAsync(q => q.Id == questionId, ct);

        if (frage is null)
            return Result<AdminQuizQuestionDto>.NotFound("Frage nicht gefunden.");

        if (string.IsNullOrWhiteSpace(request.Text))
            return Result<AdminQuizQuestionDto>.Failure("Die Fragestellung darf nicht leer sein.");

        var zeilen = request.Options ?? [];
        if (zeilen.Any(z => string.IsNullOrWhiteSpace(z.Text)))
            return Result<AdminQuizQuestionDto>.Failure("Eine Antwortzeile darf nicht leer sein.");

        if (zeilen.Any(z => !Enum.TryParse<QuizOptionKind>(z.Kind, out _)))
            return Result<AdminQuizQuestionDto>.Failure("Unbekannte Zeilenart.");

        var fehler = Pruefen(frage.Kind, zeilen, request.SampleSolution);
        if (fehler is not null)
            return Result<AdminQuizQuestionDto>.Failure(fehler);

        frage.Text = request.Text.Trim();
        frage.SampleSolution = string.IsNullOrWhiteSpace(request.SampleSolution)
            ? null
            : request.SampleSolution.Trim();

        var bestand = frage.Options.ToList();
        var behalten = new HashSet<Guid>();
        var reihenfolge = 0;

        foreach (var zeile in zeilen)
        {
            reihenfolge++;
            var art = Enum.Parse<QuizOptionKind>(zeile.Kind);

            var option = zeile.Id is { } id ? bestand.FirstOrDefault(o => o.Id == id) : null;
            if (zeile.Id is not null && option is null)
                return Result<AdminQuizQuestionDto>.Failure("Eine geänderte Antwortzeile gehört nicht zu dieser Frage.");

            if (option is null)
            {
                option = new QuizOption { QuestionId = frage.Id, Question = frage };
                frage.Options.Add(option);
                db.QuizOptions.Add(option);
            }
            else
            {
                option.UpdatedAt = clock.GetUtcNow();
                behalten.Add(option.Id);
            }

            option.Kind = art;
            option.Text = zeile.Text.Trim();
            option.IsCorrect = art == QuizOptionKind.Answer && zeile.IsCorrect;
            option.MatchKey = string.IsNullOrWhiteSpace(zeile.MatchKey) ? null : zeile.MatchKey.Trim();
            option.ImageName = string.IsNullOrWhiteSpace(zeile.ImageName) ? null : zeile.ImageName.Trim();
            option.SortOrder = reihenfolge;
        }

        foreach (var entfernt in bestand.Where(o => !behalten.Contains(o.Id)))
            entfernt.DeletedAt = clock.GetUtcNow();

        // Ab jetzt hat die Handfassung Vorrang - der Seeder lässt die Frage in
        // Ruhe, sonst wäre die Korrektur beim nächsten Start wieder weg.
        frage.EditedAt = clock.GetUtcNow();
        frage.EditedByUserId = userId;
        frage.UpdatedAt = clock.GetUtcNow();

        await db.SaveChangesAsync(ct);

        return Result<AdminQuizQuestionDto>.Success(ZuDto(frage));
    }

    public async Task<Result> RevertQuestionAsync(Guid questionId, CancellationToken ct = default)
    {
        var frage = await db.QuizQuestions.FirstOrDefaultAsync(q => q.Id == questionId, ct);
        if (frage is null)
            return Result.NotFound("Frage nicht gefunden.");

        if (frage.EditedAt is null)
            return Result.Failure("Diese Frage wurde nicht von Hand überarbeitet.");

        frage.EditedAt = null;
        frage.EditedByUserId = null;
        frage.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <summary>
    /// Prüft, ob die Frage nach der Änderung noch beantwortbar wäre. Lieber
    /// hier ablehnen als eine Frage in den Katalog lassen, die niemand lösen
    /// kann - genau das war der Fehler bei den Zuordnungsaufgaben.
    /// </summary>
    private static string? Pruefen(
        QuizQuestionKind art, IReadOnlyList<UpdateQuizOptionRequest> zeilen, string? musterloesung)
    {
        var antworten = zeilen.Where(z => z.Kind == nameof(QuizOptionKind.Answer)).ToList();
        var begriffe = zeilen.Where(z => z.Kind == nameof(QuizOptionKind.Term)).ToList();

        switch (art)
        {
            case QuizQuestionKind.SingleChoice:
                if (antworten.Count < 2) return "Eine Auswahlfrage braucht mindestens zwei Antworten.";
                if (antworten.Count(a => a.IsCorrect) != 1) return "Genau eine Antwort muss richtig sein.";
                break;

            case QuizQuestionKind.MultipleChoice:
                if (antworten.Count < 2) return "Eine Auswahlfrage braucht mindestens zwei Antworten.";
                if (!antworten.Any(a => a.IsCorrect)) return "Mindestens eine Antwort muss richtig sein.";
                break;

            case QuizQuestionKind.Assignment:
                if (begriffe.Count < 2) return "Eine Zuordnung braucht mindestens zwei Begriffe.";
                if (begriffe.Any(b => string.IsNullOrWhiteSpace(b.MatchKey)))
                    return "Jeder Begriff braucht einen Schlüssel.";
                var schluessel = begriffe.Select(b => b.MatchKey!.Trim().ToUpperInvariant()).ToList();
                if (schluessel.Distinct().Count() != schluessel.Count)
                    return "Jeder Schlüssel darf nur einmal vorkommen.";
                break;

            case QuizQuestionKind.FreeText:
                if (string.IsNullOrWhiteSpace(musterloesung))
                    return "Eine Freitextfrage braucht eine Musterlösung.";
                break;
        }

        return null;
    }

    // ---- Auffälligkeiten ----
    //
    // Die Kataloge stammen aus einer PDF-Auswertung, und die hinterlässt
    // Spuren. Statt 112 Fragen Zeile für Zeile durchzusehen, zeigt die
    // Verwaltung, wo sich das Nachsehen lohnt. Bewusst nur Hinweise, keine
    // automatischen Korrekturen - ob "Gehorsams- und Straßenverkehrsteil"
    // richtig ist, weiß nur ein Mensch.

    [GeneratedRegex(@"[\x00-\x1f\x7f-\x9f]")]
    private static partial Regex Steuerzeichen();

    /// <summary>Trennstrich mitten im Wort - aber nicht die zulässige Auslassung ("Gehorsams- und ...").</summary>
    [GeneratedRegex(@"[a-zäöüß]{2,}- (?!und |oder |bzw|sowie |wie )[a-zäöüß]")]
    private static partial Regex Trennstrich();

    /// <summary>Kleinbuchstabe direkt gefolgt von Großbuchstabe - meist ein verschlucktes Leerzeichen.</summary>
    [GeneratedRegex(@"[a-zäöüß][A-ZÄÖÜ]")]
    private static partial Regex FehlendesLeerzeichen();

    [GeneratedRegex(@"\s[,.;!?]")]
    private static partial Regex LeerzeichenVorSatzzeichen();

    [GeneratedRegex(@"\S {2,}\S")]
    private static partial Regex DoppeltesLeerzeichen();

    /// <summary>
    /// Aufzählungszeichen wie "a)" oder "1)". Sie sehen aus wie eine
    /// schließende Klammer ohne Gegenstück und lösten die Klammerprüfung aus -
    /// die Musterlösung zu D5 ("a) Geruchssinn b) ...") war deshalb dauerhaft
    /// als auffällig markiert, obwohl sie es nicht ist.
    /// </summary>
    [GeneratedRegex(@"\b[a-z0-9]\)")]
    private static partial Regex Aufzaehlungszeichen();

    private static IReadOnlyList<string> AuffaelligkeitenIn(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var flags = new List<string>();
        if (Steuerzeichen().IsMatch(text)) flags.Add("Steuerzeichen");
        if (Trennstrich().IsMatch(text)) flags.Add("Trennstrich im Wort");
        if (FehlendesLeerzeichen().IsMatch(text)) flags.Add("fehlendes Leerzeichen");
        if (LeerzeichenVorSatzzeichen().IsMatch(text)) flags.Add("Leerzeichen vor Satzzeichen");
        if (DoppeltesLeerzeichen().IsMatch(text)) flags.Add("doppeltes Leerzeichen");
        var ohneAufzaehlung = Aufzaehlungszeichen().Replace(text, "");
        if (ohneAufzaehlung.Count(c => c == '(') != ohneAufzaehlung.Count(c => c == ')'))
            flags.Add("unpaarige Klammer");
        return flags;
    }

    private static AdminQuizQuestionDto ZuDto(QuizQuestion frage)
    {
        var flags = new List<string>(AuffaelligkeitenIn(frage.Text));
        flags.AddRange(AuffaelligkeitenIn(frage.SampleSolution).Where(f => !flags.Contains(f)));

        var antworten = frage.Options.Where(o => o.Kind == QuizOptionKind.Answer).ToList();
        if (frage.Kind == QuizQuestionKind.SingleChoice && antworten.Count(o => o.IsCorrect) != 1)
            flags.Add("keine eindeutige Lösung");
        if (frage.Kind is QuizQuestionKind.SingleChoice or QuizQuestionKind.MultipleChoice && antworten.Count < 2)
            flags.Add("weniger als zwei Antworten");

        return new AdminQuizQuestionDto(
            frage.Id,
            frage.Catalog?.Code ?? string.Empty,
            frage.Catalog?.Name ?? string.Empty,
            frage.Number,
            frage.Section,
            frage.SectionName,
            frage.Kind.ToString(),
            frage.Text,
            frage.SampleSolution,
            frage.ImageName,
            frage.EditedAt,
            frage.Options
                .OrderBy(o => o.SortOrder)
                .Select(o => new AdminQuizOptionDto(
                    o.Id, o.Kind.ToString(), o.Text, o.IsCorrect, o.MatchKey, o.ImageName, o.SortOrder,
                    AuffaelligkeitenIn(o.Text)))
                .ToList(),
            flags);
    }
}
