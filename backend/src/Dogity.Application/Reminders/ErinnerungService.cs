using System.Data.Common;
using Dogity.Application.Abstractions;
using Dogity.Application.Community;
using Dogity.Application.Dogs;
using Dogity.Application.Notifications;
using Dogity.Application.Preferences;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Notifications;
using Dogity.Domain.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dogity.Application.Reminders;

/// <summary>
/// Holt die Daten für die Erinnerungen, lässt <see cref="Erinnerungsregeln"/> entscheiden,
/// was fällig ist, und versendet es über den NotificationService - als Meldung in der Glocke
/// plus Push, nie als Mail (kein <c>wichtig</c>). Der Text entsteht dort in der Sprache der
/// Empfänger:in.
///
/// Gegen Doppelversand: Vor dem Versand wird die Kennung in <see cref="SentReminder"/> gespeichert;
/// der eindeutige Index weist einen zweiten Versuch ab (Neustart, zwei Instanzen). Das ist
/// "höchstens einmal": Scheitert danach der Versand selbst, kommt die Erinnerung nicht mehr -
/// bei einer Erinnerung besser als eine doppelte.
///
/// Abgefragt wird nur, was gerade in Frage kommt (Termine des Folgetags, Fährten der letzten
/// Stunden, Fristen der nächsten 14 Tage, Trainer:innen mit heute fälligem Rhythmus) - außerhalb des Versandfensters (08-22 Uhr)
/// wird gar nichts geladen.
/// </summary>
public class ErinnerungService(
    IApplicationDbContext db,
    INotificationService notifications,
    TimeProvider time,
    ILogger<ErinnerungService> logger) : IErinnerungService
{
    /// <summary>SQLSTATE "unique_violation" (Postgres).</summary>
    private const string EindeutigerIndexVerletzt = "23505";

    /// <summary>Wie lange ein Eintrag in SentReminder gebraucht wird: länger als jede Erinnerung gültig ist.</summary>
    private static readonly TimeSpan Aufbewahrung = TimeSpan.FromDays(60);

    /// <summary>
    /// Fährten, deren Anlegen länger als so lange zurückliegt, kommen nicht mehr in Frage: Frist 24 Stunden
    /// plus Sicherheit für eine Aufnahme, die erst später gespeichert wurde. Nur zum Eingrenzen der Abfrage;
    /// die Frist selbst prüft <see cref="Erinnerungsregeln"/> am letzten Punkt.
    /// </summary>
    private static readonly TimeSpan FaehrtenRueckblick = TimeSpan.FromHours(30);

    public async Task<int> VersendeFaelligeAsync(CancellationToken ct = default)
    {
        var jetzt = time.GetUtcNow();

        await Abschnitt("Aufräumen", () => AufraeumenAsync(jetzt, ct));
        if (!Erinnerungsregeln.ImVersandfenster(jetzt)) return 0;

        // Jeder Abschnitt für sich: Ein Fehler bei den Fährten soll die Termine nicht mitnehmen.
        var gesendet = 0;
        gesendet += await Abschnitt("Termine", () => TermineAsync(jetzt, ct));
        gesendet += await Abschnitt("Fährten", () => FaehrtenAsync(jetzt, ct));
        gesendet += await Abschnitt("Rückmeldungen", () => RueckmeldungenAsync(jetzt, ct));
        gesendet += await Abschnitt("Gesundheit", () => GesundheitAsync(jetzt, ct));
        return gesendet;
    }

    private async Task<int> Abschnitt(string name, Func<Task<int>> lauf)
    {
        try
        {
            return await lauf();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Der Tick versucht es in zehn Minuten wieder; nichts weiter als der Abschnitt im Log.
            logger.LogError(ex, "Erinnerungen ({Abschnitt}) fehlgeschlagen.", name);
            return 0;
        }
    }

    private async Task<int> AufraeumenAsync(DateTimeOffset jetzt, CancellationToken ct)
    {
        var grenze = jetzt - Aufbewahrung;
        // In Stücken, damit ein Rückstau (etwa nach langem Stillstand) einen Tick nicht lange blockiert.
        var alt = await db.SentReminders.Where(r => r.CreatedAt < grenze).Take(500).ToListAsync(ct);
        if (alt.Count == 0) return 0;

        // Wirklich löschen und nicht weich: Die Zeilen sind reine Merkzettel und haben nach
        // Ablauf jeder Gültigkeit keinen Zweck mehr.
        db.SentReminders.RemoveRange(alt);
        await db.SaveChangesAsync(ct);
        return alt.Count;
    }

    // ---- a) Termine am Vortag ----

    private async Task<int> TermineAsync(DateTimeOffset jetzt, CancellationToken ct)
    {
        // Vor 18 Uhr gibt es nichts zu holen.
        if (Vereinszeit.Lokal(jetzt).Hour < Erinnerungsregeln.TerminAbStunde) return 0;

        var folgetag = Vereinszeit.Tag(jetzt).AddDays(1);
        var von = Vereinszeit.TagesBeginn(folgetag);
        var bis = Vereinszeit.TagesBeginn(folgetag.AddDays(1));

        var termine = await db.GroupTrainingSessions
            .Where(s => s.Status == GroupTrainingSessionStatus.Planned && s.StartsAt >= von && s.StartsAt < bis)
            .Select(s => new { s.Id, s.GroupId, s.StartsAt })
            .AsNoTracking()
            .ToListAsync(ct);
        if (termine.Count == 0) return 0;

        var gruppenIds = termine.Select(t => t.GroupId).Distinct().ToList();
        var gruppen = await db.Groups
            .Where(g => gruppenIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        // Dieselbe Sichtbarkeit wie GroupTrainingScheduleService.GetMemberScheduleAsync:
        // aktive Mitglieder der Gruppe. Eingeladene und Anfragende sehen den Termin nicht.
        var mitglieder = await db.GroupMembers
            .Where(m => gruppenIds.Contains(m.GroupId) && m.Status == GroupMemberStatus.Active)
            .Select(m => new { m.GroupId, m.UserId })
            .AsNoTracking()
            .ToListAsync(ct);

        var terminIds = termine.Select(t => t.Id).ToList();
        // Antworten gelten je Person (nicht je Hund): Hat die Person zugesagt, gilt das für
        // alle ihre Hunde - es gibt genau eine Erinnerung je Person und Termin.
        var antworten = (await db.GroupTrainingSessionResponses
                .Where(r => terminIds.Contains(r.GroupTrainingSessionId))
                .Select(r => new { r.GroupTrainingSessionId, r.UserId, r.IsAttending })
                .AsNoTracking()
                .ToListAsync(ct))
            .ToDictionary(r => (r.GroupTrainingSessionId, r.UserId), r => r.IsAttending);

        var nutzerIds = mitglieder.Select(m => m.UserId).Distinct().ToList();
        var erlaubt = await ErlaubtAsync(nutzerIds, ReminderType.SessionDayBefore, ct);

        var adressaten = new List<TerminAdressat>();
        foreach (var termin in termine)
        {
            if (!gruppen.TryGetValue(termin.GroupId, out var gruppenName)) continue; // Gruppe gelöscht

            foreach (var m in mitglieder.Where(m => m.GroupId == termin.GroupId))
            {
                var antwort = antworten.TryGetValue((termin.Id, m.UserId), out var kommt)
                    ? kommt ? TerminAntwort.Zugesagt : TerminAntwort.Abgesagt
                    : TerminAntwort.Offen;
                adressaten.Add(new TerminAdressat(
                    termin.Id, gruppenName, termin.StartsAt, Abgesagt: false, m.UserId, antwort, erlaubt.Contains(m.UserId)));
            }
        }

        var gesendet = await GesendeteAsync(ReminderType.SessionDayBefore, terminIds, folgetag, ct);
        return await VersendeAsync(Erinnerungsregeln.TerminErinnerungen(jetzt, adressaten, gesendet), ct);
    }

    // ---- b) Fährte noch nicht abgelaufen ----

    private async Task<int> FaehrtenAsync(DateTimeOffset jetzt, CancellationToken ct)
    {
        var seit = jetzt - FaehrtenRueckblick;
        var fruehestesEnde = jetzt - Erinnerungsregeln.FaehrteFrist;
        var spaetestesEnde = jetzt - Erinnerungsregeln.FaehrteNach;

        var faehrten = await db.GpsTracks
            .Where(t => t.CreatedAt >= seit && !t.WalkRuns.Any())
            .Select(t => new { t.Id, t.TrainingSessionId })
            .AsNoTracking()
            .ToListAsync(ct);
        if (faehrten.Count == 0) return 0;

        // Ende des Legens wie legeEnde im Frontend: der letzte AUTOMATISCHE Punkt; manuelle
        // Marker zählen nicht, sie können einen späteren Zeitstempel tragen.
        var faehrtenIds = faehrten.Select(f => f.Id).ToList();
        var enden = (await db.GpsPoints
                .Where(p => faehrtenIds.Contains(p.TrackId) && p.PointType == GpsPointType.Automatic)
                .GroupBy(p => p.TrackId)
                .Select(g => new { TrackId = g.Key, Ende = g.Max(p => p.Timestamp) })
                .ToListAsync(ct))
            .Where(e => e.Ende >= fruehestesEnde && e.Ende <= spaetestesEnde)
            .ToDictionary(e => e.TrackId, e => e.Ende);
        if (enden.Count == 0) return 0;

        var trainingIds = faehrten.Where(f => enden.ContainsKey(f.Id)).Select(f => f.TrainingSessionId).Distinct().ToList();
        var trainings = await db.TrainingSessions
            .Where(s => trainingIds.Contains(s.Id))
            .Select(s => new { s.Id, s.UserId, s.DogId })
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Id, ct);

        var hundeIds = trainings.Values.Select(s => s.DogId).Distinct().ToList();
        var hunde = await db.Dogs
            .Where(d => hundeIds.Contains(d.Id) && d.ArchivedAt == null)
            .Select(d => new { d.Id, d.Name, d.UsesOwnSports })
            .AsNoTracking()
            .ToDictionaryAsync(d => d.Id, ct);
        var pausen = await db.ActivePausesAsync(hundeIds, Vereinszeit.Tag(jetzt), ct);

        // Wer den Hund (noch) sehen darf: Besitzer:in oder betreuende Trainer:in - wie HasDogAccessAsync.
        // Sonst führte der Link ins Leere, etwa nach dem Austritt als Mitbesitzer:in.
        var zugang = new HashSet<(Guid UserId, Guid DogId)>();
        foreach (var o in await db.DogOwners.Where(o => hundeIds.Contains(o.DogId)).Select(o => new { o.UserId, o.DogId }).AsNoTracking().ToListAsync(ct))
            zugang.Add((o.UserId, o.DogId));
        foreach (var a in await db.TrainerAssignments.Where(a => hundeIds.Contains(a.DogId)).Select(a => new { a.TrainerId, a.DogId }).AsNoTracking().ToListAsync(ct))
            zugang.Add((a.TrainerId, a.DogId));

        var legerinnen = trainings.Values.Select(s => s.UserId).Distinct().ToList();
        var erlaubt = await ErlaubtAsync(legerinnen, ReminderType.TrackNotWalked, ct);
        var faehrteAngeboten = await FaehrteAngebotenAsync(legerinnen, hunde.Where(h => h.Value.UsesOwnSports).Select(h => h.Key).ToList(), ct);

        var kandidaten = new List<FaehrteKandidat>();
        foreach (var f in faehrten)
        {
            if (!enden.TryGetValue(f.Id, out var ende) || !trainings.TryGetValue(f.TrainingSessionId, out var training)) continue;
            if (!hunde.TryGetValue(training.DogId, out var hund)) continue;

            kandidaten.Add(new FaehrteKandidat(
                f.Id, training.Id, training.DogId, hund.Name, training.UserId, ende,
                Abgelaufen: false, // oben schon nach "keine Abläufe" gefiltert
                HundPausiert: pausen.ContainsKey(training.DogId),
                Erlaubt: erlaubt.Contains(training.UserId)
                    && zugang.Contains((training.UserId, training.DogId))
                    && faehrteAngeboten(training.UserId, training.DogId, hund.UsesOwnSports)));
        }

        var gesendet = await GesendeteAsync(ReminderType.TrackNotWalked, kandidaten.Select(k => k.FaehrteId).ToList(), SentReminder.KeinTag, ct);
        return await VersendeAsync(Erinnerungsregeln.FaehrtenErinnerungen(jetzt, kandidaten, gesendet), ct);
    }

    /// <summary>
    /// Ob der Person Fährte bei diesem Hund angeboten wird - dieselbe Antwort wie auf der Hundeseite
    /// und der Startseite (moduleEnabled(faehrte) &amp;&amp; laeuftFaehrte): Modul nicht abgewählt und
    /// eine Fährten-Sportart in den wirksamen Sportarten des Hundes (Vererbungsregel wie
    /// <c>PreferenceService.GetEffectiveDogSportsAsync</c>; leere Auswahl heißt "alles"). Sonst
    /// erinnerte die App an eine Fährte, die sie dort gar nicht mehr zeigt.
    /// </summary>
    private async Task<Func<Guid, Guid, bool, bool>> FaehrteAngebotenAsync(
        IReadOnlyCollection<Guid> nutzerIds, IReadOnlyCollection<Guid> hundeMitEigenenSportarten, CancellationToken ct)
    {
        var moduleAus = (await db.UserDisabledModules
                .Where(m => m.ModuleKey == Modules.Faehrte && nutzerIds.Contains(m.UserPreference!.UserId))
                .Select(m => m.UserPreference!.UserId)
                .ToListAsync(ct))
            .ToHashSet();

        var faehrtenSportarten = (await db.Sports
                .Where(s => Erinnerungsregeln.FaehrtenSportCodes.Contains(s.Code))
                .Select(s => s.Id)
                .ToListAsync(ct))
            .ToHashSet();

        var vomHund = (await db.DogSportSelections
                .Where(s => hundeMitEigenenSportarten.Contains(s.DogId))
                .Select(s => new { s.DogId, s.SportId })
                .ToListAsync(ct))
            .ToLookup(s => s.DogId, s => s.SportId);
        var vomMenschen = (await db.UserSportSelections
                .Where(s => nutzerIds.Contains(s.UserPreference!.UserId))
                .Select(s => new { s.UserPreference!.UserId, s.SportId })
                .ToListAsync(ct))
            .ToLookup(s => s.UserId, s => s.SportId);

        return (userId, hundId, eigeneSportarten) =>
            !moduleAus.Contains(userId)
            && Erinnerungsregeln.FaehrteAngeboten(eigeneSportarten ? vomHund[hundId].ToList() : vomMenschen[userId].ToList(), faehrtenSportarten);
    }

    // ---- c) Offene Rückmeldungen der Trainer:innen ----

    private async Task<int> RueckmeldungenAsync(DateTimeOffset jetzt, CancellationToken ct)
    {
        var lokal = Vereinszeit.Lokal(jetzt);
        var tagesBit = Erinnerungsregeln.TagesBit(lokal.DayOfWeek);
        var heute = DateOnly.FromDateTime(lokal);
        var stunde = lokal.Hour;
        // Ohne Einstellungszeile gelten die Vorgaben (Sonntag, 18 Uhr) - ob die heute zutreffen, steht schon hier fest.
        var vorgabeFaellig = tagesBit == Erinnerungsregeln.VorgabeTage && stunde >= Erinnerungsregeln.VorgabeStunde;

        // Trainer:in im Sinn dieser Erinnerung ist, wer Hunde betreut - nur dann gibt es etwas zu bewerten
        // (dieselbe Reichweite wie TrainerOpenCountsService und die Liste "Trainings bewerten").
        // Schon in der Abfrage auf heute fällige Personen eingegrenzt (Wochentag, Stunde, Schalter): Der Tick läuft
        // im Versandfenster rund 84-mal am Tag, da sollen nicht jedes Mal alle Betreuungen und Einstellungen kommen.
        var betreuungen = await db.TrainerAssignments
            .Where(a => db.UserPreferences.Any(p =>
                    p.UserId == a.TrainerId && p.ReminderTrainerSummary
                    && (p.TrainerSummaryDays & tagesBit) != 0 && p.TrainerSummaryHour <= stunde)
                || (vorgabeFaellig && !db.UserPreferences.Any(p => p.UserId == a.TrainerId)))
            .Select(a => new { a.TrainerId, a.DogId })
            .AsNoTracking()
            .ToListAsync(ct);
        if (betreuungen.Count == 0) return 0;

        var trainerIds = betreuungen.Select(b => b.TrainerId).Distinct().ToList();
        var einstellungen = await db.UserPreferences
            .Where(p => trainerIds.Contains(p.UserId))
            .Select(p => new { p.UserId, p.ReminderTrainerSummary, p.TrainerSummaryDays, p.TrainerSummaryHour })
            .AsNoTracking()
            .ToDictionaryAsync(p => p.UserId, ct);

        var kandidaten = trainerIds
            .Select(id => einstellungen.TryGetValue(id, out var p)
                ? new TrainerKandidat(id, p.ReminderTrainerSummary, p.TrainerSummaryDays, p.TrainerSummaryHour)
                : new TrainerKandidat(id, true, Erinnerungsregeln.VorgabeTage, Erinnerungsregeln.VorgabeStunde))
            .ToList();

        var gesendet = (await db.SentReminders
                .Where(r => r.Type == ReminderType.TrainerFeedbackSummary && r.DayKey == heute && trainerIds.Contains(r.UserId))
                .Select(r => new ErinnerungsSchluessel(r.Type, r.UserId, r.ReferenceId, r.DayKey))
                .ToListAsync(ct))
            .ToHashSet();

        var faellig = Erinnerungsregeln.FaelligeTrainer(jetzt, kandidaten, gesendet);

        var erinnerungen = new List<FaelligeErinnerung>();
        foreach (var trainer in faellig)
        {
            var hunde = betreuungen.Where(b => b.TrainerId == trainer.UserId).Select(b => b.DogId).Distinct().ToList();
            // Dieselbe Zählung wie die Zahl auf der Trainer-Übersicht und die Liste dahinter.
            var anzahl = await db.TrainingSessionsToRate(hunde).CountAsync(ct);
            if (Erinnerungsregeln.RueckmeldungsErinnerung(jetzt, trainer.UserId, anzahl) is { } e) erinnerungen.Add(e);
        }
        return await VersendeAsync(erinnerungen, ct);
    }

    // ---- d) Gesundheit: Fristen und Tierarzttermine der eigenen Hunde ----

    private async Task<int> GesundheitAsync(DateTimeOffset jetzt, CancellationToken ct)
    {
        var heute = Vereinszeit.Tag(jetzt);
        var folgetag = heute.AddDays(1);
        var vorlaufEnde = heute.AddDays(Erinnerungsregeln.GesundheitVorlaufTage);
        // Der Vortags-Termin kommt erst ab 18 Uhr; vorher muss er nicht einmal geladen werden.
        var terminFaellig = Vereinszeit.Lokal(jetzt).Hour >= Erinnerungsregeln.TerminAbStunde;

        var kandidaten = await db.DogHealthRecords
            .Where(r => (r.Kind == DogHealthKind.VetAppointment && terminFaellig && r.Date == folgetag)
                || (r.Kind != DogHealthKind.VetAppointment && r.DueDate >= heute && r.DueDate <= vorlaufEnde))
            .Select(r => new { r.Id, r.DogId, r.Kind, r.Title, r.Date, r.Time, r.DueDate })
            .AsNoTracking()
            .ToListAsync(ct);
        if (kandidaten.Count == 0) return 0;

        // Archivierte Hunde (etwa verstorbene) erinnern nicht mehr; gelöschte blendet der Filter aus.
        var hundeIds = kandidaten.Select(k => k.DogId).Distinct().ToList();
        var hunde = await db.Dogs
            .Where(d => hundeIds.Contains(d.Id) && d.ArchivedAt == null)
            .Select(d => new { d.Id, d.Name })
            .AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        if (hunde.Count == 0) return 0;

        // "Aktuell" heißt: der jüngste Eintrag je Hund, Art und Titel - das lässt sich nur im Blick auf
        // alle Einträge dieser Hunde sagen (eine erneuerte Impfung liegt außerhalb der Fristen-Abfrage).
        var fristKandidaten = kandidaten.Where(k => k.Kind != DogHealthKind.VetAppointment).Select(k => k.DogId).Distinct().ToList();
        var aktuell = Gesundheitsstand.Aktuelle(await db.DogHealthRecords
                .Where(r => fristKandidaten.Contains(r.DogId) && r.Kind != DogHealthKind.VetAppointment)
                .Select(r => new DogHealthRecord { Id = r.Id, DogId = r.DogId, Kind = r.Kind, Title = r.Title, Date = r.Date, CreatedAt = r.CreatedAt })
                .AsNoTracking()
                .ToListAsync(ct))
            .Select(r => r.Id)
            .ToHashSet();

        var aktiveHundeIds = hunde.Keys.ToList();
        var besitzer = await db.DogOwners
            .Where(o => aktiveHundeIds.Contains(o.DogId))
            .Select(o => new { o.DogId, o.UserId })
            .AsNoTracking()
            .ToListAsync(ct);
        var nutzerIds = besitzer.Select(b => b.UserId).Distinct().ToList();
        var erlaubt = await ErlaubtAsync(nutzerIds, ReminderType.HealthDueAhead, ct);
        // Das Modul abgeschaltet: Bereich und Hinweis sind weg, also auch die Erinnerung.
        var moduleAus = (await db.UserDisabledModules
                .Where(m => m.ModuleKey == Modules.Gesundheit && nutzerIds.Contains(m.UserPreference!.UserId))
                .Select(m => m.UserPreference!.UserId)
                .ToListAsync(ct))
            .ToHashSet();

        var adressaten = new List<GesundheitsAdressat>();
        foreach (var k in kandidaten)
        {
            if (!hunde.TryGetValue(k.DogId, out var hundName)) continue;
            foreach (var b in besitzer.Where(b => b.DogId == k.DogId))
            {
                adressaten.Add(new GesundheitsAdressat(
                    k.Id, k.DogId, hundName, k.Kind, k.Title, k.Date, k.Time, k.DueDate, aktuell.Contains(k.Id), b.UserId,
                    Erlaubt: erlaubt.Contains(b.UserId) && !moduleAus.Contains(b.UserId)));
            }
        }

        // Alle drei Arten auf einmal: Die Kennung enthält den Tag der Frist, ein Treffer am falschen Tag stört nicht.
        var bezugIds = kandidaten.Select(k => k.Id).ToList();
        var gesendet = (await db.SentReminders
                .Where(r => (r.Type == ReminderType.HealthDueAhead || r.Type == ReminderType.HealthDueToday || r.Type == ReminderType.HealthVetDayBefore)
                    && bezugIds.Contains(r.ReferenceId))
                .Select(r => new ErinnerungsSchluessel(r.Type, r.UserId, r.ReferenceId, r.DayKey))
                .ToListAsync(ct))
            .ToHashSet();

        return await VersendeAsync(Erinnerungsregeln.GesundheitsErinnerungen(jetzt, adressaten, gesendet), ct);
    }

    // ---- gemeinsam ----

    /// <summary>
    /// Die Personen unter <paramref name="nutzerIds"/>, die diese Art Erinnerung nicht abgeschaltet haben.
    /// Wer keine Einstellungszeile hat, hat nichts abgeschaltet.
    /// </summary>
    private async Task<HashSet<Guid>> ErlaubtAsync(IReadOnlyCollection<Guid> nutzerIds, ReminderType typ, CancellationToken ct)
    {
        var zeilen = await db.UserPreferences
            .Where(p => nutzerIds.Contains(p.UserId))
            .Select(p => new { p.UserId, p.ReminderSessions, p.ReminderTracks, p.ReminderHealth })
            .AsNoTracking()
            .ToListAsync(ct);
        var ausgeschaltet = zeilen
            .Where(z => !(typ switch
            {
                ReminderType.SessionDayBefore => z.ReminderSessions,
                ReminderType.TrackNotWalked => z.ReminderTracks,
                _ => z.ReminderHealth
            }))
            .Select(z => z.UserId)
            .ToHashSet();
        return nutzerIds.Where(id => !ausgeschaltet.Contains(id)).ToHashSet();
    }

    private async Task<IReadOnlySet<ErinnerungsSchluessel>> GesendeteAsync(
        ReminderType typ, IReadOnlyCollection<Guid> bezugIds, DateOnly tag, CancellationToken ct) =>
        (await db.SentReminders
            .Where(r => r.Type == typ && r.DayKey == tag && bezugIds.Contains(r.ReferenceId))
            .Select(r => new ErinnerungsSchluessel(r.Type, r.UserId, r.ReferenceId, r.DayKey))
            .ToListAsync(ct))
        .ToHashSet();

    private async Task<int> VersendeAsync(IEnumerable<FaelligeErinnerung> erinnerungen, CancellationToken ct)
    {
        var gesendet = 0;
        foreach (var e in erinnerungen)
        {
            // Erst merken, dann senden: Der eindeutige Index lässt nur einen Lauf durch.
            var zeile = new SentReminder
            {
                UserId = e.Schluessel.UserId,
                Type = e.Schluessel.Typ,
                ReferenceId = e.Schluessel.BezugId,
                DayKey = e.Schluessel.Tag
            };
            db.SentReminders.Add(zeile);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is DbException { SqlState: EindeutigerIndexVerletzt })
            {
                // Ein anderer Lauf (zweite Instanz, überlappender Tick) war schneller und sendet.
                db.SentReminders.Remove(zeile);
                continue;
            }

            // wichtig bleibt aus: Erinnerungen sind Glocke und Push, keine Mail.
            await notifications.CreateAsync(e.Schluessel.UserId, e.Meldung, e.Link, ct);
            gesendet++;
        }
        return gesendet;
    }
}
