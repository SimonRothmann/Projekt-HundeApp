using Dogity.Application.Abstractions;
using Dogity.Application.Account;
using Dogity.Application.Community;
using Dogity.Application.Planning;
using Dogity.Application.Preferences;
using Dogity.Application.Tracking;
using Dogity.Application.Training;
using Dogity.Application.Weather;
using Dogity.Domain.Betrieb;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Tracking;
using Dogity.Infrastructure.Persistence;
using Dogity.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dogity.Infrastructure.Identity;

/// <summary>
/// Legt Gastzugänge an ("App ausprobieren ohne Anmeldung") und räumt abgelaufene wieder ab -
/// siehe docs/GASTZUGANG.md.
///
/// Ein Gast bekommt ein eigenes Konto mit Hund, drei Wochen Trainings, Ziel mit Plan und einer
/// ausgewerteten Fährte, dazu einen erfundenen Verein, in dem er Trainer:in mit Verwaltung ist
/// (mit erfundenen Mitgliedern, einer Gruppe samt Termin und einer offenen Beitrittsanfrage).
/// Die Daten entstehen über dieselben Dienste wie in der App; nur Hund, Mitglieder und Verein
/// werden direkt angelegt. Alles trägt <c>GuestUntil</c> - daran erkennen Abschottung,
/// Statistik und Löschdienst, was zum Gast gehört.
///
/// Die erfundenen Mitglieder sind ebenfalls Gastkonten, aber ohne Passwort: Niemand kann sich
/// bei ihnen anmelden. Auch der Gast selbst hat keins - er bekommt seine Sitzung direkt.
/// </summary>
public class GastkontoService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    ITrainingService training,
    IGoalService goals,
    IGroupService groups,
    IGroupTrainingScheduleService termine,
    IPreferenceService preferences,
    IAccountDataService accountData,
    IRefreshTokenService refreshTokens,
    KontoStatus kontoStatus,
    TimeProvider timeProvider,
    ILogger<GastkontoService> logger)
{
    private const string VereinsName = "Hundesportverein Musterstadt (Demo)";

    /// <summary>Die erfundenen Mitglieder: Vorname, Nachname, Hund, Rasse. Das letzte hat nur angefragt.</summary>
    private static readonly (string Vorname, string Nachname, string Hund, string Rasse)[] Mitglieder =
    [
        ("Mia", "Berger", "Luna", "Border Collie"),
        ("Jonas", "Weber", "Rocky", "Labrador Retriever"),
        ("Sabine", "Koch", "Emma", "Australian Shepherd"),
        ("Tim", "Neumann", "Balu", "Mischling"),
        ("Petra", "Schulz", "Frieda", "Golden Retriever"),
    ];

    /// <returns>Das neue Gastkonto, oder null, wenn gerade zu viele Gäste unterwegs sind.</returns>
    public async Task<ApplicationUser?> AnlegenAsync(string? locale, CancellationToken ct = default)
    {
        var jetzt = timeProvider.GetUtcNow();
        if (await db.Clubs.IgnoreQueryFilters().CountAsync(c => c.GuestUntil > jetzt, ct) >= Gastkonto.MaxAktiv)
            return null;

        var bis = jetzt + Gastkonto.Laufzeit;
        var konten = new List<Guid>();
        var club = new Club { Name = VereinsName, Description = "Erfundener Verein zum Ausprobieren.", GuestUntil = bis };
        ApplicationUser? gast = null;
        try
        {
            // Eine Transaktion für das Ganze: Sonst wäre jedes der gut dreißig SaveChanges (Konten, Trainings,
            // Fährte, Gruppe ...) ein eigener Commit, und das Anlegen dauerte auf dem Server Sekunden.
            await db.InTransaktionAsync(async () =>
            {
                gast = await NeuesKontoAsync("Gast", "", bis, [Roles.User, Roles.Trainer], ct);
                konten.Add(gast.Id);
                gast.OnboardingDismissedAt = jetzt;
                await userManager.UpdateAsync(gast);

                await preferences.SetRegistrationLocaleAsync(gast.Id, locale, ct);
                await preferences.UpdateCountryAsync(gast.Id, new UpdateCountryRequest("DE"), ct);

                db.Clubs.Add(club);
                db.ClubTrainers.Add(new ClubTrainer { ClubId = club.Id, UserId = gast.Id, Role = ClubRole.Training | ClubRole.Verwaltung });
                await db.SaveChangesAsync(ct);

                await HundUndVerlaufAsync(gast, ct);
                // Die rund 700 Punkte der Fährte müssen danach nicht bei jedem weiteren Speichern auf Änderungen
                // geprüft werden; alles Folgende arbeitet mit Ids.
                db.ChangeTracker.Clear();
                await MitgliederAsync(gast, club, bis, konten, ct);
            });
        }
        catch
        {
            // Kein halbfertiger Gast: Was bis hierher entstand, geht gleich wieder. Die Transaktion ist
            // zurückgerollt; bleibt trotzdem etwas stehen, räumt es der Rückbau weg.
            db.ChangeTracker.Clear();
            await EntferneAsync(konten, [club.Id], CancellationToken.None);
            throw;
        }

        await ZaehleAsync(registriert: false);
        return gast;
    }

    /// <summary>
    /// Zählt eine Registrierung, die aus dem Gast-Banner kam (siehe <see cref="GastzugangTag"/>). Ein Fehler
    /// beim Zählen darf die Registrierung nicht kippen.
    /// </summary>
    public Task ZaehleRegistrierungAsync() => ZaehleAsync(registriert: true);

    private async Task ZaehleAsync(bool registriert)
    {
        try
        {
            var tag = Vereinszeit.Tag(timeProvider.GetUtcNow());
            if (db.UnterstuetztMassenbefehle)
            {
                // Atomar in einem Befehl: zwei gleichzeitige Gäste zählen beide, kein Lesen-Ändern-Schreiben-Wettlauf.
                var (c, r) = registriert ? (0, 1) : (1, 0);
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     INSERT INTO guest_access_days ("Day", "Created", "Registered") VALUES ({tag}, {c}, {r})
                     ON CONFLICT ("Day") DO UPDATE SET "Created" = guest_access_days."Created" + {c}, "Registered" = guest_access_days."Registered" + {r}
                     """);
                return;
            }

            // In-Memory-Ersatz der Tests: kennt weder rohes SQL noch Wettläufe.
            var zeile = await db.GastzugangTage.FindAsync(tag) ?? db.GastzugangTage.Add(new GastzugangTag { Day = tag }).Entity;
            if (registriert) zeile.Registered++; else zeile.Created++;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Der Zähler der Gastzugänge konnte nicht erhöht werden.");
        }
    }

    /// <summary>Löscht abgelaufene Gäste und Gast-Vereine restlos. Was nicht gelingt, versucht der nächste Lauf erneut.</summary>
    /// <returns>Die Zahl der entfernten Konten.</returns>
    public async Task<int> EntferneAbgelaufeneAsync(CancellationToken ct = default)
    {
        var jetzt = timeProvider.GetUtcNow();
        var konten = await db.Users.Where(u => u.GuestUntil < jetzt).Select(u => u.Id).ToListAsync(ct);
        var vereine = await db.Clubs.IgnoreQueryFilters().Where(c => c.GuestUntil < jetzt).Select(c => c.Id).ToListAsync(ct);
        return await EntferneAsync(konten, vereine, ct);
    }

    private async Task<ApplicationUser> NeuesKontoAsync(string vorname, string nachname, DateTimeOffset bis, string[] rollen, CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            // Lesbar, weil sie in der Mitgliederliste des Gast-Vereins steht; die Kennung hält sie eindeutig.
            Email = $"{(nachname == "" ? vorname : $"{vorname}.{nachname}").ToLowerInvariant()}-{Guid.NewGuid().ToString("N")[..8]}@{Gastkonto.MailDomain}",
            FirstName = vorname,
            LastName = nachname,
            GuestUntil = bis,
        };
        user.UserName = user.Email;
        var ergebnis = await userManager.CreateAsync(user);
        if (!ergebnis.Succeeded)
            throw new InvalidOperationException($"Gastkonto konnte nicht angelegt werden: {string.Join(", ", ergebnis.Errors.Select(e => e.Description))}");
        await userManager.AddToRolesAsync(user, rollen);
        return user;
    }

    private async Task HundUndVerlaufAsync(ApplicationUser gast, CancellationToken ct)
    {
        var heute = Vereinszeit.Heute();
        var ayla = new Dog { Name = "Ayla", Breed = "Malinois", Gender = DogGender.Female, Birthday = heute.AddYears(-2).AddDays(-40) };
        db.Dogs.Add(ayla);
        db.DogOwners.Add(new DogOwner { DogId = ayla.Id, UserId = gast.Id });
        await db.SaveChangesAsync(ct);

        var sportarten = await db.Sports.Where(s => s.ClubId == null && (s.Code == "BH" || s.Code == "IGP1" || s.Code == "FAERTE")).ToListAsync(ct);
        await preferences.UpdateSportsAsync(gast.Id, new UpdateSportsRequest(sportarten.Select(s => s.Id).ToList()), ct);

        var bh = sportarten.FirstOrDefault(s => s.Code == "BH");
        var uebungen = bh is null ? [] : await db.Exercises
            .Where(e => e.SportId == bh.Id && e.ClubId == null).OrderBy(e => e.Name).Take(4).ToListAsync(ct);

        // Rund drei Wochen, jeden zweiten Tag; die Bewertungen steigen von Einheit zu Einheit.
        var tage = new[] { 21, 19, 17, 14, 12, 10, 7, 5, 3, 1 };
        for (var i = 0; i < tage.Length && uebungen.Count > 0; i++)
        {
            var eintraege = uebungen.Take(2 + i % 2).Select((u, j) =>
            {
                var bewertung = Math.Clamp(2 + i / 3 + (j == 0 ? 1 : 0), 1, 5);
                return new CreateTrainingExerciseRequest(u.Id, bewertung, u.Difficulty, bewertung >= 3, null);
            }).ToList();
            var einheit = await training.CreateAsync(gast.Id,
                new CreateTrainingSessionRequest(ayla.Id, heute.AddDays(-tage[i]), 30 + 5 * (i % 3), i == tage.Length - 1 ? "Heute lief die Fußarbeit schon richtig rund." : null, eintraege), ct);
            if (!einheit.Succeeded)
                logger.LogWarning("Gast-Training konnte nicht angelegt werden: {Fehler}", string.Join("; ", einheit.Errors));
        }

        var regulation = bh is null ? null : await db.Regulations.FirstOrDefaultAsync(r => r.SportId == bh.Id && (r.SeedKey ?? r.Name) == "BH", ct);
        if (bh is not null && regulation is not null)
        {
            var ziel = await goals.CreateAsync(gast.Id, new CreateGoalRequest(ayla.Id, bh.Id, regulation.Id, heute.AddDays(45), "Begleithundeprüfung im Verein."), ct);
            if (!ziel.Succeeded)
                logger.LogWarning("Gast-Ziel konnte nicht angelegt werden: {Fehler}", string.Join("; ", ziel.Errors));
        }

        // Eine ausgewertete Fährte; der Dienst ohne Wetterabruf, wie beim Demo-Seeder.
        var laenge = Math.Round(DemoFaehrtenGeometrie.LaengeMeter);
        var tag = heute.AddDays(-6);
        var (gelegt, abgelaufen) = DemoFaehrtenGeometrie.Erzeuge(3, new DateTimeOffset(tag.ToDateTime(new TimeOnly(15, 30), DateTimeKind.Utc)), TimeSpan.FromMinutes(55), 1.2);
        var tracks = new GpsTrackService(db, new OhneWetter());
        var faehrte = await tracks.CreateAsync(gast.Id, new CreateGpsTrackRequest(
            null, laenge, 55, "Wiese", "sonnig", "schwach", "Winkelfährte mit drei Gegenständen, fast sauber durchgelaufen.", gelegt,
            DogId: ayla.Id, Date: tag, DurationMinutes: 45), ct);
        if (!faehrte.Succeeded)
        {
            logger.LogWarning("Gast-Fährte konnte nicht angelegt werden: {Fehler}", string.Join("; ", faehrte.Errors));
            return;
        }
        var lauf = await tracks.AddWalkRunAsync(gast.Id, faehrte.Value!.Id, new CreateGpsWalkRunRequest(laenge, null, abgelaufen), ct);
        if (!lauf.Succeeded)
            logger.LogWarning("Gast-Ablauf konnte nicht gespeichert werden: {Fehler}", string.Join("; ", lauf.Errors));
    }

    private async Task MitgliederAsync(ApplicationUser gast, Club club, DateTimeOffset bis, List<Guid> konten, CancellationToken ct)
    {
        var gruppe = await groups.CreateAsync(gast.Id, new CreateGroupRequest("Junghunde", "Gruppe für junge Hunde (erfunden).", club.Id), ct);
        if (!gruppe.Succeeded)
            throw new InvalidOperationException($"Gast-Gruppe konnte nicht angelegt werden: {string.Join(", ", gruppe.Errors)}");

        var jetzt = timeProvider.GetUtcNow();
        for (var i = 0; i < Mitglieder.Length; i++)
        {
            var (vorname, nachname, hundName, rasse) = Mitglieder[i];
            var mitglied = await NeuesKontoAsync(vorname, nachname, bis, [Roles.User], ct);
            konten.Add(mitglied.Id);

            var hund = new Dog { Name = hundName, Breed = rasse, Gender = i % 2 == 0 ? DogGender.Female : DogGender.Male, Birthday = Vereinszeit.Heute().AddYears(-1).AddDays(-30 * i) };
            db.Dogs.Add(hund);
            db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = mitglied.Id });

            // Das letzte Mitglied hat nur angefragt: So lässt sich das Freigeben ausprobieren.
            var offen = i == Mitglieder.Length - 1;
            db.ClubMemberships.Add(new ClubMembership
            {
                ClubId = club.Id, UserId = mitglied.Id, RequestedAt = jetzt.AddDays(offen ? -1 : -20 + i),
                Status = offen ? ClubMembershipStatus.Pending : ClubMembershipStatus.Approved,
                DecidedAt = offen ? null : jetzt.AddDays(-19 + i), DecidedByUserId = offen ? null : gast.Id,
            });
            if (i < 3) db.GroupMembers.Add(new GroupMember { GroupId = gruppe.Value!.Id, UserId = mitglied.Id });
        }
        await db.SaveChangesAsync(ct);

        var start = Vereinszeit.TagesBeginn(Vereinszeit.Heute().AddDays(3)).AddHours(18.5);
        var termin = await termine.CreateSessionAsync(gast.Id, club.Id, new CreateSessionRequest(
            gruppe.Value!.Id, GroupTrainingCategory.YoungDog, start, 60, "Vereinsgelände", null, [gast.Id],
            [new SessionContentInput(FreeText: "Leinenführigkeit mit Ablenkung"), new SessionContentInput(FreeText: "Rückruf auf der Wiese")]), ct);
        if (!termin.Succeeded)
            logger.LogWarning("Gast-Termin konnte nicht angelegt werden: {Fehler}", string.Join("; ", termin.Errors));
    }

    /// <summary>
    /// Zuerst die Konten (dieselbe Löschung wie bei jedem Konto: Hunde, Trainings, Fährten, Einstellungen,
    /// Benachrichtigungen ...), danach die Vereine mit allem, was an ihnen hängt.
    /// </summary>
    private async Task<int> EntferneAsync(IReadOnlyCollection<Guid> konten, IReadOnlyCollection<Guid> vereine, CancellationToken ct)
    {
        var entfernt = 0;
        foreach (var id in konten)
        {
            var purge = await accountData.PurgeAsync(id, ct);
            if (!purge.Succeeded)
            {
                logger.LogWarning("Gastkonto {KontoId} konnte nicht gelöscht werden: {Fehler}", id, string.Join("; ", purge.Errors));
                continue;
            }
            var user = await userManager.FindByIdAsync(id.ToString());
            if (user is not null && !(await userManager.DeleteAsync(user)).Succeeded) continue;
            await refreshTokens.DeleteAllForUserAsync(id, ct);
            kontoStatus.Vergessen(id);
            entfernt++;
        }

        await EntferneVereineAsync(vereine, ct);
        return entfernt;
    }

    /// <summary>
    /// Echtes Löschen (IgnoreQueryFilters, kein DeletedAt): Es soll keine Zeile übrig bleiben. Die Gruppen
    /// zuerst - ihr Verweis auf den Verein würde sonst nur geleert und sie blieben stehen.
    /// </summary>
    private async Task EntferneVereineAsync(IReadOnlyCollection<Guid> vereine, CancellationToken ct)
    {
        if (vereine.Count == 0) return;

        async Task<List<T>> Laden<T>(IQueryable<T> abfrage) where T : class => await abfrage.ToListAsync(ct);

        var gruppen = await db.Groups.IgnoreQueryFilters().Where(g => g.ClubId != null && vereine.Contains(g.ClubId.Value)).Select(g => g.Id).ToListAsync(ct);
        var gruppentermine = await db.GroupTrainingSessions.IgnoreQueryFilters().Where(s => vereine.Contains(s.ClubId)).Select(s => s.Id).ToListAsync(ct);
        var anmeldungen = await db.GroupRegistrations.IgnoreQueryFilters().Where(r => gruppen.Contains(r.GroupId)).Select(r => r.Id).ToListAsync(ct);
        var einheiten = await db.GroupTrainingUnits.IgnoreQueryFilters().Where(u => vereine.Contains(u.ClubId)).Select(u => u.Id).ToListAsync(ct);
        var pruefungen = await db.ClubExams.IgnoreQueryFilters().Where(e => vereine.Contains(e.ClubId)).Select(e => e.Id).ToListAsync(ct);
        var einsaetze = await db.ClubWorkEvents.IgnoreQueryFilters().Where(e => vereine.Contains(e.ClubId)).Select(e => e.Id).ToListAsync(ct);
        var sportarten = await db.Sports.IgnoreQueryFilters().Where(s => s.ClubId != null && vereine.Contains(s.ClubId.Value)).Select(s => s.Id).ToListAsync(ct);
        var uebungen = await db.Exercises.IgnoreQueryFilters()
            .Where(e => (e.ClubId != null && vereine.Contains(e.ClubId.Value)) || (e.SportId != null && sportarten.Contains(e.SportId.Value)))
            .Select(e => e.Id).ToListAsync(ct);

        db.GroupTrainingSessionResponses.RemoveRange(await Laden(db.GroupTrainingSessionResponses.IgnoreQueryFilters().Where(x => gruppentermine.Contains(x.GroupTrainingSessionId))));
        db.GroupTrainingSessionTrainers.RemoveRange(await Laden(db.GroupTrainingSessionTrainers.IgnoreQueryFilters().Where(x => gruppentermine.Contains(x.GroupTrainingSessionId))));
        db.GroupTrainingSessionItems.RemoveRange(await Laden(db.GroupTrainingSessionItems.IgnoreQueryFilters().Where(x => gruppentermine.Contains(x.GroupTrainingSessionId))));
        db.GroupTrainingSessions.RemoveRange(await Laden(db.GroupTrainingSessions.IgnoreQueryFilters().Where(x => vereine.Contains(x.ClubId))));
        db.GroupTrainingUnitItems.RemoveRange(await Laden(db.GroupTrainingUnitItems.IgnoreQueryFilters().Where(x => einheiten.Contains(x.GroupTrainingUnitId))));
        db.GroupTrainingUnits.RemoveRange(await Laden(db.GroupTrainingUnits.IgnoreQueryFilters().Where(x => vereine.Contains(x.ClubId))));
        db.GroupTrainingExercises.RemoveRange(await Laden(db.GroupTrainingExercises.IgnoreQueryFilters().Where(x => vereine.Contains(x.ClubId))));
        db.GroupRegistrationAttendances.RemoveRange(await Laden(db.GroupRegistrationAttendances.IgnoreQueryFilters().Where(x => anmeldungen.Contains(x.RegistrationId))));
        db.GroupRegistrations.RemoveRange(await Laden(db.GroupRegistrations.IgnoreQueryFilters().Where(x => gruppen.Contains(x.GroupId))));
        db.GroupMembers.RemoveRange(await Laden(db.GroupMembers.IgnoreQueryFilters().Where(x => gruppen.Contains(x.GroupId))));
        db.GroupTrainers.RemoveRange(await Laden(db.GroupTrainers.IgnoreQueryFilters().Where(x => gruppen.Contains(x.GroupId))));
        db.Groups.RemoveRange(await Laden(db.Groups.IgnoreQueryFilters().Where(x => gruppen.Contains(x.Id))));
        db.ClubExamEntries.RemoveRange(await Laden(db.ClubExamEntries.IgnoreQueryFilters().Where(x => pruefungen.Contains(x.ExamId))));
        db.ClubExamRegulations.RemoveRange(await Laden(db.ClubExamRegulations.IgnoreQueryFilters().Where(x => pruefungen.Contains(x.ExamId))));
        db.ClubExams.RemoveRange(await Laden(db.ClubExams.IgnoreQueryFilters().Where(x => vereine.Contains(x.ClubId))));
        db.ClubWorkParticipations.RemoveRange(await Laden(db.ClubWorkParticipations.IgnoreQueryFilters().Where(x => einsaetze.Contains(x.ClubWorkEventId))));
        db.ClubWorkEvents.RemoveRange(await Laden(db.ClubWorkEvents.IgnoreQueryFilters().Where(x => vereine.Contains(x.ClubId))));
        db.ClubWorkGuests.RemoveRange(await Laden(db.ClubWorkGuests.IgnoreQueryFilters().Where(x => vereine.Contains(x.ClubId))));
        db.ClubMemberships.RemoveRange(await Laden(db.ClubMemberships.IgnoreQueryFilters().Where(x => vereine.Contains(x.ClubId))));
        db.ClubTrainers.RemoveRange(await Laden(db.ClubTrainers.IgnoreQueryFilters().Where(x => vereine.Contains(x.ClubId))));
        db.RegulationExercises.RemoveRange(await Laden(db.RegulationExercises.IgnoreQueryFilters().Where(x => uebungen.Contains(x.ExerciseId))));
        db.Exercises.RemoveRange(await Laden(db.Exercises.IgnoreQueryFilters().Where(x => uebungen.Contains(x.Id))));
        db.Sports.RemoveRange(await Laden(db.Sports.IgnoreQueryFilters().Where(x => sportarten.Contains(x.Id))));
        db.Clubs.RemoveRange(await Laden(db.Clubs.IgnoreQueryFilters().Where(x => vereine.Contains(x.Id))));
        await db.SaveChangesAsync(ct);
    }

    private sealed class OhneWetter : IWeatherEnrichmentService
    {
        public Task EnqueueTrackAsync(GpsTrack track, CancellationToken ct = default) => Task.CompletedTask;
        public void EnqueueSession(Domain.Training.TrainingSession session) { }
        public Task EnrichTrackAsync(GpsTrack track, CancellationToken ct = default) => Task.CompletedTask;
    }
}
