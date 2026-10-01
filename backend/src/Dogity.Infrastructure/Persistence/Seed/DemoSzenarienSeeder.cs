using Dogity.Application.Community;
using Dogity.Application.Planning;
using Dogity.Application.Tracking;
using Dogity.Application.Training;
using Dogity.Application.Weather;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;
using Dogity.Domain.Sports;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dogity.Infrastructure.Persistence.Seed;

/// <summary>
/// Ergänzt die Demo-Konten (siehe <see cref="DemoDataSeeder"/>) um Szenarien,
/// mit denen sich ALLE Funktionen der App von Hand ausprobieren lassen:
/// Einladungslink, Prüfungsziele mit Fortschritt und Ergebnissen, Fährten mit
/// Trend, Trainer-Feedback, Gruppentermine mit Zu- und Absagen, Anmeldungen
/// zur Welpengruppe mit Anwesenheit.
///
/// Warum ein eigener Seeder und nicht noch mehr im DemoDataSeeder: Der bricht
/// ab, sobald das Trainer-Konto existiert - auf der Test-Umgebung gibt es die
/// Demo-Konten seit Juli, jede Erweiterung dort käme nie an. Dieser Seeder
/// läuft bei JEDEM Start (siehe Program.cs, nur in Development), und jedes
/// Szenario prüft für sich, ob seine Daten schon da sind (an festen Texten),
/// und legt nur Fehlendes an. Termine sind "rollend": bei jedem Start gibt es
/// wieder künftige.
///
/// Angefasst werden ausschließlich die Demo-Konten und der Demo-Verein; fehlt
/// ein Konto, entfällt das Szenario. Gelöscht wird nichts. Die Daten entstehen
/// über dieselben Dienste wie in der App (Rechte, Benachrichtigungen,
/// Fährten-Auswertung), nicht über selbst gesetzte Kennzahlen.
/// </summary>
public static class DemoSzenarienSeeder
{
    public const string NeulingEmail = "neuling@dogity.test";

    private const string VereinsName = "Hundesportverein Musterstadt e.V.";
    private const string GruppenName = "Welpengruppe Dienstag";

    // Feste Texte als Erkennungszeichen - an ihnen sieht jedes Szenario, ob es schon gelaufen ist.
    public const string NotizBhErreicht = "Begleithundeprüfung beim Verein Musterstadt.";
    public const string NotizUprErreicht = "Unterordnung Stufe 1 beim befreundeten Verein.";
    public const string NotizUprZiel = "Nächstes Ziel: Unterordnung Stufe 2.";
    public const string NotizFaehrteOffen = "Fährtenprüfung FPr 1 beim Verein Musterstadt.";
    public const string NotizFeedbackOhneReaktion = "Leinenführigkeit auf dem Vereinsgelände, viele andere Hunde in der Nähe.";
    public const string NotizFeedbackMitRueckfrage = "Verkehrstraining in der Innenstadt: Radfahrer, Kinderwagen, Straßenbahn.";

    public static readonly string[] FaehrtenKommentare =
    [
        "Erste Winkelfährte mit drei Gegenständen. Im zweiten Schenkel sehr weit nach links gekommen.",
        "Zweiter Versuch: ruhiger angegangen, am Winkel kurz gesucht.",
        "Feuchter Morgen, die Spur hielt gut. Nur am letzten Gegenstand ein Bogen.",
        "Fast sauber durchgelaufen, alle Gegenstände sicher verwiesen.",
    ];

