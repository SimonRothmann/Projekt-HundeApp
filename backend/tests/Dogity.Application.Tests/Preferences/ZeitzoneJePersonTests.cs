using Dogity.Application.Account;
using Dogity.Application.Common;
using Dogity.Application.Community;
using Dogity.Application.Planning;
using Dogity.Application.Preferences;
using Dogity.Application.Reminders;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Training;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Preferences;
using Dogity.Domain.Sports;

namespace Dogity.Application.Tests.Preferences;

/// <summary>
/// Personenzeit: "heute", Erinnerungs- und Ruhezeiten zählen in der Zone des Geräts der Person; ohne
/// gemeldete Zone gilt wie bisher die Vereinszeit (Europe/Berlin).
/// </summary>
public class ZeitzoneJePersonTests
{
    private const string NewYork = "America/New_York";
    private const string Tokio = "Asia/Tokyo";

    private sealed class FesteUhr(DateTimeOffset jetzt) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => jetzt;
    }

    private static readonly IReadOnlySet<ErinnerungsSchluessel> NichtsGesendet = new HashSet<ErinnerungsSchluessel>();

    // Mittwoch 15.07.2026, 01:30 UTC = 03:30 Uhr in Berlin, aber noch Dienstag 14.07., 21:30 Uhr in New York.
    private static readonly DateTimeOffset Nachts = new(2026, 7, 15, 1, 30, 0, TimeSpan.Zero);

    // ---------------------------------------------------------------- Ortszeit

    [Fact]
    public void Tag_RechnetInDerGegebenenZone()
    {
        var abends = new DateTimeOffset(2026, 7, 14, 20, 0, 0, TimeSpan.FromHours(-4)); // 20:00 in New York

        Assert.Equal(new DateOnly(2026, 7, 14), Ortszeit.Tag(abends, Ortszeit.Zone(NewYork)));
        Assert.Equal(new DateOnly(2026, 7, 15), Ortszeit.Tag(abends, Vereinszeit.Zone));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Nirgendwo/Nichts")]
    public void Zone_OhneOderMitUnbekannterKennung_IstDieVereinszeit(string? id) =>
        Assert.Equal(Vereinszeit.Zone, Ortszeit.Zone(id));

    [Fact]
    public void TagesBeginn_IstMitternachtInDerZone()
    {
        var beginn = Ortszeit.TagesBeginn(new DateOnly(2026, 7, 14), Ortszeit.Zone(NewYork));

        Assert.Equal(new DateTimeOffset(2026, 7, 14, 4, 0, 0, TimeSpan.Zero), beginn);
    }

    [Fact]
    public void TagSpanne_UmfasstAlleTageAufDerErde()
    {
        var (frueh, spaet) = Ortszeit.TagSpanne(Nachts);

        Assert.Equal(new DateOnly(2026, 7, 14), frueh);
        Assert.Equal(new DateOnly(2026, 7, 15), spaet);
    }

    // ---------------------------------------------------------------- Speichern

    [Fact]
    public async Task Speichern_NimmtEineBekannteZoneAn_UndLeerStelltDieVereinszeitWiederHer()
    {
        var db = InMemoryDbContext.Create();
        var dienst = new PreferenceService(db);
        var nutzer = Guid.NewGuid();

        Assert.True((await dienst.UpdateTimeZoneAsync(nutzer, new UpdateTimeZoneRequest(NewYork))).Succeeded);
        Assert.Equal(NewYork, (await dienst.GetAsync(nutzer)).Value!.TimeZone);

        Assert.True((await dienst.UpdateTimeZoneAsync(nutzer, new UpdateTimeZoneRequest(" "))).Succeeded);
        Assert.Null((await dienst.GetAsync(nutzer)).Value!.TimeZone);
    }

    [Theory]
    [InlineData("Nirgendwo/Nichts")]
    [InlineData("'; drop table user_preferences; --")]
    public async Task Speichern_LehntEineUnbekannteZoneAb(string zone)
    {
        var dienst = new PreferenceService(InMemoryDbContext.Create());
        var nutzer = Guid.NewGuid();

        var ergebnis = await dienst.UpdateTimeZoneAsync(nutzer, new UpdateTimeZoneRequest(zone));

        Assert.False(ergebnis.Succeeded);
        Assert.Null((await dienst.GetAsync(nutzer)).Value!.TimeZone);
    }

    [Fact]
    public async Task Speichern_LehntEineZuLangeKennungAb()
    {
        var dienst = new PreferenceService(InMemoryDbContext.Create());

        var ergebnis = await dienst.UpdateTimeZoneAsync(Guid.NewGuid(), new UpdateTimeZoneRequest(new string('A', Ortszeit.ZoneMaxLaenge + 1)));

        Assert.False(ergebnis.Succeeded);
    }

    [Fact]
    public async Task Export_NenntDieZone()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var nutzer = Guid.NewGuid();
        lookup.Register(nutzer, "ich@dogity.test", "Ich", "Selbst");
        db.UserPreferences.Add(new UserPreference { UserId = nutzer, TimeZone = NewYork });
        await db.SaveChangesAsync();

        var export = (await new AccountDataService(db, lookup, new FakeNotificationService()).ExportAsync(nutzer)).Value!;

        Assert.Equal(NewYork, export.Einstellungen!.Zeitzone);
    }

    // ---------------------------------------------------------------- "heute" bei Anfragen

    [Fact]
    public async Task Heute_OhneZone_IstDerVereinszeitTag_MitZoneDerOrtstag()
    {
        var db = InMemoryDbContext.Create();
        var uhr = new FesteUhr(Nachts);
        var ohne = Guid.NewGuid();
        var amerika = Guid.NewGuid();
        db.UserPreferences.Add(new UserPreference { UserId = amerika, TimeZone = NewYork });
        await db.SaveChangesAsync();

        Assert.Equal(new DateOnly(2026, 7, 15), await db.HeuteAsync(ohne, uhr));
        Assert.Equal(new DateOnly(2026, 7, 14), await db.HeuteAsync(amerika, uhr));
    }

    [Fact]
    public async Task Ziel_DerHeutigeTagInNewYorkIstKeineZukunft_InBerlinSchon()
    {
        var db = InMemoryDbContext.Create();
        var dienst = new GoalService(db, new FesteUhr(Nachts), new FakeNotificationService(), new ExerciseMasteryService(db));
        var berliner = Guid.NewGuid();
        var amerikaner = Guid.NewGuid();
        var sport = new Sport { Code = "UPR", Name = "Unterordnung" };
        db.Sports.Add(sport);
        db.UserPreferences.Add(new UserPreference { UserId = amerikaner, TimeZone = NewYork });
        var hunde = new[] { berliner, amerikaner }.Select(halter =>
        {
            var hund = new Dog { Name = "Bello" };
            db.Dogs.Add(hund);
            db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = halter, Role = DogOwnerRole.Owner });
            return hund;
        }).ToList();
        db.Exercises.Add(new Exercise { Name = "Freifolge", SportId = sport.Id, Difficulty = ExerciseDifficulty.Beginner });
        await db.SaveChangesAsync();

        // Der 15.07. ist in Berlin "heute" (kein Zieldatum), in New York noch morgen.
        var dasDatum = new DateOnly(2026, 7, 15);
        var inBerlin = await dienst.CreateAsync(berliner, new CreateGoalRequest(hunde[0].Id, sport.Id, null, dasDatum, null));
        var inNewYork = await dienst.CreateAsync(amerikaner, new CreateGoalRequest(hunde[1].Id, sport.Id, null, dasDatum, null));

        Assert.False(inBerlin.Succeeded);
        Assert.True(inNewYork.Succeeded);
    }

    // ---------------------------------------------------------------- Trainingsrhythmus

    [Fact]
    public void NaechstesTraining_ZaehltDenTagDerZone()
    {
        // Dienstag 14.07., 20:30 in New York; Rhythmus: dienstags um 23 Uhr (Erinnerung 21:00).
        var jetzt = new DateTimeOffset(2026, 7, 15, 0, 30, 0, TimeSpan.Zero);
        var rhythmus = new GespeicherterRhythmus(Tage: 2, Stunde: 23);

        var inNewYork = Trainingsrhythmus.NaechstesTraining(jetzt, [], [], rhythmus, Ortszeit.Zone(NewYork));
        var inBerlin = Trainingsrhythmus.NaechstesTraining(jetzt, [], [], rhythmus);

        Assert.Equal(new DateOnly(2026, 7, 14), inNewYork!.Value.Tag);
        Assert.Equal(new DateOnly(2026, 7, 21), inBerlin!.Value.Tag);
    }

    // ---------------------------------------------------------------- Ruhezeit beim Empfänger

    [Fact]
    public void Gesundheit_RuhezeitUndTagGeltenBeimEmpfaenger()
    {
        // 04:00 UTC: in Berlin Ruhezeit (06:00 im Sommer wäre noch zu früh), in Tokio 13:00 Uhr.
        var jetzt = new DateTimeOffset(2026, 7, 15, 4, 0, 0, TimeSpan.Zero);
        var nutzer = Guid.NewGuid();
        var heuteInTokio = new DateOnly(2026, 7, 15);
        var frist = new GesundheitsAdressat(
            Guid.NewGuid(), Guid.NewGuid(), "Bello", DogHealthKind.Vaccination, "Tollwut", new DateOnly(2026, 1, 1), null,
            heuteInTokio, true, nutzer, true);
        Func<Guid, TimeZoneInfo> tokio = _ => Ortszeit.Zone(Tokio);

        Assert.Empty(Erinnerungsregeln.GesundheitsErinnerungen(jetzt, [frist], NichtsGesendet));
        Assert.Single(Erinnerungsregeln.GesundheitsErinnerungen(jetzt, [frist], NichtsGesendet, tokio));
    }

    [Fact]
    public void Termin_BleibtVereinszeit_DieRuhezeitGiltBeimEmpfaenger()
    {
        // 17:30 UTC = 19:30 Uhr in Berlin (ab 18 Uhr Vortags-Erinnerung); in Tokio 02:30 Uhr nachts, in New York 13:30 Uhr.
        var jetzt = new DateTimeOffset(2026, 7, 14, 17, 30, 0, TimeSpan.Zero);
        var beginn = new DateTimeOffset(2026, 7, 15, 17, 0, 0, TimeSpan.Zero); // Mittwoch 19:00 Uhr in Berlin
        var inTokio = Guid.NewGuid();
        var inNewYork = Guid.NewGuid();
        TerminAdressat Adressat(Guid nutzer) => new(Guid.NewGuid(), "Dienstagsgruppe", beginn, false, nutzer, TerminAntwort.Offen, true);
        Func<Guid, TimeZoneInfo> zonen = id => Ortszeit.Zone(id == inTokio ? Tokio : NewYork);

        var erinnerungen = Erinnerungsregeln.TerminErinnerungen(jetzt, [Adressat(inTokio), Adressat(inNewYork)], NichtsGesendet, zonen);

        var einzige = Assert.Single(erinnerungen);
        Assert.Equal(inNewYork, einzige.Schluessel.UserId);
        // Der Termin selbst bleibt in Vereinszeit: 19:00 Uhr, nicht 13:00.
        Assert.Contains("19:00", Uebersetzung.Rendere(einzige.Meldung, null));
    }

    [Fact]
    public void Zusammenfassung_DerWochentagUndDieStundeGeltenInDerZoneDerTrainerin()
    {
        // 23:30 UTC am Samstag 18.07. = Sonntag 01:30 in Berlin, Samstag 19:30 in New York. Vorgabe: sonntags ab 18 Uhr.
        var jetzt = new DateTimeOffset(2026, 7, 18, 23, 30, 0, TimeSpan.Zero);
        var trainerin = Guid.NewGuid();
        var kandidat = new TrainerKandidat(trainerin, true, Erinnerungsregeln.VorgabeTage, Erinnerungsregeln.VorgabeStunde);
        // Am Sonntag um 18 Uhr in New York: 22:00 UTC.
        var sonntagsAbends = new DateTimeOffset(2026, 7, 19, 22, 30, 0, TimeSpan.Zero);
        Func<Guid, TimeZoneInfo> newYork = _ => Ortszeit.Zone(NewYork);

        Assert.Empty(Erinnerungsregeln.FaelligeTrainer(jetzt, [kandidat], NichtsGesendet, newYork));
        Assert.Single(Erinnerungsregeln.FaelligeTrainer(sonntagsAbends, [kandidat], NichtsGesendet, newYork));
        // Ohne Zone Berlin: Sonntag 01:30 liegt in der Ruhezeit, 00:30 Uhr am Montag nicht mehr Sonntag.
        Assert.Empty(Erinnerungsregeln.FaelligeTrainer(jetzt, [kandidat], NichtsGesendet));
    }
}
