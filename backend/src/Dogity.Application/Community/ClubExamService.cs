using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Dogs;
using Dogity.Application.Notifications;
using Dogity.Application.Planning;
using Dogity.Domain.Community;
using Dogity.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

/// <summary>
/// Prüfungstage des Vereins: anlegen, Teams von Hand eintragen (Mitglied mit Hund oder Gast), die
/// Prüfungsreife der Mitglied-Teams sehen, Ergebnisse eintragen. Die Mitglieder melden nichts selbst -
/// weil die Familie nichts bestätigt, bekommt sie bei Meldung, Austragen, Absage und Ergebnis eine
/// Benachrichtigung (Transparenz statt Zustimmung).
///
/// Ein Ergebnis "bestanden" steht sofort als Leistung beim Hund: <see cref="IGoalService"/> trägt es am Ziel
/// der Familie ein (siehe dort), der Eintrag merkt sich das Ziel. Die Familie bleibt Herrin ihres Ziels - hat
/// sie es inzwischen geändert oder gelöscht, ändert eine spätere Korrektur der Prüfungsleitung nur den Eintrag.
/// </summary>
public class ClubExamService(
    IApplicationDbContext db,
    IUserLookupService userLookup,
    INotificationService notifications,
    IGoalService goals,
    TimeProvider timeProvider) : IClubExamService
{
    // ---------- Lesen ----------

    public async Task<Result<IReadOnlyList<ClubExamListItemDto>>> GetListAsync(Guid userId, Guid clubId, CancellationToken ct = default)
    {
        if (!await DarfAsync(userId, clubId, ct))
            return Result<IReadOnlyList<ClubExamListItemDto>>.NotFound("Verein nicht gefunden.");

        var tage = await db.ClubExams
            .Where(e => e.ClubId == clubId)
            .OrderBy(e => e.Date).ThenBy(e => e.StartTime)
            .AsNoTracking()
            .ToListAsync(ct);
        var ids = tage.Select(e => e.Id).ToList();
        var ordnungen = await db.OrdnungenJePruefungAsync(ids, ct);
        var zahlen = (await db.ClubExamEntries
                .Where(e => ids.Contains(e.ExamId))
                .GroupBy(e => e.ExamId)
                .Select(g => new { ExamId = g.Key, Teams = g.Count(), Bestanden = g.Count(e => e.Result == ClubExamResult.Passed) })
                .ToListAsync(ct))
            .ToDictionary(z => z.ExamId);

        IReadOnlyList<ClubExamListItemDto> liste = tage
            .Select(e => new ClubExamListItemDto(
                e.Id, e.Date, e.StartTime, e.Location, e.Status,
                zahlen.TryGetValue(e.Id, out var z) ? z.Teams : 0,
                zahlen.TryGetValue(e.Id, out z) ? z.Bestanden : 0,
                ordnungen.GetValueOrDefault(e.Id) ?? []))
            .ToList();
        return Result<IReadOnlyList<ClubExamListItemDto>>.Success(liste);
    }

    public async Task<Result<ClubExamDto>> GetAsync(Guid userId, Guid clubId, Guid examId, CancellationToken ct = default)
    {
        var tag = await LadeAsync(userId, clubId, examId, ct);
        if (tag is null) return Result<ClubExamDto>.NotFound("Prüfung nicht gefunden.");
        return Result<ClubExamDto>.Success(await BaueAsync(tag, ct));
    }

    public async Task<Result<IReadOnlyList<ClubExamCandidateDto>>> GetCandidatesAsync(Guid userId, Guid clubId, CancellationToken ct = default)
    {
        if (!await DarfAsync(userId, clubId, ct))
            return Result<IReadOnlyList<ClubExamCandidateDto>>.NotFound("Verein nicht gefunden.");

        var personen = await db.VereinsPersonenIds(clubId).ToListAsync(ct);
        // Nur Hunde, die dem Mitglied aktiv gehören (Besitz oder Mitbesitz, nicht archiviert).
        var hunde = await (
                from o in db.DogOwners
                where personen.Contains(o.UserId)
                join d in db.Dogs on o.DogId equals d.Id
                where d.ArchivedAt == null
                select new { o.UserId, d.Id, d.Name, d.Breed, d.Birthday })
            .AsNoTracking()
            .ToListAsync(ct);
        var namen = await userLookup.FindByIdsAsync(hunde.Select(h => h.UserId).Distinct().ToList(), ct);

        IReadOnlyList<ClubExamCandidateDto> liste = hunde
            .GroupBy(h => h.UserId)
            .Where(g => namen.ContainsKey(g.Key))
            .Select(g => new ClubExamCandidateDto(
                g.Key, namen[g.Key].FirstName, namen[g.Key].LastName,
                g.OrderBy(h => h.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(h => new ClubExamCandidateDogDto(h.Id, h.Name, h.Breed, h.Birthday)).ToList()))
            .OrderBy(c => c.LastName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(c => c.FirstName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return Result<IReadOnlyList<ClubExamCandidateDto>>.Success(liste);
    }

    public async Task<Result<IReadOnlyList<DogExamRegistrationDto>>> GetForDogAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        // Nur die Familie: Betreuende Trainer:innen sehen, wo der Hund gemeldet ist, nicht - es ist ihr Hund nicht.
        if (!await db.IstBesitzerAsync(userId, dogId, ct))
            return Result<IReadOnlyList<DogExamRegistrationDto>>.NotFound("Hund nicht gefunden.");

        var heute = Heute();
        var zeilen = await db.ClubExamEntries
            .Where(e => e.DogId == dogId && e.Result == ClubExamResult.Open
                && e.Exam!.Status == ClubExamStatus.Planned && e.Exam.Date >= heute)
            .OrderBy(e => e.Exam!.Date)
            .Select(e => new { e.Id, e.Exam!.Date, ClubName = e.Exam.Club!.Name, e.RegulationId })
            .AsNoTracking()
            .ToListAsync(ct);
        var ordnungIds = zeilen.Select(z => z.RegulationId).Distinct().ToList();
        var ordnungen = await db.Regulations
            .Where(r => ordnungIds.Contains(r.Id))
            .Select(r => new ClubExamRegulationDto(r.Id, r.Name, r.NameEn, null))
            .ToDictionaryAsync(r => r.Id, ct);

        IReadOnlyList<DogExamRegistrationDto> liste = zeilen
            .Where(z => ordnungen.ContainsKey(z.RegulationId))
            .Select(z => new DogExamRegistrationDto(z.Id, z.Date, z.ClubName, ordnungen[z.RegulationId]))
            .ToList();
        return Result<IReadOnlyList<DogExamRegistrationDto>>.Success(liste);
    }

    // ---------- Prüfungstag ----------

    public async Task<Result<ClubExamDto>> CreateAsync(Guid userId, Guid clubId, SaveClubExamRequest request, CancellationToken ct = default)
    {
        if (!await DarfAsync(userId, clubId, ct))
            return Result<ClubExamDto>.NotFound("Verein nicht gefunden.");
        var fehler = await PruefeTagAsync(clubId, request, ct);
        if (fehler is not null) return Result<ClubExamDto>.Failure(fehler);

        var tag = new ClubExam { ClubId = clubId, CreatedByUserId = userId };
        Uebernimm(tag, request);
        foreach (var ordnungId in (request.RegulationIds ?? []).Distinct())
            tag.Regulations.Add(new ClubExamRegulation { ExamId = tag.Id, RegulationId = ordnungId });
        db.ClubExams.Add(tag);
        await db.SaveChangesAsync(ct);

        return Result<ClubExamDto>.Success(await BaueAsync(tag, ct));
    }

    public async Task<Result<ClubExamDto>> UpdateAsync(Guid userId, Guid clubId, Guid examId, SaveClubExamRequest request, CancellationToken ct = default)
    {
        var tag = await LadeAsync(userId, clubId, examId, ct, track: true);
        if (tag is null) return Result<ClubExamDto>.NotFound("Prüfung nicht gefunden.");
        var fehler = await PruefeTagAsync(clubId, request, ct);
        if (fehler is not null) return Result<ClubExamDto>.Failure(fehler);

        // Das Ergebnis steht am Hund unter dem Prüfungstag - ein anderer Tag ließe es dort nicht mehr wiedererkennen.
        if (request.Date != tag.Date && await db.ClubExamEntries.AnyAsync(e => e.ExamId == tag.Id && e.Result != ClubExamResult.Open, ct))
            return Result<ClubExamDto>.Failure("Das Datum lässt sich nicht mehr ändern, sobald Ergebnisse eingetragen sind.");

        Uebernimm(tag, request);
        tag.UpdatedAt = timeProvider.GetUtcNow();

        var gewollt = (request.RegulationIds ?? []).ToHashSet();
        var bisher = tag.Regulations.Where(r => r.DeletedAt == null).ToList();
        foreach (var zeile in bisher.Where(r => !gewollt.Contains(r.RegulationId)))
            zeile.DeletedAt = timeProvider.GetUtcNow();
        foreach (var ordnungId in gewollt.Where(id => bisher.All(r => r.RegulationId != id)))
            db.ClubExamRegulations.Add(new ClubExamRegulation { ExamId = tag.Id, RegulationId = ordnungId });
        await db.SaveChangesAsync(ct);

        return Result<ClubExamDto>.Success(await BaueAsync(tag, ct));
    }

    public async Task<Result> CancelAsync(Guid userId, Guid clubId, Guid examId, CancellationToken ct = default)
    {
        var tag = await LadeAsync(userId, clubId, examId, ct, track: true);
        if (tag is null) return Result.NotFound("Prüfung nicht gefunden.");
        if (tag.Status == ClubExamStatus.Cancelled) return Result.Failure("Die Prüfung ist schon abgesagt.");
        if (await db.ClubExamEntries.AnyAsync(e => e.ExamId == tag.Id && e.Result != ClubExamResult.Open, ct))
            return Result.Failure("Eine Prüfung mit Ergebnissen lässt sich nicht absagen oder löschen.");

        tag.Status = ClubExamStatus.Cancelled;
        tag.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await MeldeAbsageAsync(tag, ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid clubId, Guid examId, CancellationToken ct = default)
    {
        var tag = await LadeAsync(userId, clubId, examId, ct, track: true);
        if (tag is null) return Result.NotFound("Prüfung nicht gefunden.");
        if (await db.ClubExamEntries.AnyAsync(e => e.ExamId == tag.Id && e.Result != ClubExamResult.Open, ct))
            return Result.Failure("Eine Prüfung mit Ergebnissen lässt sich nicht absagen oder löschen.");

        var jetzt = timeProvider.GetUtcNow();
        var eintraege = await db.ClubExamEntries.Where(e => e.ExamId == tag.Id).ToListAsync(ct);
        // Wer schon eine Absage bekommen hat, bekommt keine zweite.
        var meldungen = tag.Status == ClubExamStatus.Planned ? await MitgliederMeldungenAsync(tag, eintraege, ct) : [];
        foreach (var e in eintraege) Loesche(e, jetzt);
        foreach (var r in tag.Regulations) r.DeletedAt = jetzt;
        tag.DeletedAt = jetzt;
        await db.SaveChangesAsync(ct);

        foreach (var (hundId, meldung) in meldungen) await MeldeFamilieAsync(hundId, meldung, ct);
        return Result.Success();
    }

    // ---------- Teams ----------

    public async Task<Result> AddEntryAsync(Guid userId, Guid clubId, Guid examId, AddClubExamEntryRequest request, CancellationToken ct = default)
    {
        var tag = await LadeAsync(userId, clubId, examId, ct, track: true);
        if (tag is null) return Result.NotFound("Prüfung nicht gefunden.");
        if (tag.Status == ClubExamStatus.Cancelled) return Result.Failure("Die Prüfung ist abgesagt.");

        if (!await db.Regulations.AnyAsync(r => r.Id == request.RegulationId && (r.Sport!.ClubId == null || r.Sport.ClubId == clubId), ct))
            return Result.NotFound("Prüfungsordnung nicht gefunden.");
        var angeboten = tag.Regulations.Where(r => r.DeletedAt == null).ToList();
        if (angeboten.Count > 0 && angeboten.All(r => r.RegulationId != request.RegulationId))
            return Result.Failure("Diese Ordnung wird bei dieser Prüfung nicht angeboten.");

        var eintrag = new ClubExamEntry { ExamId = tag.Id, RegulationId = request.RegulationId };
        var vorhanden = db.ClubExamEntries.Where(e => e.ExamId == tag.Id && e.RegulationId == request.RegulationId);

        if (request.MemberUserId is not null || request.DogId is not null)
        {
            if (request.MemberUserId is not { } mitglied || request.DogId is not { } hund)
                return Result.Failure("Bitte wähle Mitglied und Hund.");
            if (!string.IsNullOrWhiteSpace(request.GuestHandlerName) || !string.IsNullOrWhiteSpace(request.GuestDogName) || !string.IsNullOrWhiteSpace(request.GuestInfo))
                return Result.Failure("Ein Team ist entweder ein Mitglied mit Hund oder ein Gast.");
            if (!await db.GehoertZumVereinAsync(mitglied, clubId, ct))
                return Result.Failure("Dieses Mitglied gehört nicht zum Verein.");
            if (!await db.IstBesitzerAsync(mitglied, hund, ct) || await db.Dogs.AnyAsync(d => d.Id == hund && d.ArchivedAt != null, ct))
                return Result.Failure("Dieser Hund gehört dem Mitglied nicht.");
            if (await vorhanden.AnyAsync(e => e.DogId == hund, ct))
                return Result.Failure("Dieses Team ist in dieser Ordnung schon gemeldet.");

            eintrag.MemberUserId = mitglied;
            eintrag.DogId = hund;
        }
        else
        {
            var fuehrer = request.GuestHandlerName?.Trim();
            var hundName = request.GuestDogName?.Trim();
            var info = Texte.NullIfBlank(request.GuestInfo);
            if (string.IsNullOrEmpty(fuehrer) || string.IsNullOrEmpty(hundName))
                return Result.Failure("Bitte Name und Hund des Gasts angeben.");
            var zuLang = Textlaengen.ZuLang(fuehrer, Textlaengen.GastName, "Der Name der Hundeführer:in")
                ?? Textlaengen.ZuLang(hundName, Textlaengen.GastName, "Der Hundename")
                ?? Textlaengen.ZuLang(info, Textlaengen.GastInfo, "Die Angabe zu Verein und Rasse");
            if (zuLang is not null) return Result.Failure(zuLang);

            var gleiche = (await vorhanden.Where(e => e.GuestHandlerName != null).Select(e => new { e.GuestHandlerName, e.GuestDogName }).ToListAsync(ct))
                .Any(e => string.Equals(e.GuestHandlerName, fuehrer, StringComparison.CurrentCultureIgnoreCase)
                    && string.Equals(e.GuestDogName, hundName, StringComparison.CurrentCultureIgnoreCase));
            if (gleiche) return Result.Failure("Dieses Team ist in dieser Ordnung schon gemeldet.");

            eintrag.GuestHandlerName = fuehrer;
            eintrag.GuestDogName = hundName;
            eintrag.GuestInfo = info;
        }

        eintrag.StartOrder = (await vorhanden.Select(e => (int?)e.StartOrder).MaxAsync(ct) ?? 0) + 1;
        db.ClubExamEntries.Add(eintrag);
        // Ein neues Team ohne Ergebnis macht einen fertigen Tag wieder offen.
        if (tag.Status == ClubExamStatus.Done) tag.Status = ClubExamStatus.Planned;
        await db.SaveChangesAsync(ct);

        if (eintrag.DogId is { } hundId)
            await MeldeFamilieAsync(hundId, await GemeldetMeldungAsync(tag, eintrag, ct), ct);
        return Result.Success();
    }

    public async Task<Result> RemoveEntryAsync(Guid userId, Guid clubId, Guid examId, Guid entryId, CancellationToken ct = default)
    {
        var tag = await LadeAsync(userId, clubId, examId, ct, track: true);
        if (tag is null) return Result.NotFound("Prüfung nicht gefunden.");
        var eintrag = await db.ClubExamEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.ExamId == tag.Id, ct);
        if (eintrag is null) return Result.NotFound("Team nicht gefunden.");

        // Nur solange die Meldung noch gilt: Nach einer Absage oder einem Ergebnis hat die Familie schon Bescheid.
        var meldung = eintrag.DogId is not null && tag.Status == ClubExamStatus.Planned && eintrag.Result == ClubExamResult.Open
            ? await ZurueckgenommenMeldungAsync(tag, eintrag, ct) : null;
        var hundId = eintrag.DogId;
        // Ein "bestanden" nimmt die Leistung am Hund wieder mit (wie das Zurücksetzen des Ergebnisses).
        if (eintrag.Result == ClubExamResult.Passed)
            await ZieheZielZurueckAsync(tag, eintrag, ct);
        Loesche(eintrag, timeProvider.GetUtcNow());
        await AktualisiereStatusAsync(tag, ct);
        await db.SaveChangesAsync(ct);

        if (hundId is { } id && meldung is not null) await MeldeFamilieAsync(id, meldung, ct);
        return Result.Success();
    }

    public async Task<Result> MoveEntryAsync(Guid userId, Guid clubId, Guid examId, Guid entryId, int direction, CancellationToken ct = default)
    {
        var tag = await LadeAsync(userId, clubId, examId, ct);
        if (tag is null) return Result.NotFound("Prüfung nicht gefunden.");
        if (direction is not (-1 or 1)) return Result.Failure("Die Reihenfolge lässt sich nur um einen Platz ändern.");

        var eintrag = await db.ClubExamEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.ExamId == tag.Id, ct);
        if (eintrag is null) return Result.NotFound("Team nicht gefunden.");

        var reihe = await db.ClubExamEntries
            .Where(e => e.ExamId == tag.Id && e.RegulationId == eintrag.RegulationId)
            .OrderBy(e => e.StartOrder).ThenBy(e => e.CreatedAt)
            .ToListAsync(ct);
        var platz = reihe.FindIndex(e => e.Id == eintrag.Id);
        var ziel = platz + direction;
        // Am Rand: nichts zu tun, aber auch kein Fehler (ein doppelter Tipp).
        if (ziel < 0 || ziel >= reihe.Count) return Result.Success();

        // Die Werte werden neu vergeben statt getauscht: So bleiben auch Einträge mit gleichem Wert in einer festen Reihenfolge.
        (reihe[platz], reihe[ziel]) = (reihe[ziel], reihe[platz]);
        for (var i = 0; i < reihe.Count; i++) reihe[i].StartOrder = i + 1;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ---------- Ergebnisse ----------

    public async Task<Result> SetResultAsync(Guid userId, Guid clubId, Guid examId, Guid entryId, SetClubExamResultRequest request, CancellationToken ct = default)
    {
        var tag = await LadeAsync(userId, clubId, examId, ct, track: true);
        if (tag is null) return Result.NotFound("Prüfung nicht gefunden.");
        var eintrag = await db.ClubExamEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.ExamId == tag.Id, ct);
        if (eintrag is null) return Result.NotFound("Team nicht gefunden.");
        if (tag.Status == ClubExamStatus.Cancelled) return Result.Failure("Die Prüfung ist abgesagt.");
        if (!Enum.IsDefined(request.Result)) return Result.Failure("Ungültiges Ergebnis.");

        var neu = request.Result;
        var bemerkung = Texte.NullIfBlank(request.Note);
        if (neu == ClubExamResult.Open && (request.Score is not null || bemerkung is not null))
            return Result.Failure("Ohne Ergebnis gibt es weder Punkte noch eine Bemerkung.");
        if (neu == ClubExamResult.NotStarted && request.Score is not null)
            return Result.Failure("Wer nicht angetreten ist, hat keine Punkte.");
        if (neu != ClubExamResult.Open
            && await goals.PruefeVereinsergebnisAsync(eintrag.RegulationId, tag.Date, request.Score, bemerkung, ct) is { } fehler)
            return Result.Failure(fehler);

        var bisher = (eintrag.Result, eintrag.Score, eintrag.ResultNote);
        if (bisher == (neu, request.Score, bemerkung)) return Result.Success();

        // Am Hund nachziehen (nur Mitglied-Teams): "bestanden" trägt das Ziel ein oder ändert es, alles andere nimmt es zurück.
        if (eintrag.MemberUserId is not null && eintrag.DogId is { } hundId)
        {
            if (eintrag.Result == ClubExamResult.Passed && neu == ClubExamResult.Passed)
            {
                if (eintrag.GoalId is { } zielId)
                    await goals.ZieheVereinsergebnisNachAsync(
                        zielId, hundId, new VereinsErgebnis(tag.Date, eintrag.Score, eintrag.ResultNote),
                        new VereinsErgebnis(tag.Date, request.Score, bemerkung), eintrag.GoalCreated, ct);
            }
            else if (eintrag.Result == ClubExamResult.Passed)
            {
                await ZieheZielZurueckAsync(tag, eintrag, ct);
            }
            else if (neu == ClubExamResult.Passed)
            {
                var ziel = await goals.TrageVereinsergebnisAsync(hundId, eintrag.RegulationId, new VereinsErgebnis(tag.Date, request.Score, bemerkung), ct);
                eintrag.GoalId = ziel.GoalId;
                eintrag.GoalCreated = ziel.NeuAngelegt;
            }
        }

        eintrag.Result = neu;
        eintrag.Score = request.Score;
        eintrag.ResultNote = bemerkung;
        eintrag.ResultEnteredAt = neu == ClubExamResult.Open ? null : timeProvider.GetUtcNow();
        eintrag.ResultByUserId = neu == ClubExamResult.Open ? null : userId;
        eintrag.UpdatedAt = timeProvider.GetUtcNow();
        await AktualisiereStatusAsync(tag, ct);
        await db.SaveChangesAsync(ct);

        if (neu != ClubExamResult.Open && eintrag.DogId is { } familienHund)
            await MeldeFamilieAsync(familienHund, await ErgebnisMeldungAsync(tag, eintrag, ct), ct);
        return Result.Success();
    }

    // ---------- Hilfen ----------

    private DateOnly Heute() => Vereinszeit.Tag(timeProvider.GetUtcNow());

    private Task<bool> DarfAsync(Guid userId, Guid clubId, CancellationToken ct) =>
        db.CanManageExamsAsync(userId, clubId, ct);

    /// <summary>Der Prüfungstag, wenn die Person ihn führen darf - sonst null (fremder Verein, fehlende Rolle, gibt es nicht).</summary>
    private async Task<ClubExam?> LadeAsync(Guid userId, Guid clubId, Guid examId, CancellationToken ct, bool track = false)
    {
        if (!await DarfAsync(userId, clubId, ct)) return null;
        var abfrage = db.ClubExams.Include(e => e.Regulations).Where(e => e.Id == examId && e.ClubId == clubId);
        return await (track ? abfrage : abfrage.AsNoTracking()).FirstOrDefaultAsync(ct);
    }

    private async Task<string?> PruefeTagAsync(Guid clubId, SaveClubExamRequest request, CancellationToken ct)
    {
        if (request.Date == default) return "Bitte das Datum der Prüfung angeben.";
        if (string.IsNullOrWhiteSpace(request.Location)) return "Bitte den Ort der Prüfung angeben.";
        var zuLang = Textlaengen.ZuLang(request.Location, Textlaengen.PruefungsOrt, "Der Ort")
            ?? Textlaengen.ZuLang(request.JudgeName, Textlaengen.PruefungsRichter, "Der Name der Richter:in")
            ?? Textlaengen.ZuLang(request.Note, Textlaengen.PruefungsBemerkung, "Die Bemerkung");
        if (zuLang is not null) return zuLang;

        var ordnungen = (request.RegulationIds ?? []).Distinct().ToList();
        if (ordnungen.Count == 0) return null;
        var gueltig = await db.Regulations.CountAsync(r => ordnungen.Contains(r.Id) && (r.Sport!.ClubId == null || r.Sport.ClubId == clubId), ct);
        return gueltig == ordnungen.Count ? null : "Prüfungsordnung nicht gefunden.";
    }

    private static void Uebernimm(ClubExam tag, SaveClubExamRequest request)
    {
        tag.Date = request.Date;
        tag.StartTime = request.StartTime;
        tag.Location = request.Location.Trim();
        tag.JudgeName = Texte.NullIfBlank(request.JudgeName);
        tag.RegistrationDeadline = request.RegistrationDeadline;
        tag.Note = Texte.NullIfBlank(request.Note);
    }

    /// <summary>Löschen heißt hier auch: Die Gastdaten Dritter verschwinden mit dem Eintrag, nicht erst im Papierkorb der Tabelle.</summary>
    private static void Loesche(ClubExamEntry eintrag, DateTimeOffset jetzt)
    {
        eintrag.GuestHandlerName = null;
        eintrag.GuestDogName = null;
        eintrag.GuestInfo = null;
        eintrag.DeletedAt = jetzt;
    }

    /// <summary>
    /// "Fertig", sobald jedes Team ein Ergebnis hat - und wieder "geplant", wenn eins zurückgesetzt wird oder ein
    /// Team dazukommt. Eine Absage bleibt, wie sie ist. Rechnet mit dem Stand im Speicher (auch mit noch
    /// ungespeicherten Änderungen), nicht mit der Datenbank.
    /// </summary>
    private async Task AktualisiereStatusAsync(ClubExam tag, CancellationToken ct)
    {
        if (tag.Status == ClubExamStatus.Cancelled) return;
        // Der Speicher kennt Änderungen, die die Datenbank noch nicht hat: Gelöschtes zählt nicht mehr mit.
        var eintraege = (await db.ClubExamEntries.Where(e => e.ExamId == tag.Id).ToListAsync(ct)).Where(e => e.DeletedAt == null).ToList();
        tag.Status = eintraege.Count > 0 && eintraege.All(e => e.Result != ClubExamResult.Open) ? ClubExamStatus.Done : ClubExamStatus.Planned;
    }

    /// <summary>Nimmt "bestanden" am Hund zurück; der Eintrag verliert den Bezug zum Ziel (auch wenn die Familie es inzwischen anders hat).</summary>
    private async Task ZieheZielZurueckAsync(ClubExam tag, ClubExamEntry eintrag, CancellationToken ct)
    {
        if (eintrag.GoalId is { } zielId && eintrag.DogId is { } hundId)
            await goals.ZieheVereinsergebnisNachAsync(
                zielId, hundId, new VereinsErgebnis(tag.Date, eintrag.Score, eintrag.ResultNote), null, eintrag.GoalCreated, ct);
        eintrag.GoalId = null;
        eintrag.GoalCreated = false;
    }

    // ---------- Benachrichtigungen ----------

    private async Task MeldeFamilieAsync(Guid dogId, Meldung meldung, CancellationToken ct)
    {
        var besitzer = await db.BesitzerIds(dogId).ToListAsync(ct);
        foreach (var id in besitzer)
            await notifications.CreateAsync(id, meldung, $"/dogs/{dogId}", ct);
    }

    private async Task<Meldung> GemeldetMeldungAsync(ClubExam tag, ClubExamEntry eintrag, CancellationToken ct)
    {
        var (verein, hund, ordnung) = await NamenAsync(tag, eintrag, ct);
        var meldung = Meldung.Von("{verein} hat {hund} für die {ordnung} am {datum} gemeldet. Die Prüfungsleitung sieht dafür die Prüfungsreife.",
            ("verein", verein), ("hund", hund), ("ordnung", ordnung), ("datum", tag.Date));
        return meldung;
    }

    private async Task<Meldung> ZurueckgenommenMeldungAsync(ClubExam tag, ClubExamEntry eintrag, CancellationToken ct)
    {
        var (verein, hund, ordnung) = await NamenAsync(tag, eintrag, ct);
        var meldung = Meldung.Von("{verein} hat die Meldung von {hund} für die {ordnung} am {datum} zurückgenommen.",
            ("verein", verein), ("hund", hund), ("ordnung", ordnung), ("datum", tag.Date));
        return meldung;
    }

    private async Task<Meldung> ErgebnisMeldungAsync(ClubExam tag, ClubExamEntry eintrag, CancellationToken ct)
    {
        var (_, hund, ordnung) = await NamenAsync(tag, eintrag, ct);
        var punkte = eintrag.Score ?? 0;
        var mitPunkten = eintrag.Score is not null;

        // Eine Vorlage je Fall, jeweils ein ganzer Satz (siehe Meldung): Wörter wie "bestanden" lassen sich nicht einsetzen.
        Meldung meldung;
        if (eintrag.Result == ClubExamResult.Passed && mitPunkten)
            meldung = Meldung.Von("Ergebnis der {ordnung} vom {datum} für {hund} eingetragen: bestanden, Punkte: {punkte}.",
                ("ordnung", ordnung), ("datum", tag.Date), ("hund", hund), ("punkte", punkte));
        else if (eintrag.Result == ClubExamResult.Passed)
            meldung = Meldung.Von("Ergebnis der {ordnung} vom {datum} für {hund} eingetragen: bestanden.",
                ("ordnung", ordnung), ("datum", tag.Date), ("hund", hund));
        else if (eintrag.Result == ClubExamResult.Failed && mitPunkten)
            meldung = Meldung.Von("Ergebnis der {ordnung} vom {datum} für {hund} eingetragen: nicht bestanden, Punkte: {punkte}.",
                ("ordnung", ordnung), ("datum", tag.Date), ("hund", hund), ("punkte", punkte));
        else if (eintrag.Result == ClubExamResult.Failed)
            meldung = Meldung.Von("Ergebnis der {ordnung} vom {datum} für {hund} eingetragen: nicht bestanden.",
                ("ordnung", ordnung), ("datum", tag.Date), ("hund", hund));
        else
            meldung = Meldung.Von("Ergebnis der {ordnung} vom {datum} für {hund} eingetragen: nicht angetreten.",
                ("ordnung", ordnung), ("datum", tag.Date), ("hund", hund));
        return meldung;
    }

    /// <summary>Absage des Tages (oder sein Löschen): eine Nachricht je Mitglied-Team ohne Ergebnis.</summary>
    private async Task MeldeAbsageAsync(ClubExam tag, CancellationToken ct)
    {
        var eintraege = await db.ClubExamEntries.Where(e => e.ExamId == tag.Id).AsNoTracking().ToListAsync(ct);
        foreach (var (hundId, meldung) in await MitgliederMeldungenAsync(tag, eintraege, ct))
            await MeldeFamilieAsync(hundId, meldung, ct);
    }

    private async Task<List<(Guid DogId, Meldung Meldung)>> MitgliederMeldungenAsync(ClubExam tag, IReadOnlyList<ClubExamEntry> eintraege, CancellationToken ct)
    {
        var meldungen = new List<(Guid DogId, Meldung Meldung)>();
        foreach (var e in eintraege.Where(e => e.DogId is not null && e.Result == ClubExamResult.Open))
        {
            var (verein, hund, ordnung) = await NamenAsync(tag, e, ct);
            var absage = Meldung.Von("Die Prüfung bei {verein} am {datum} wurde abgesagt. Die Meldung von {hund} für die {ordnung} entfällt.",
                ("verein", verein), ("datum", tag.Date), ("hund", hund), ("ordnung", ordnung));
            meldungen.Add((e.DogId!.Value, absage));
        }
        return meldungen;
    }

    private async Task<(string Verein, string Hund, string Ordnung)> NamenAsync(ClubExam tag, ClubExamEntry eintrag, CancellationToken ct)
    {
        var verein = await db.Clubs.Where(c => c.Id == tag.ClubId).Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "";
        var hund = await db.Dogs.Where(d => d.Id == eintrag.DogId).Select(d => d.Name).FirstOrDefaultAsync(ct) ?? "";
        var ordnung = await db.Regulations.Where(r => r.Id == eintrag.RegulationId).Select(r => r.Name).FirstOrDefaultAsync(ct) ?? "";
        return (verein, hund, ordnung);
    }

    // ---------- Anzeige ----------

    private async Task<ClubExamDto> BaueAsync(ClubExam tag, CancellationToken ct)
    {
        var verein = await db.Clubs.Where(c => c.Id == tag.ClubId).Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "";
        var eintraege = await db.ClubExamEntries.Where(e => e.ExamId == tag.Id).AsNoTracking().ToListAsync(ct);
        var ordnungen = (await db.OrdnungenJePruefungAsync([tag.Id], ct)).GetValueOrDefault(tag.Id) ?? [];

        var punkte = await goals.GetHoechstpunkteAsync(ordnungen.Select(o => o.Id).ToList(), ct);
        ordnungen = ordnungen.Select(o => o with { MaxPoints = punkte.TryGetValue(o.Id, out var summe) ? summe : null }).ToList();
        var angeboten = tag.Regulations.Where(r => r.DeletedAt == null).Select(r => r.RegulationId).ToHashSet();

        var hundIds = eintraege.Where(e => e.DogId is not null).Select(e => e.DogId!.Value).Distinct().ToList();
        var hunde = await db.Dogs.Where(d => hundIds.Contains(d.Id)).Select(d => new { d.Id, d.Name, d.Breed, d.Birthday }).AsNoTracking().ToDictionaryAsync(d => d.Id, ct);
        var personen = await userLookup.FindByIdsAsync(eintraege.Where(e => e.MemberUserId is not null).Select(e => e.MemberUserId!.Value).Distinct().ToList(), ct);
        var reifen = await ReifenAsync(eintraege.Where(e => e.Result == ClubExamResult.Open && e.MemberUserId is not null && e.DogId is not null).ToList(), ct);

        var reihenfolge = ordnungen.Select((o, i) => (o.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var dtos = new List<ClubExamEntryDto>();
        foreach (var gruppe in eintraege.GroupBy(e => e.RegulationId).OrderBy(g => reihenfolge.GetValueOrDefault(g.Key, int.MaxValue)))
        {
            var platz = 0;
            foreach (var e in gruppe.OrderBy(e => e.StartOrder).ThenBy(e => e.CreatedAt))
            {
                platz++;
                if (e.MemberUserId is { } mitglied && personen.TryGetValue(mitglied, out var person) && e.DogId is { } hundId && hunde.TryGetValue(hundId, out var hund))
                {
                    dtos.Add(new ClubExamEntryDto(
                        e.Id, e.RegulationId, platz, ClubExamTeamKind.Member,
                        $"{person.FirstName} {person.LastName}".Trim(), hund.Name, hund.Breed, hund.Birthday, null,
                        reifen.GetValueOrDefault(e.Id), e.Result, e.Score, e.ResultNote));
                }
                else if (!string.IsNullOrEmpty(e.GuestHandlerName))
                {
                    dtos.Add(new ClubExamEntryDto(
                        e.Id, e.RegulationId, platz, ClubExamTeamKind.Guest,
                        e.GuestHandlerName, e.GuestDogName, null, null, e.GuestInfo,
                        null, e.Result, e.Score, e.ResultNote));
                }
                else
                {
                    dtos.Add(new ClubExamEntryDto(
                        e.Id, e.RegulationId, platz, ClubExamTeamKind.FormerMember,
                        null, null, null, null, null, null, e.Result, e.Score, e.ResultNote));
                }
            }
        }

        return new ClubExamDto(
            tag.Id, tag.ClubId, verein, tag.Date, tag.StartTime, tag.Location, tag.JudgeName, tag.RegistrationDeadline, tag.Note, tag.Status,
            ordnungen.Where(o => angeboten.Contains(o.Id)).ToList(), ordnungen, dtos);
    }

    /// <summary>
    /// Die Prüfungsreife je Eintrag, wo der Hund ein laufendes Ziel zur selben Ordnung hat. Über
    /// <see cref="PruefungsreifeLader"/> und ohne Betrachter: Die Theoriezeile bleibt der Person selbst.
    /// Die Prüfungsleitung bekommt davon nur Ampel, Zählung und die offenen und wackeligen Übungen.
    /// </summary>
    private async Task<Dictionary<Guid, ClubExamReifeDto>> ReifenAsync(List<ClubExamEntry> eintraege, CancellationToken ct)
    {
        var ergebnis = new Dictionary<Guid, ClubExamReifeDto>();
        if (eintraege.Count == 0) return ergebnis;

        var hundIds = eintraege.Select(e => e.DogId!.Value).Distinct().ToList();
        var ordnungIds = eintraege.Select(e => e.RegulationId).Distinct().ToList();
        var laufende = (await db.Goals
                .Where(g => hundIds.Contains(g.DogId) && g.RegulationId != null && ordnungIds.Contains(g.RegulationId.Value)
                    && g.Status == GoalStatus.Active && !g.IsCustom)
                .AsNoTracking()
                .ToListAsync(ct))
            .OrderBy(g => g.TargetDate).ThenBy(g => g.CreatedAt)
            .ToList();
        var jeTeam = laufende
            .GroupBy(g => (g.DogId, Ordnung: g.RegulationId!.Value))
            .ToDictionary(g => g.Key, g => g.First());

        var pausen = await db.PauseZeitraeumeAsync(hundIds, ct);
        var reife = await PruefungsreifeLader.LadeAsync(db, jeTeam.Values.ToList(), Heute(), pausen, null, ct);

        foreach (var e in eintraege)
        {
            if (!jeTeam.TryGetValue((e.DogId!.Value, e.RegulationId), out var ziel) || !reife.TryGetValue(ziel.Id, out var r)) continue;
            ergebnis[e.Id] = new ClubExamReifeDto(
                r.Ampel, r.Sicher, r.Wackelig, r.Offen,
                r.Uebungen.Where(u => u.Zustand != ReifeZustand.Sicher)
                    .Select(u => new ClubExamReifeUebungDto(u.ExerciseId, u.Name, u.NameEn, u.IsMandatory, u.Zustand))
                    .ToList());
        }
        return ergebnis;
    }
}