    /// <returns>Die Szenarien, die fehlgeschlagen sind (leer = alles in Ordnung). Fehler werden zusätzlich protokolliert und stoppen den Start nicht.</returns>
    public static async Task<IReadOnlyList<string>> SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var lauf = await Lauf.ErmittleAsync(services, ct);
        // Ohne Demo-Konten (Produktion, frische Datenbank ohne DemoDataSeeder) gibt es nichts zu tun.
        return lauf is null ? [] : await lauf.AlleAsync(ct);
    }

    private sealed class Lauf
    {
        private readonly IServiceProvider services;
        private readonly ApplicationDbContext db;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly ILogger logger;

        private readonly ApplicationUser trainer;
        private readonly ApplicationUser max;
        private readonly ApplicationUser lisa;
        private readonly Club club;
        private readonly Group? gruppe;

        private Lauf(IServiceProvider services, ApplicationUser trainer, ApplicationUser max, ApplicationUser lisa, Club club, Group? gruppe)
        {
            this.services = services;
            db = services.GetRequiredService<ApplicationDbContext>();
            userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            logger = services.GetService<ILoggerFactory>()?.CreateLogger("DemoSzenarien")
                ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            this.trainer = trainer;
            this.max = max;
            this.lisa = lisa;
            this.club = club;
            this.gruppe = gruppe;
        }

        public static async Task<Lauf?> ErmittleAsync(IServiceProvider services, CancellationToken ct)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            var trainer = await userManager.FindByEmailAsync("trainer@dogity.test");
            var max = await userManager.FindByEmailAsync("mitglied1@dogity.test");
            var lisa = await userManager.FindByEmailAsync("mitglied2@dogity.test");
            if (trainer is null || max is null || lisa is null) return null;

            // Der Demo-Verein: der gleichnamige, in dem die Demo-Trainerin Trainerin ist.
            var club = await db.Clubs.FirstOrDefaultAsync(
                c => c.Name == VereinsName && db.ClubTrainers.Any(t => t.ClubId == c.Id && t.UserId == trainer.Id), ct);
            if (club is null) return null;

            var gruppe = await db.Groups.FirstOrDefaultAsync(
                g => g.ClubId == club.Id && g.TrainerId == trainer.Id && g.Name == GruppenName, ct);

            return new Lauf(services, trainer, max, lisa, club, gruppe);
        }

        public async Task<IReadOnlyList<string>> AlleAsync(CancellationToken ct)
        {
            var fehlgeschlagen = new List<string>();
            // Reihenfolge zählt: Die Anmeldungen hängen ihre Anwesenheit an die Termine.
            foreach (var (name, szenario) in new (string, Func<CancellationToken, Task>)[]
            {
                ("Verein", VereinAsync),
                ("Prüfungsziele", ZieleAsync),
                ("Fährten", FaehrtenAsync),
                ("Feedback", FeedbackAsync),
                ("Termine", TermineAsync),
                ("Anmeldungen", AnmeldungenAsync),
            })
            {
                try
                {
                    await szenario(ct);
                }
                catch (Exception ex)
                {
                    // Ein kaputtes Szenario darf weder die anderen noch den Start des Servers verhindern.
                    logger.LogError(ex, "Demo-Szenario {Szenario} ist fehlgeschlagen.", name);
                    db.ChangeTracker.Clear();
                    fehlgeschlagen.Add(name);
                }
            }
            return fehlgeschlagen;
        }

        // ---------------------------------------------------------------- a) Verein

        private async Task VereinAsync(CancellationToken ct)
        {
            var einladung = services.GetRequiredService<IClubInviteService>();

            var link = await einladung.GetAsync(trainer.Id, club.Id, ct);
            var code = link.Value?.Code;
            if (code is null)
            {
                var neu = await einladung.RegenerateAsync(trainer.Id, club.Id, ct);
                code = neu.Value?.Code;
            }
            if (code is null) return;

            var neuling = await userManager.FindByEmailAsync(NeulingEmail)
                ?? await DemoDataSeeder.CreateUserAsync(userManager, NeulingEmail, "Nina", "Neuling", [Roles.User]);

            // Nur beim allerersten Mal beitreten: Wer die Anfrage in der Demo ablehnt oder freigibt,
            // soll sie nicht beim nächsten Start wieder offen vorfinden.
            var hatteSchonAnfrage = await db.ClubMemberships.IgnoreQueryFilters()
                .AnyAsync(m => m.ClubId == club.Id && m.UserId == neuling.Id, ct);
            if (!hatteSchonAnfrage)
                await einladung.JoinAsync(neuling.Id, code, ct);
        }

        // ---------------------------------------------------------------- b) Prüfungsziele

        private async Task ZieleAsync(CancellationToken ct)
        {
            var hunde = await HundeVonMaxAsync(ct);
            var emma = await EmmaAsync(hunde, ct);

            var heute = DateOnly.FromDateTime(DateTime.UtcNow);

            // Zwei bestandene Prüfungen: BH ohne Punkte (nur bestanden/nicht bestanden) und
            // UPr 1 mit Punkten unter dem Höchstwert. Daraus ergeben sich "Leistungen",
            // Druckansicht und die Folgestufe.
            await ErreichtesZielAsync(emma, "BH", "BH", heute.AddDays(-150), null,
                "Teil B ohne Beanstandung, nur die Ablenkung in der Gruppe war knapp.", NotizBhErreicht, ct);
            await ErreichtesZielAsync(emma, "UPR", "FCI-UPr 1", heute.AddDays(-60), 86,
                "Freifolge und Bringen sauber, beim Klettersprung gezögert.", NotizUprErreicht, ct);

            // Aktives Ziel in rund fünf Wochen, mit Plan und angefangener Woche: Die Zielkarte
            // auf der Startseite zeigt Fortschritt. Die Anker liegen drei Tage zurück, damit die
            // Woche 1 noch einige Tage "laufende Woche" bleibt.
            var (aktiv, aktivNeu) = await ZielMitPlanAsync(emma, "UPR", "FCI-UPr 2", 32, NotizUprZiel, ct);
            if (aktiv is not null)
            {
                if (aktivNeu) await ZielRueckdatierenAsync(aktiv, tage: 3, ct);
                await FortschrittEintragenAsync(emma!, aktiv, ct);
            }

            // Prüfung war gestern, Ergebnis fehlt: Zustand "Ergebnis eintragen" - beim zweiten
            // Hund, damit die Hundeliste beides nebeneinander zeigt.
            var (offen, offenNeu) = await ZielMitPlanAsync(emma, "FPR", "FCI-FPr 1", 28, NotizFaehrteOffen, ct);
            if (offen is not null && offenNeu)
            {
                await ZielRueckdatierenAsync(offen, tage: 28, ct);
                offen.TargetDate = heute.AddDays(-1);
                await db.SaveChangesAsync(ct);
            }
        }

        /// <summary>
        /// Max' zweiter Hund. Einen zweiten Hund gibt es in den Demo-Daten sonst nicht, und ohne ihn
        /// entfielen Leistungen, Fortschritt und der Zustand "Ergebnis eintragen" in der Hundeliste.
        /// </summary>
        private async Task<Dog?> EmmaAsync(List<Dog> hunde, CancellationToken ct)
        {
            var emma = hunde.FirstOrDefault(h => h.Name == "Emma");
            if (emma is not null) return emma;

            emma = new Dog
            {
                Name = "Emma",
                Breed = "Australian Shepherd",
                Gender = DogGender.Female,
                Birthday = new DateOnly(2023, 5, 20),
            };
            db.Dogs.Add(emma);
            db.DogOwners.Add(new DogOwner { DogId = emma.Id, UserId = max.Id });
            await db.SaveChangesAsync(ct);
            return emma;
        }

        private async Task<List<Dog>> HundeVonMaxAsync(CancellationToken ct) =>
            await db.Dogs.Where(d => db.DogOwners.Any(o => o.DogId == d.Id && o.UserId == max.Id)).ToListAsync(ct);

        private async Task ErreichtesZielAsync(
            Dog? hund, string sportCode, string ordnung, DateOnly pruefungstag, int? punkte, string bemerkung, string notiz, CancellationToken ct)
        {
            if (hund is null) return;
            // Ohne Query-Filter: Ein in der Demo gelöschtes Ziel soll beim nächsten Start nicht wiederkommen.
            if (await db.Goals.IgnoreQueryFilters().AnyAsync(g => g.DogId == hund.Id && g.Notes == notiz, ct)) return;

            var (sport, regulation) = await KatalogAsync(sportCode, ordnung, ct);
            if (sport is null || regulation is null) return;

            var vorlauf = pruefungstag.AddDays(-56).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var goal = new Goal
            {
                DogId = hund.Id,
                SportId = sport.Id,
                RegulationId = regulation.Id,
                TargetDate = pruefungstag,
                Status = GoalStatus.Achieved,
                ExamDate = pruefungstag,
                ExamScore = punkte,
                ExamNote = bemerkung,
                Notes = notiz,
                CreatedAt = new DateTimeOffset(vorlauf, TimeSpan.Zero),
            };
            // Wie jedes Ziel mit einem (hier leeren) Plan: Die Oberfläche rechnet damit.
            goal.TrainingPlan = new TrainingPlan { GoalId = goal.Id, Goal = goal, GeneratedAt = goal.CreatedAt, CreatedAt = goal.CreatedAt };
            db.Goals.Add(goal);
            await db.SaveChangesAsync(ct);
        }

        /// <summary>Legt ein Ziel über den GoalService an - samt generiertem Plan - oder liefert das vorhandene (Neu = false).</summary>
        private async Task<(Goal? Ziel, bool Neu)> ZielMitPlanAsync(Dog? hund, string sportCode, string ordnung, int tageBisZiel, string notiz, CancellationToken ct)
        {
            if (hund is null) return (null, false);

            var vorhanden = await db.Goals.IgnoreQueryFilters().Include(g => g.TrainingPlan)
                .FirstOrDefaultAsync(g => g.DogId == hund.Id && g.Notes == notiz, ct);
            if (vorhanden is not null) return (vorhanden.DeletedAt is null ? vorhanden : null, false);

            var (sport, regulation) = await KatalogAsync(sportCode, ordnung, ct);
            if (sport is null || regulation is null) return (null, false);

            var heute = DateOnly.FromDateTime(DateTime.UtcNow);
            var ergebnis = await services.GetRequiredService<IGoalService>().CreateAsync(
                max.Id, new CreateGoalRequest(hund.Id, sport.Id, regulation.Id, heute.AddDays(tageBisZiel), notiz), ct);
            if (!ergebnis.Succeeded)
            {
                logger.LogWarning("Demo-Ziel {Ordnung} konnte nicht angelegt werden: {Fehler}", ordnung, string.Join("; ", ergebnis.Errors));
                return (null, false);
            }

            return (await db.Goals.Include(g => g.TrainingPlan).FirstAsync(g => g.Id == ergebnis.Value!.Id, ct), true);
        }

        private async Task ZielRueckdatierenAsync(Goal goal, int tage, CancellationToken ct)
        {
            var beginn = DateTimeOffset.UtcNow.AddDays(-tage);
            goal.CreatedAt = beginn;
            if (goal.TrainingPlan is not null)
            {
                goal.TrainingPlan.GeneratedAt = beginn;
                goal.TrainingPlan.CreatedAt = beginn;
            }
            await db.SaveChangesAsync(ct);
        }

        /// <summary>Trainingseinträge zur laufenden Woche: die Übungen des ersten Trainingstags ganz, die nächste angefangen.</summary>
        private async Task FortschrittEintragenAsync(Dog hund, Goal goal, CancellationToken ct)
        {
            if (goal.TrainingPlan is null) return;
            var woche = await db.TrainingPlanItems
                .Where(i => i.TrainingPlanId == goal.TrainingPlan.Id && i.WeekNumber == 1 && !i.IsRestWeek && i.ExerciseId != null)
                .OrderBy(i => i.DayIndex).ThenBy(i => i.CreatedAt)
                .ToListAsync(ct);
            if (woche.Count == 0) return;

            var ids = woche.Select(i => i.Id).ToList();
            if (await db.TrainingExercises.AnyAsync(e => e.TrainingPlanItemId != null && ids.Contains(e.TrainingPlanItemId.Value), ct)) return;

            var training = services.GetRequiredService<ITrainingService>();
            var heute = DateOnly.FromDateTime(DateTime.UtcNow);
            var ersterTag = woche[0].DayIndex;

            var ganz = woche.Where(i => i.DayIndex == ersterTag).ToList();
            var angefangen = woche.FirstOrDefault(i => i.DayIndex != ersterTag);

            await TrainingMitPlanEintraegenAsync(training, hund, heute.AddDays(-2), 40, "Plan der Woche, erster Trainingstag.", ganz, alle: true, ct);
            if (angefangen is not null)
                await TrainingMitPlanEintraegenAsync(training, hund, heute.AddDays(-1), 20, "Eine Übung aus dem zweiten Trainingstag angefangen.", [angefangen], alle: false, ct);
        }

        private async Task TrainingMitPlanEintraegenAsync(
            ITrainingService training, Dog hund, DateOnly tag, int minuten, string notiz, List<TrainingPlanItem> eintraege, bool alle, CancellationToken ct)
        {
            var uebungen = new List<CreateTrainingExerciseRequest>();
            var nummer = 0;
            foreach (var eintrag in eintraege)
            {
                // Je Durchgang eine Zeile - so zählt der Plan seinen Fortschritt.
                var durchgaenge = alle ? eintrag.RepetitionsTarget : 1;
                for (var i = 0; i < Math.Max(1, durchgaenge); i++)
                {
                    var bewertung = new[] { 4, 3, 5, 4, 3 }[nummer++ % 5];
                    uebungen.Add(new CreateTrainingExerciseRequest(
                        eintrag.ExerciseId, bewertung, eintrag.Difficulty ?? ExerciseDifficulty.Beginner, bewertung >= 3, null, eintrag.Id));
                }
            }

            var ergebnis = await training.CreateAsync(max.Id, new CreateTrainingSessionRequest(hund.Id, tag, minuten, notiz, uebungen), ct);
            if (!ergebnis.Succeeded)
                logger.LogWarning("Demo-Training mit Planbezug konnte nicht angelegt werden: {Fehler}", string.Join("; ", ergebnis.Errors));
        }

        private async Task<(Sport? Sport, Regulation? Regulation)> KatalogAsync(string sportCode, string ordnung, CancellationToken ct)
        {
            var sport = await db.Sports.FirstOrDefaultAsync(s => s.Code == sportCode, ct);
            if (sport is null) return (null, null);
            var regulation = await db.Regulations.FirstOrDefaultAsync(r => r.SportId == sport.Id && r.Name == ordnung, ct);
            return (sport, regulation);
        }

        // ---------------------------------------------------------------- c) Fährten

        private async Task FaehrtenAsync(CancellationToken ct)
        {
            var bello = (await HundeVonMaxAsync(ct)).FirstOrDefault(h => h.Name == "Bello");
            if (bello is null) return;

            // Eigene Instanz ohne Wetterabruf: Der echte Dienst fragte beim Start des Servers das
            // Netz nach Wetter von vor Wochen. Die Temperaturen setzen wir selbst (unten), in
            // denselben Feldern, die das Wetter-Feature füllt.
            var service = new GpsTrackService(db, new OhneWetter());
            var heute = DateOnly.FromDateTime(DateTime.UtcNow);

            var tage = new[] { -26, -19, -12, -6 };
            var abweichung = new[] { 4.4, 3.0, 1.8, 0.9 };
            var alter = new[] { 25, 40, 55, 60 };
            var untergrund = new[] { "Wiese", "Wiese", "Acker", "Waldweg" };
            var wetter = new[] { "sonnig, trocken", "bewölkt", "feuchter Morgen, leichter Regen davor", "sonnig, trocken" };

            for (var i = 0; i < FaehrtenKommentare.Length; i++)
            {
                var kommentar = FaehrtenKommentare[i];
                var vorhanden = await db.GpsTracks.IgnoreQueryFilters().AnyAsync(
                    t => t.Comment == kommentar && db.TrainingSessions.IgnoreQueryFilters().Any(s => s.Id == t.TrainingSessionId && s.DogId == bello.Id), ct);
                if (vorhanden) continue;

                var tag = heute.AddDays(tage[i]);
                var gelegtUm = new DateTimeOffset(tag.ToDateTime(new TimeOnly(15, 30), DateTimeKind.Utc));
                var (gelegt, abgelaufen) = DemoFaehrtenGeometrie.Erzeuge(i, gelegtUm, TimeSpan.FromMinutes(alter[i]), abweichung[i]);

                var track = await service.CreateAsync(max.Id, new CreateGpsTrackRequest(
                    TrainingSessionId: null, LengthMeters: Math.Round(DemoFaehrtenGeometrie.LaengeMeter), AgeMinutes: alter[i],
                    Surface: untergrund[i], Weather: wetter[i], Wind: "schwach", Comment: kommentar, Points: gelegt,
                    DogId: bello.Id, Date: tag, DurationMinutes: 45), ct);
                if (!track.Succeeded)
                {
                    logger.LogWarning("Demo-Fährte {Nummer} konnte nicht angelegt werden: {Fehler}", i + 1, string.Join("; ", track.Errors));
                    continue;
                }

                // Der Ablauf wird wie in der App gespeichert und dabei vom GpsTrackEvaluator ausgewertet.
                var lauf = await service.AddWalkRunAsync(max.Id, track.Value!.Id, new CreateGpsWalkRunRequest(
                    Math.Round(DemoFaehrtenGeometrie.LaengeMeter), null, abgelaufen), ct);
                if (!lauf.Succeeded)
                {
                    logger.LogWarning("Demo-Ablauf {Nummer} konnte nicht gespeichert werden: {Fehler}", i + 1, string.Join("; ", lauf.Errors));
                    continue;
                }

                // Wetter zum Legen und Suchen, wie es das Wetter-Feature speichert.
                var w = DemoFaehrtenGeometrie.Wetter[i];
                var zeile = await db.GpsTracks.FirstAsync(t => t.Id == track.Value.Id, ct);
                zeile.LaidTemperatureC = w.LegenC;
                zeile.LaidRelativeHumidity = w.LegenFeuchte;
                zeile.LaidWindSpeedKmh = w.LegenWind;
                zeile.LaidWeatherCode = w.LegenCode;
                zeile.SearchTemperatureC = w.SuchenC;
                zeile.SearchRelativeHumidity = w.SuchenFeuchte;
                zeile.SearchWindSpeedKmh = w.SuchenWind;
                zeile.SearchWeatherCode = w.SuchenCode;
                zeile.WeatherFetchedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }

        private sealed class OhneWetter : IWeatherEnrichmentService
        {
            public Task EnrichTrackAsync(GpsTrack track, CancellationToken ct = default) => Task.CompletedTask;
            public Task EnrichSessionAsync(TrainingSession session, CancellationToken ct = default) => Task.CompletedTask;
        }

        // ---------------------------------------------------------------- d) Feedback

        private async Task FeedbackAsync(CancellationToken ct)
        {
            var bello = (await HundeVonMaxAsync(ct)).FirstOrDefault(h => h.Name == "Bello");
            if (bello is null) return;

            var bh = await db.Sports.FirstOrDefaultAsync(s => s.Code == "BH", ct);
            if (bh is null) return;
            var uebungen = await db.Exercises.Where(e => e.SportId == bh.Id && e.ClubId == null).OrderBy(e => e.Name).Take(40).ToListAsync(ct);
            var fussarbeit = uebungen.FirstOrDefault(e => e.Name == "Fußarbeit") ?? uebungen.FirstOrDefault();
            var verkehr = uebungen.FirstOrDefault(e => e.Name == "Verhalten im Verkehr") ?? uebungen.LastOrDefault();
            if (fussarbeit is null || verkehr is null) return;

            var training = services.GetRequiredService<ITrainingService>();
            var heute = DateOnly.FromDateTime(DateTime.UtcNow);

            // Frisches Feedback, noch keine Reaktion: Die Benachrichtigung führt auf ?eintrag=.
            var erste = await TrainingAnlegenAsync(training, bello, heute.AddDays(-9), NotizFeedbackOhneReaktion, 40,
                [Uebung(fussarbeit, 3, "Beim Abbiegen noch unsauber."), Uebung(verkehr, 4, null)], ct);
            if (erste is not null)
                await training.SetFeedbackAsync(trainer.Id, erste.Value,
                    new SetFeedbackRequest("Die Fußarbeit wird ruhiger. Gib das Hörzeichen beim Abbiegen etwas früher, bevor du dich bewegst - dann bleibt Bello sauber an deinem Bein."), ct);

            // Feedback mit "Verstanden" und Rückfrage: Der Trainer bekommt eine Benachrichtigung.
            var zweite = await TrainingAnlegenAsync(training, bello, heute.AddDays(-11), NotizFeedbackMitRueckfrage, 50,
                [Uebung(verkehr, 5, "Radfahrer völlig gelassen."), Uebung(fussarbeit, 4, null)], ct);
            if (zweite is not null)
            {
                await training.SetFeedbackAsync(trainer.Id, zweite.Value,
                    new SetFeedbackRequest("Sehr gut gemeistert: Bello bleibt bei Radfahrern gelassen. Übe als Nächstes Begegnungen mit Kinderwagen und der Straßenbahn-Haltestelle."), ct);
                await training.ReplyToFeedbackAsync(max.Id, zweite.Value,
                    new FeedbackReplyRequest(FeedbackReaction.Understood, "Verstanden, danke! Wie lange soll ich an der Haltestelle stehen bleiben, bevor wir weitergehen?"), ct);
            }
        }

        private static CreateTrainingExerciseRequest Uebung(Exercise uebung, int bewertung, string? notiz) =>
            new(uebung.Id, bewertung, uebung.Difficulty, bewertung >= 3, notiz);

        /// <summary>Legt das Training an, falls es (an seiner Notiz erkennbar) noch fehlt; liefert dann seine Id, sonst null.</summary>
        private async Task<Guid?> TrainingAnlegenAsync(
            ITrainingService training, Dog hund, DateOnly tag, string notiz, int minuten, List<CreateTrainingExerciseRequest> uebungen, CancellationToken ct)
        {
            if (await db.TrainingSessions.IgnoreQueryFilters().AnyAsync(s => s.DogId == hund.Id && s.Notes != null && s.Notes.Contains(notiz), ct))
                return null;

            var ergebnis = await training.CreateAsync(max.Id, new CreateTrainingSessionRequest(hund.Id, tag, minuten, notiz, uebungen), ct);
            if (!ergebnis.Succeeded)
            {
                logger.LogWarning("Demo-Training konnte nicht angelegt werden: {Fehler}", string.Join("; ", ergebnis.Errors));
                return null;
            }
            return ergebnis.Value!.Id;
        }

        // ---------------------------------------------------------------- e) Gruppentermine

        private async Task TermineAsync(CancellationToken ct)
        {
            if (gruppe is null) return;
            var termine = services.GetRequiredService<IGroupTrainingScheduleService>();

            var sitzungen = await db.GroupTrainingSessions.Where(s => s.GroupId == gruppe.Id).AsNoTracking().ToListAsync(ct);
            var jetzt = DateTimeOffset.UtcNow;
            var heute = Vereinszeit.Heute();

            // Die Welpengruppe trifft sich dienstags um 18 Uhr: die letzten drei und die nächsten zwei Dienstage.
            var inhalte = new[]
            {
                new[] { "Begrüßung und Namenstraining", "Blickkontakt aufbauen", "Leinenführigkeit im Kreis" },
                new[] { "Sitz und Platz mit Futter locken", "Begegnungen an lockerer Leine", "Entspannung auf der Decke" },
                new[] { "Rückruf im umzäunten Platz", "Gegenstände anzeigen und bringen", "Ruhiges Warten an der Tür" },
            };
            var kuenftige = 0;
            for (var tag = heute.AddDays(-22); tag <= heute.AddDays(14); tag = tag.AddDays(1))
            {
                if (tag.DayOfWeek != DayOfWeek.Tuesday) continue;
                var beginn = LokalZuUtc(tag, new TimeOnly(18, 0));
                var vergangen = beginn <= jetzt;
                if (vergangen ? beginn < jetzt.AddDays(-21) : beginn > jetzt.AddDays(14)) continue;
                if (!vergangen) kuenftige++;
                if (sitzungen.Any(s => Vereinszeit.Tag(s.StartsAt) == tag)) continue;

                // Der zweite künftige Termin ist ein besonderer: anderer Treffpunkt, mit Hinweis.
                var sonderfall = !vergangen && kuenftige == 2;
                var ergebnis = await termine.CreateSessionAsync(trainer.Id, club.Id, new CreateSessionRequest(
                    gruppe.Id, GroupTrainingCategory.Puppy, beginn, 60,
                    sonderfall ? "Waldparkplatz Musterstadt" : null,
                    sonderfall ? "Heute raus aus dem Platz: Spaziergang mit Begegnungen. Bitte Futterbeutel und Kotbeutel mitbringen." : null,
                    [trainer.Id],
                    inhalte[Math.Abs(tag.DayNumber / 7) % inhalte.Length].Select(t => new SessionContentInput(FreeText: t)).ToList()), ct);
                if (!ergebnis.Succeeded)
                    logger.LogWarning("Demo-Termin am {Tag} konnte nicht angelegt werden: {Fehler}", tag, string.Join("; ", ergebnis.Errors));
            }

            // Ein künftiger, abgesagter Termin - donnerstags, damit er die Dienstage nicht belegt.
            // Rollend: Ist der letzte vorbei, kommt beim nächsten Start ein neuer.
            if (!sitzungen.Any(s => s.Status == GroupTrainingSessionStatus.Cancelled && s.StartsAt > jetzt))
            {
                var donnerstag = heute.AddDays(5);
                while (donnerstag.DayOfWeek != DayOfWeek.Thursday) donnerstag = donnerstag.AddDays(1);
                if (!sitzungen.Any(s => Vereinszeit.Tag(s.StartsAt) == donnerstag))
                {
                    var angelegt = await termine.CreateSessionAsync(trainer.Id, club.Id, new CreateSessionRequest(
                        gruppe.Id, GroupTrainingCategory.Puppy, LokalZuUtc(donnerstag, new TimeOnly(17, 30)), 60,
                        null, "Zusatztermin Spaziergang - fällt aus.", [trainer.Id],
                        [new SessionContentInput(FreeText: "Spaziergang im Park")]), ct);
                    if (angelegt.Succeeded)
                        await termine.CancelSessionAsync(trainer.Id, angelegt.Value!.Id, ct);
                }
            }

            // Beim nächsten Termin: Max sagt zu, Lisa sagt ab (und die Trainerin bekommt die Absage).
            var naechster = await db.GroupTrainingSessions
                .Where(s => s.GroupId == gruppe.Id && s.Status == GroupTrainingSessionStatus.Planned && s.StartsAt > jetzt)
                .OrderBy(s => s.StartsAt)
                .FirstOrDefaultAsync(ct);
            if (naechster is null) return;

            var antworten = await db.GroupTrainingSessionResponses
                .AnyAsync(r => r.GroupTrainingSessionId == naechster.Id && (r.UserId == max.Id || r.UserId == lisa.Id), ct);
            if (!antworten)
            {
                await termine.RespondAsync(max.Id, naechster.Id, true, ct);
                await termine.RespondAsync(lisa.Id, naechster.Id, false, ct);
            }
        }

        private static DateTimeOffset LokalZuUtc(DateOnly tag, TimeOnly uhrzeit) =>
            new(TimeZoneInfo.ConvertTimeToUtc(tag.ToDateTime(uhrzeit, DateTimeKind.Unspecified), Vereinszeit.Zone), TimeSpan.Zero);

        // ---------------------------------------------------------------- f) Anmeldungen zur Welpengruppe

        private sealed record Anmeldung(
            string Vorname, string Nachname, string Hund, string Rasse, int Wochen, string Telefon,
            GroupRegistrationSource Quelle, int TageHer, bool Bezahlt, int Anwesend, string? Notiz = null);

        private static readonly Anmeldung[] Anmeldungen =
        [
            new("Julia", "Brandt", "Mila", "Golden Retriever", 9, "0711 555 0101", GroupRegistrationSource.Form, 20, true, 3),
            new("Markus", "Keller", "Odin", "Deutscher Schäferhund", 12, "0711 555 0102", GroupRegistrationSource.Form, 19, true, 3),
            new("Sabine", "Roth", "Lotta", "Mischling", 14, "0711 555 0103", GroupRegistrationSource.Import, 15, true, 2),
            new("Tobias", "Engel", "Finn", "Australian Shepherd", 8, "0711 555 0104", GroupRegistrationSource.Manual, 12, false, 1,
                "Finn ist bei fremden Männern ängstlich - bitte langsam heranführen."),
            new("Franziska", "Neumann", "Bruno", "Labrador Retriever", 19, "0711 555 0105", GroupRegistrationSource.Import, 10, false, 2),
            new("Daniel", "Vogel", "Emil", "Border Collie", 11, "0711 555 0106", GroupRegistrationSource.Manual, 8, true, 1),
        ];

        private async Task AnmeldungenAsync(CancellationToken ct)
        {
            if (gruppe is null) return;
            var anmeldungen = services.GetRequiredService<IGroupRegistrationService>();

            var link = await anmeldungen.GetLinkAsync(trainer.Id, gruppe.Id, ct);
            var code = link.Value?.Code;
            if (code is null)
            {
                var neu = await anmeldungen.RegenerateLinkAsync(trainer.Id, gruppe.Id, ct);
                code = neu.Value?.Code;
            }
            if (code is null) return;

            // Auch gelöschte zählen: Was ein Tester in der Demo löscht, soll nicht beim Neustart zurückkehren.
            var vorhandene = await db.GroupRegistrations.IgnoreQueryFilters().Where(r => r.GroupId == gruppe.Id)
                .Select(r => new { r.Phone, r.DogName }).ToListAsync(ct);
            bool Vorhanden(string telefon, string hund) => vorhandene.Any(v => v.Phone == telefon && v.DogName == hund);

            // Tage, an denen die Gruppe sich getroffen hat (für die Anwesenheit), neueste zuerst.
            var jetzt = DateTimeOffset.UtcNow;
            var heute = Vereinszeit.Heute();
            var treffen = (await db.GroupTrainingSessions
                    .Where(s => s.GroupId == gruppe.Id && s.Status == GroupTrainingSessionStatus.Planned && s.StartsAt <= jetzt)
                    .Select(s => s.StartsAt).ToListAsync(ct))
                .Select(Vereinszeit.Tag).Where(t => t <= heute).Distinct().OrderByDescending(t => t).Take(3).ToList();

            foreach (var a in Anmeldungen)
            {
                if (Vorhanden(a.Telefon, a.Hund)) continue;

                var angemeldet = DateTimeOffset.UtcNow.AddDays(-a.TageHer);
                var zeile = new GroupRegistration
                {
                    GroupId = gruppe.Id,
                    FirstName = a.Vorname,
                    LastName = a.Nachname,
                    DogName = a.Hund,
                    DogBreed = a.Rasse,
                    DogBirthDate = heute.AddDays(-7 * a.Wochen),
                    Phone = a.Telefon,
                    Source = a.Quelle,
                    RegisteredAt = angemeldet,
                    CreatedAt = angemeldet,
                    Notes = a.Notiz,
                };
                db.GroupRegistrations.Add(zeile);
                await db.SaveChangesAsync(ct);

                // Bezahlt und Anwesenheit über den Dienst: derselbe Weg wie beim Abhaken in der App.
                if (a.Bezahlt)
                    await anmeldungen.SetPaidAsync(trainer.Id, gruppe.Id, zeile.Id, true, ct);
                foreach (var tag in treffen.Take(a.Anwesend))
                    await anmeldungen.SetAttendanceAsync(trainer.Id, gruppe.Id, zeile.Id, tag, true, ct);
            }

            // Eine Anmeldung von heute, über das öffentliche Formular - mit der Benachrichtigung an die Trainerin.
            if (!Vorhanden("0711 555 0107", "Nala"))
            {
                var formular = await services.GetRequiredService<IGroupRegistrationFormService>().SubmitAsync(code, new PublicRegistrationRequest(
                    "Anja", "Lorenz", "Nala", "Rhodesian Ridgeback", heute.AddDays(-70), "0711 555 0107", Consent: true, Website: null), ct);
                if (!formular.Succeeded)
                    logger.LogWarning("Demo-Anmeldung über das Formular wurde abgelehnt: {Fehler}", string.Join("; ", formular.Errors));
            }
        }
    }
}
