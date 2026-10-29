using Dogity.Application.Admin;
using Dogity.Application.Preferences;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Learning;
using Dogity.Domain.Notifications;
using Dogity.Domain.Planning;
using Dogity.Domain.Preferences;
using Dogity.Domain.Sports;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;

namespace Dogity.Application.Tests.Admin;

/// <summary>
/// "Wie und was wird genutzt" der Admin-Statistik: je Abschnitt Daten, die die
/// Definition prüfen - gelöschte Einträge und Konten zählen nicht, ein Konto mit
/// zwei Einträgen zählt einmal, die 30-Tage-Grenze, und Wochen/Stunden nach
/// Berliner Zeit.
/// </summary>
public class AdminUsageTests
{
    // Mittwoch, 28.10.2026, 12:00 UTC (= 13:00 in Berlin, Winterzeit seit dem 25.10.).
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private sealed class FesteUhr(DateTimeOffset jetzt) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => jetzt;
    }

    private static DateTimeOffset VorTagen(double tage) => Jetzt.AddDays(-tage);

    private sealed class Umgebung
    {
        public ApplicationDbContext Db { get; } = InMemoryDbContext.Create();
        public FakeUserLookupService Lookup { get; } = new();
        public FakeRefreshTokenService Tokens { get; } = new();

        public Guid Konto(double alterInTagen = 100)
        {
            var id = Guid.NewGuid();
            Lookup.Register(id, $"{id}@test.de", createdAt: VorTagen(alterInTagen));
            return id;
        }

        public Dog Hund(params Guid[] besitzer)
        {
            var hund = new Dog { Name = "Bello" };
            Db.Dogs.Add(hund);
            foreach (var b in besitzer) Db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = b });
            return hund;
        }

        public TrainingSession Training(Guid nutzer, DateTimeOffset angelegt, bool geloescht = false)
        {
            var s = new TrainingSession
            {
                UserId = nutzer, DogId = Guid.NewGuid(), Date = DateOnly.FromDateTime(angelegt.UtcDateTime),
                DurationMinutes = 30, CreatedAt = angelegt, DeletedAt = geloescht ? Jetzt : null,
            };
            Db.TrainingSessions.Add(s);
            return s;
        }

        public Task<AdminUsageDto> Holen() =>
            new AdminUsageService(Db, Lookup, Tokens, new FesteUhr(Jetzt)).GetAsync().ContinueWith(t => t.Result.Value!);
    }

    private static AdminFeatureUsageDto Funktion(AdminUsageDto dto, string key) => Assert.Single(dto.Features, f => f.Key == key);

    // --- Aktivität ---

    [Fact]
    public async Task LeereDatenbank_AllesNull_UndVollstaendigeReihen()
    {
        var u = new Umgebung();

        var dto = await u.Holen();

        Assert.Equal(0, dto.Activity.TotalAccounts);
        Assert.Equal(0, dto.Activity.ActiveLast90Days);
        Assert.Equal(12, dto.Activity.Weeks.Count);
        Assert.All(dto.Activity.Weeks, w => Assert.Equal(0, w.Entries));
        Assert.All(dto.Features, f => { Assert.Equal(0, f.AccountsEver); Assert.Null(f.LastUsed); });
        Assert.Equal(7, dto.Timing.ByWeekday.Count);
        Assert.Equal(24, dto.Timing.ByHour.Count);
        // Auch nie abgeschaltete Module stehen drin.
        Assert.Equal(Modules.Bekannt, dto.DisabledModules.Select(m => m.Key));
    }

    [Fact]
    public async Task AktiveKonten_ZweiEintraegeZaehlenEinmal_GeloeschtesUndFremdesKontoNicht_GrenzenStimmen()
    {
        var u = new Umgebung();
        var zweimal = u.Konto();
        var gestern = u.Konto();
        var vor29 = u.Konto();
        var vor31 = u.Konto();
        var vor91 = u.Konto();
        var nurGeloescht = u.Konto();
        var weg = Guid.NewGuid(); // Konto gibt es nicht mehr, das Training steht noch
        u.Training(zweimal, VorTagen(1));
        u.Training(zweimal, VorTagen(2));
        u.Training(gestern, VorTagen(1));
        u.Training(vor29, VorTagen(29));
        u.Training(vor31, VorTagen(31));
        u.Training(vor91, VorTagen(91));
        u.Training(nurGeloescht, VorTagen(1), geloescht: true);
        u.Training(weg, VorTagen(1));
        await u.Db.SaveChangesAsync();

        var a = (await u.Holen()).Activity;

        Assert.Equal(6, a.TotalAccounts);
        Assert.Equal(2, a.ActiveLast7Days);       // zweimal, gestern
        Assert.Equal(3, a.ActiveLast30Days);      // + vor 29 Tagen
        Assert.Equal(4, a.ActiveLast90Days);      // + vor 31 Tagen; vor 91 nicht
    }

    [Fact]
    public async Task AppGeoeffnet_KommtAusDenTokens_7UndDreissigTage()
    {
        var u = new Umgebung();
        var heute = u.Konto();
        var vorZehnTagen = u.Konto();
        var vorZweiMonaten = u.Konto();
        u.Tokens.Ausgestellt.AddRange([(heute, VorTagen(0.1)), (heute, VorTagen(3)), (vorZehnTagen, VorTagen(10)), (vorZweiMonaten, VorTagen(60))]);

        var a = (await u.Holen()).Activity;

        Assert.Equal(1, a.OpenedLast7Days);
        Assert.Equal(2, a.OpenedLast30Days);
    }

    [Fact]
    public async Task Verlauf_ZwoelfKalenderwochenInBerlinerZeit_SonntagNachtGehoertZurNeuenWoche()
    {
        var u = new Umgebung();
        var konto = u.Konto();
        var anderes = u.Konto();
        // Sonntag 25.10. 23:30 UTC = Montag 26.10. 00:30 in Berlin: laufende Woche, nicht die davor.
        u.Training(konto, new DateTimeOffset(2026, 10, 25, 23, 30, 0, TimeSpan.Zero));
        u.Training(anderes, new DateTimeOffset(2026, 10, 25, 23, 30, 0, TimeSpan.Zero));
        // Vorige Woche, zwei Einträge einer Person.
        u.Training(konto, new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));
        u.Training(konto, new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));
        // Übungen eines Trainings zählen in der Wochenreihe nicht als eigene Einträge.
        u.Db.TrainingExercises.Add(new TrainingExercise { TrainingSessionId = u.Training(konto, new DateTimeOffset(2026, 10, 22, 9, 0, 0, TimeSpan.Zero)).Id, FreeTextLabel = "Frei", Rating = 3 });
        // Älteste angezeigte Woche beginnt am Montag 10.8.2026; der Sonntag davor ist draußen.
        u.Training(konto, new DateTimeOffset(2026, 8, 10, 6, 0, 0, TimeSpan.Zero));
        u.Training(konto, new DateTimeOffset(2026, 8, 9, 20, 0, 0, TimeSpan.Zero));
        await u.Db.SaveChangesAsync();

        var wochen = (await u.Holen()).Activity.Weeks;

        Assert.Equal(new DateOnly(2026, 8, 10), wochen[0].WeekStart);
        Assert.Equal(new DateOnly(2026, 10, 26), wochen[11].WeekStart);
        Assert.All(wochen, w => Assert.Equal(DayOfWeek.Monday, w.WeekStart.DayOfWeek));
        Assert.Equal(new AdminUsageWeekDto(new DateOnly(2026, 8, 10), 1, 1), wochen[0]);
        Assert.Equal(new AdminUsageWeekDto(new DateOnly(2026, 10, 19), 1, 3), wochen[10]);
        Assert.Equal(new AdminUsageWeekDto(new DateOnly(2026, 10, 26), 2, 2), wochen[11]);
    }

    // --- Nutzung je Funktion ---

    [Fact]
    public async Task Funktion_Training_KontenEintraegeJemalsZuletzt()
    {
        var u = new Umgebung();
        var a = u.Konto();
        var b = u.Konto();
        u.Training(a, VorTagen(5));
        u.Training(a, VorTagen(6));
        u.Training(b, VorTagen(45));
        u.Training(b, VorTagen(2), geloescht: true);
        await u.Db.SaveChangesAsync();

        var f = Funktion(await u.Holen(), "training");

        Assert.Equal(1, f.AccountsLast30Days);
        Assert.Equal(2, f.EntriesLast30Days);
        Assert.Equal(2, f.AccountsEver);
        Assert.Equal(new DateOnly(2026, 10, 23), f.LastUsed);
    }

    [Fact]
    public async Task Funktion_NieGenutzt_HatKeinDatum()
    {
        var u = new Umgebung();
        u.Konto();

        var f = Funktion(await u.Holen(), "sachkunde-exam");

        Assert.Equal(0, f.AccountsEver);
        Assert.Null(f.LastUsed);
    }

    [Fact]
    public async Task Funktion_OhnePersonenfeld_GiltFuerAlleBesitzer_DerEintragZaehltEinmal()
    {
        var u = new Umgebung();
        var eins = u.Konto();
        var zwei = u.Konto();
        var fremd = u.Konto();
        var hund = u.Hund(eins, zwei);
        var geloeschterHund = u.Hund(fremd);
        geloeschterHund.DeletedAt = Jetzt;
        u.Db.Goals.Add(new Goal { DogId = hund.Id, SportId = Guid.NewGuid(), TargetDate = new DateOnly(2027, 1, 1), CreatedAt = VorTagen(3) });
        u.Db.Goals.Add(new Goal { DogId = geloeschterHund.Id, SportId = Guid.NewGuid(), TargetDate = new DateOnly(2027, 1, 1), CreatedAt = VorTagen(3) });
        u.Db.DogImages.Add(new DogImage { DogId = hund.Id, CreatedAt = VorTagen(40) });
        await u.Db.SaveChangesAsync();

        var dto = await u.Holen();

        var ziel = Funktion(dto, "goal");
        Assert.Equal(2, ziel.AccountsLast30Days);
        Assert.Equal(1, ziel.EntriesLast30Days);
        Assert.Equal(2, ziel.AccountsEver);
        // Das Foto ist älter als 30 Tage: nur "jemals".
        var foto = Funktion(dto, "dog-photo");
        Assert.Equal(0, foto.AccountsLast30Days);
        Assert.Equal(0, foto.EntriesLast30Days);
        Assert.Equal(2, foto.AccountsEver);
    }

    [Fact]
    public async Task Funktion_PausenUndGesundheit_NehmenDieAnlegendePersonSonstDieBesitzer()
    {
        var u = new Umgebung();
        var besitzer = u.Konto();
        var trainerin = u.Konto();
        var hund = u.Hund(besitzer);
        u.Db.DogPauses.Add(new DogPause { DogId = hund.Id, StartDate = new DateOnly(2026, 10, 1), CreatedByUserId = trainerin, CreatedAt = VorTagen(2) });
        u.Db.DogHealthRecords.Add(new DogHealthRecord { DogId = hund.Id, Title = "Impfung", Date = new DateOnly(2026, 10, 1), CreatedAt = VorTagen(2) });
        await u.Db.SaveChangesAsync();

        var dto = await u.Holen();

        // Die Pause hat eine anlegende Person (die Trainerin), der Besitzer nicht.
        Assert.Equal(1, Funktion(dto, "pause").AccountsEver);
        Assert.Equal(1, Funktion(dto, "pause").AccountsLast30Days);
        // Der Gesundheitseintrag ohne die Angabe gehört dem Besitzer.
        Assert.Equal(1, Funktion(dto, "health").AccountsEver);
        Assert.Equal(1, Funktion(dto, "health").EntriesLast30Days);
    }

    [Fact]
    public async Task Funktion_FaehrteUndAblauf_GehoerenDerPersonDesTrainings_GeloeschteNicht()
    {
        var u = new Umgebung();
        var konto = u.Konto();
        var training = u.Training(konto, VorTagen(50));
        var geloeschtesTraining = u.Training(konto, VorTagen(50), geloescht: true);
        var spur = new GpsTrack { TrainingSessionId = training.Id, CreatedAt = VorTagen(4) };
        u.Db.GpsTracks.AddRange(spur, new GpsTrack { TrainingSessionId = geloeschtesTraining.Id, CreatedAt = VorTagen(4) });
        u.Db.GpsWalkRuns.AddRange(
            new GpsWalkRun { TrackId = spur.Id, CreatedAt = VorTagen(1) },
            new GpsWalkRun { TrackId = spur.Id, CreatedAt = VorTagen(1), DeletedAt = Jetzt });
        await u.Db.SaveChangesAsync();

        var dto = await u.Holen();

        Assert.Equal(1, Funktion(dto, "track-laid").EntriesLast30Days);
        Assert.Equal(1, Funktion(dto, "track-walk").EntriesLast30Days);
        Assert.Equal(1, Funktion(dto, "track-walk").AccountsLast30Days);
        Assert.Equal(new DateOnly(2026, 10, 27), Funktion(dto, "track-walk").LastUsed);
    }

    [Fact]
    public async Task Funktion_SachkundeLernen_ZaehltDieLetzteAntwort_ProKontoEinmal()
    {
        var u = new Umgebung();
        var konto = u.Konto();
        var katalog = new QuizCatalog { Code = "k", Name = "K" };
        var frage1 = new QuizQuestion { CatalogId = katalog.Id };
        var frage2 = new QuizQuestion { CatalogId = katalog.Id };
        u.Db.QuizCatalogs.Add(katalog);
        u.Db.QuizQuestions.AddRange(frage1, frage2);
        // Vor 60 Tagen zum ersten Mal beantwortet, gestern wiederholt.
        u.Db.QuizMasteries.Add(new QuizMastery { UserId = konto, QuestionId = frage1.Id, CreatedAt = VorTagen(60), LastAnsweredAt = VorTagen(1) });
        u.Db.QuizMasteries.Add(new QuizMastery { UserId = konto, QuestionId = frage2.Id, CreatedAt = VorTagen(60), LastAnsweredAt = VorTagen(1) });
        await u.Db.SaveChangesAsync();

        var f = Funktion(await u.Holen(), "sachkunde-learn");

        Assert.Equal(1, f.AccountsLast30Days);
        Assert.Equal(2, f.EntriesLast30Days);
        Assert.Equal(1, f.AccountsEver);
    }

    [Fact]
    public async Task Funktion_Mitbesitz_ZaehltNurAngenommeneEinladungen_ZumZeitpunktDerAnnahme()
    {
        var u = new Umgebung();
        var angenommen = u.Konto();
        var offen = u.Konto();
        var besitzer = u.Konto();
        var hund = u.Hund(besitzer);
        // Vor 50 Tagen eingeladen, gestern angenommen.
        u.Db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = angenommen, InvitedByUserId = besitzer, CreatedAt = VorTagen(50), UpdatedAt = VorTagen(1) });
        u.Db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = offen, InvitedByUserId = besitzer, Status = DogOwnerStatus.Invited, CreatedAt = VorTagen(2) });
        await u.Db.SaveChangesAsync();

        var f = Funktion(await u.Holen(), "co-owner");

        Assert.Equal(1, f.AccountsEver);
        Assert.Equal(1, f.AccountsLast30Days);
        Assert.Equal(new DateOnly(2026, 10, 27), f.LastUsed);
    }

    [Fact]
    public async Task Funktion_Gruppentraining_ClubUndPush()
    {
        var u = new Umgebung();
        var trainerin = u.Konto();
        var mitglied = u.Konto();
        var verein = new Club { Name = "Verein" };
        var gruppe = new Group { ClubId = verein.Id, TrainerId = trainerin, Name = "Welpen" };
        u.Db.Clubs.Add(verein);
        u.Db.Groups.Add(gruppe);
        var termin = new GroupTrainingSession { ClubId = verein.Id, GroupId = gruppe.Id, StartsAt = VorTagen(-3), CreatedByUserId = trainerin, CreatedAt = VorTagen(5) };
        u.Db.GroupTrainingSessions.Add(termin);
        u.Db.GroupTrainingSessionResponses.Add(new GroupTrainingSessionResponse { GroupTrainingSessionId = termin.Id, UserId = mitglied, IsAttending = true, RespondedAt = VorTagen(1) });
        u.Db.GroupRegistrations.Add(new GroupRegistration { GroupId = gruppe.Id, FirstName = "A", LastName = "B", DogName = "C", CreatedAt = VorTagen(3) });
        u.Db.ClubMemberships.AddRange(
            new ClubMembership { ClubId = verein.Id, UserId = mitglied, Status = ClubMembershipStatus.Approved, DecidedAt = VorTagen(10) },
            new ClubMembership { ClubId = verein.Id, UserId = trainerin, Status = ClubMembershipStatus.Pending });
        u.Db.PushSubscriptions.AddRange(
            new PushSubscription { UserId = mitglied, Endpoint = "a", CreatedAt = VorTagen(20) },
            new PushSubscription { UserId = mitglied, Endpoint = "b", CreatedAt = VorTagen(1) });
        await u.Db.SaveChangesAsync();

        var dto = await u.Holen();

        Assert.Equal(1, Funktion(dto, "group-session").EntriesLast30Days);
        Assert.Equal(1, Funktion(dto, "session-response").AccountsLast30Days);
        Assert.Equal(1, Funktion(dto, "group-registration").EntriesLast30Days);
        Assert.Equal(1, Funktion(dto, "club-membership").AccountsEver); // die offene Anfrage zählt nicht
        // Push: zwei Geräte eines Kontos sind ein Konto, und es gibt keine Einträge.
        var push = Funktion(dto, "push");
        Assert.Equal(1, push.AccountsLast30Days);
        Assert.Equal(1, push.AccountsEver);
        Assert.Null(push.EntriesLast30Days);
    }

    [Fact]
    public async Task Funktion_VereinsbeitrittUndPush_MachenNichtAktiv()
    {
        var u = new Umgebung();
        var mitglied = u.Konto();
        var verein = new Club { Name = "Verein" };
        u.Db.Clubs.Add(verein);
        // Die Genehmigung löst die Trainer:in aus, nicht das Mitglied.
        u.Db.ClubMemberships.Add(new ClubMembership { ClubId = verein.Id, UserId = mitglied, Status = ClubMembershipStatus.Approved, DecidedAt = VorTagen(2) });
        u.Db.PushSubscriptions.Add(new PushSubscription { UserId = mitglied, Endpoint = "a", CreatedAt = VorTagen(2) });
        await u.Db.SaveChangesAsync();

        var dto = await u.Holen();

        Assert.Equal(1, Funktion(dto, "club-membership").AccountsLast30Days);
        Assert.Equal(0, dto.Activity.ActiveLast30Days);
    }

    // --- Abgeschaltete Module ---

    [Fact]
    public async Task AbgeschalteteModule_ZaehltKontenJeModul_ImmerNurVorhandeneKonten()
    {
        var u = new Umgebung();
        var eins = u.Konto();
        var zwei = u.Konto();
        var weg = Guid.NewGuid();
        PreferenceMitModulen(u, eins, Modules.Faehrte, Modules.Wetter);
        PreferenceMitModulen(u, zwei, Modules.Faehrte);
        PreferenceMitModulen(u, weg, Modules.Faehrte);
        await u.Db.SaveChangesAsync();

        var module = (await u.Holen()).DisabledModules.ToDictionary(m => m.Key, m => m.Accounts);

        Assert.Equal(2, module[Modules.Faehrte]);
        Assert.Equal(1, module[Modules.Wetter]);
        Assert.Equal(0, module[Modules.Sachkunde]);
    }

    private static void PreferenceMitModulen(Umgebung u, Guid nutzer, params string[] module)
    {
        var einstellung = new UserPreference { UserId = nutzer };
        u.Db.UserPreferences.Add(einstellung);
        foreach (var m in module)
            u.Db.UserDisabledModules.Add(new UserDisabledModule { UserPreferenceId = einstellung.Id, ModuleKey = m });
    }

    // --- Bindung ---

    [Fact]
    public async Task Bindung_AelterAlsDreissigTage_DavonAktiv_UndNieAngelegtMitUndOhneHund()
    {
        var u = new Umgebung();
        var altAktiv = u.Konto(alterInTagen: 60);
        var altStill = u.Konto(alterInTagen: 60);
        var altFrueherAktiv = u.Konto(alterInTagen: 90);
        var neuOhneHund = u.Konto(alterInTagen: 5);
        var neuMitHund = u.Konto(alterInTagen: 5);
        var hundGeloescht = u.Konto(alterInTagen: 60);
        u.Training(altAktiv, VorTagen(3));
        u.Training(altFrueherAktiv, VorTagen(50));
        u.Hund(neuMitHund);
        u.Hund(hundGeloescht).DeletedAt = Jetzt;
        await u.Db.SaveChangesAsync();

        var b = (await u.Holen()).Retention;

        Assert.Equal(4, b.AccountsOlderThan30Days);
        Assert.Equal(1, b.OlderActiveLast30Days);
        // Nie etwas angelegt: altStill, neuOhneHund, neuMitHund, hundGeloescht.
        Assert.Equal(3, b.NeverCreatedWithoutDog);  // altStill, neuOhneHund, hundGeloescht
        Assert.Equal(1, b.NeverCreatedWithDog);     // neuMitHund
    }

    // --- Wann wird eingetragen ---

    [Fact]
    public async Task Wann_ZaehltNachBerlinerWochentagUndStunde_NurTrainingFaehrteAblauf()
    {
        var u = new Umgebung();
        var konto = u.Konto();
        // Montag 26.10. 23:30 UTC = Dienstag 27.10. 00:30 in Berlin (Winterzeit).
        u.Training(konto, new DateTimeOffset(2026, 10, 26, 23, 30, 0, TimeSpan.Zero));
        // Sommerzeit (+2 Stunden): Samstag 8.8. 21:45 UTC = Samstag 23:45 in Berlin.
        u.Training(konto, new DateTimeOffset(2026, 8, 8, 21, 45, 0, TimeSpan.Zero));
        // Älter als 90 Tage: nicht dabei. Gelöscht: nicht dabei.
        u.Training(konto, VorTagen(120));
        u.Training(konto, new DateTimeOffset(2026, 10, 26, 10, 0, 0, TimeSpan.Zero), geloescht: true);
        // Eine Quizantwort ist kein Eintrag dieser Auswertung.
        var katalog = new QuizCatalog { Code = "k", Name = "K" };
        var frage = new QuizQuestion { CatalogId = katalog.Id };
        u.Db.QuizCatalogs.Add(katalog);
        u.Db.QuizQuestions.Add(frage);
        u.Db.QuizMasteries.Add(new QuizMastery { UserId = konto, QuestionId = frage.Id, CreatedAt = VorTagen(1), LastAnsweredAt = VorTagen(1) });
        await u.Db.SaveChangesAsync();

        var wann = (await u.Holen()).Timing;

        Assert.Equal(2, wann.ByWeekday.Sum());
        Assert.Equal(1, wann.ByWeekday[1]);   // Dienstag, nicht der Montag der UTC-Zeit
        Assert.Equal(0, wann.ByWeekday[0]);
        Assert.Equal(1, wann.ByWeekday[5]);   // Samstag
        Assert.Equal(2, wann.ByHour.Sum());
        Assert.Equal(1, wann.ByHour[0]);      // 00:30, nicht 23
        Assert.Equal(1, wann.ByHour[23]);     // 23:45 Sommerzeit (21 Uhr UTC)
    }

    // --- Sportarten ---

    [Fact]
    public async Task Sportarten_TrainingsJeSportart_UndHundeMitAuswahl()
    {
        var u = new Umgebung();
        var konto = u.Konto();
        var weg = Guid.NewGuid();
        var faehrte = new Sport { Code = "FAERTE", Name = "Fährte", NameEn = "Tracking" };
        var bh = new Sport { Code = "BH", Name = "Begleithund" };
        var ungenutzt = new Sport { Code = "XY", Name = "Agility" };
        u.Db.Sports.AddRange(faehrte, bh, ungenutzt);
        var uebungF = new Exercise { SportId = faehrte.Id, Name = "Spur" };
        var uebungB1 = new Exercise { SportId = bh.Id, Name = "Sitz" };
        var uebungB2 = new Exercise { SportId = bh.Id, Name = "Platz" };
        u.Db.Exercises.AddRange(uebungF, uebungB1, uebungB2);

        // Ein Training mit zwei Übungen aus BH zählt einmal bei BH und eines mit Fährte+BH bei beiden.
        var t1 = u.Training(konto, VorTagen(3));
        var t2 = u.Training(konto, VorTagen(4));
        var tAlt = u.Training(konto, VorTagen(100));
        var tFremd = u.Training(weg, VorTagen(3));
        foreach (var (s, e) in new[] { (t1, uebungB1), (t1, uebungB2), (t2, uebungF), (t2, uebungB1), (tAlt, uebungF), (tFremd, uebungF) })
            u.Db.TrainingExercises.Add(new TrainingExercise { TrainingSessionId = s.Id, ExerciseId = e.Id, Rating = 3 });
        // Freitext ohne Katalog-Übung hat keine Sportart.
        u.Db.TrainingExercises.Add(new TrainingExercise { TrainingSessionId = t1.Id, FreeTextLabel = "Frei", Rating = 3 });

        var hund = u.Hund(konto);
        var hundOhneKonto = u.Hund(weg);
        u.Db.DogSportSelections.AddRange(
            new DogSportSelection { DogId = hund.Id, SportId = faehrte.Id },
            new DogSportSelection { DogId = hund.Id, SportId = bh.Id },
            new DogSportSelection { DogId = hundOhneKonto.Id, SportId = bh.Id });
        await u.Db.SaveChangesAsync();

        var sportarten = (await u.Holen()).Sports;

        Assert.Equal(3, sportarten.Count);
        Assert.Equal(new AdminSportUsageDto("Begleithund", null, 2, 1), sportarten[0]);
        Assert.Equal(new AdminSportUsageDto("Fährte", "Tracking", 1, 1), sportarten[1]);
        Assert.Equal(new AdminSportUsageDto("Agility", null, 0, 0), sportarten[2]);
    }

    // --- Nur Zählungen ---

    [Fact]
    public void Dto_EnthaeltKeineIdsUndKeineZeitpunkteMitUhrzeit()
    {
        Type[] typen =
        [
            typeof(AdminUsageDto), typeof(AdminUsageActivityDto), typeof(AdminUsageWeekDto), typeof(AdminFeatureUsageDto),
            typeof(AdminDisabledModuleDto), typeof(AdminRetentionDto), typeof(AdminUsageTimingDto), typeof(AdminSportUsageDto),
        ];

        var verboten = typen
            .SelectMany(t => t.GetProperties())
            .Where(p => p.PropertyType == typeof(Guid) || p.PropertyType == typeof(Guid?) || p.PropertyType == typeof(DateTimeOffset) || p.PropertyType == typeof(DateTimeOffset?))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}");

        Assert.Empty(verboten);
    }
}
