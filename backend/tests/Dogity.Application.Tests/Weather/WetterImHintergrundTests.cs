using Dogity.Application.Abstractions;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Planning;
using Dogity.Application.Tracking;
using Dogity.Application.Training;
using Dogity.Application.Weather;
using Dogity.Domain.Dogs;
using Dogity.Domain.Sports;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dogity.Application.Tests.Weather;

/// <summary>
/// Das Wetter wird nach dem Speichern im Hintergrund nachgetragen: Speichern
/// wartet nie auf den Wetterdienst (Lasttest-Fund: Hängt Open-Meteo, dauerte
/// das Speichern einer Fährte so lange wie dessen Zeitlimit).
/// </summary>
public class WetterImHintergrundTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly WeatherReading Sonne = new(18.5, 60, 7.2, 0);
    private static readonly WeatherReading Regen = new(9.5, 90, 12, 63);

    private sealed class FesteUhr : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    /// <summary>Wetterdienst, der zählt, was ihn erreicht - und antwortet, schweigt oder scheitert.</summary>
    private sealed class FakeWetterdienst : IWeatherProvider
    {
        public int Aufrufe { get; private set; }
        public Func<DateTimeOffset, WeatherReading?> Antwort { get; set; } = _ => Sonne;
        public bool Wirft { get; set; }

        public Task<WeatherReading?> GetAtAsync(double latitude, double longitude, DateTimeOffset instant, CancellationToken ct = default)
        {
            Aufrufe++;
            if (Wirft) throw new HttpRequestException("Open-Meteo hängt.");
            return Task.FromResult(Antwort(instant));
        }
    }

    private sealed class MerkendeQueue : IWetterQueue
    {
        public List<WetterAuftrag> Auftraege { get; } = [];

        public bool Enqueue(WetterAuftrag auftrag)
        {
            Auftraege.Add(auftrag);
            return true;
        }
    }

    private sealed class Aufsetzen
    {
        public ApplicationDbContext Db { get; } = InMemoryDbContext.Create();
        public FakeWetterdienst Wetterdienst { get; } = new();
        public MerkendeQueue Queue { get; } = new();
        public Guid Nutzer { get; } = Guid.NewGuid();
        public Guid HundId { get; }
        public GpsTrackService Fahrten { get; }
        public TrainingService Trainings { get; }
        public WetterNachtrag Nachtrag { get; }

        public Aufsetzen()
        {
            var hund = new Dog { Name = "Bello" };
            Db.Dogs.Add(hund);
            Db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = Nutzer, Role = DogOwnerRole.Owner });
            Db.SaveChanges();
            HundId = hund.Id;

            var anreicherung = new WeatherEnrichmentService(Db, Wetterdienst, Queue);
            Fahrten = new GpsTrackService(Db, anreicherung);
            Trainings = new TrainingService(Db, new FakeNotificationService(), new FakeUserLookupService(), new ExerciseMasteryService(Db), anreicherung, TimeProvider.System);
            Nachtrag = new WetterNachtrag(Db, Wetterdienst, new FesteUhr(), NullLogger<WetterNachtrag>.Instance);
        }

        public static CreateGpsTrackRequest FaehrteMitPunkten(Guid hundId, DateTimeOffset start) => new(
            null, 300, null, null, null, null, null,
            [
                new CreateGpsPointRequest(48.9, 8.4, start, 5),
                new CreateGpsPointRequest(48.9005, 8.4005, start.AddMinutes(10), 5),
                // Ein manueller Marker mit späterem Zeitstempel darf nicht als Legezeit gelten.
                new CreateGpsPointRequest(48.9003, 8.4003, start.AddMinutes(40), 5, Domain.Tracking.GpsPointType.Manual)
            ],
            DogId: hundId, Date: DateOnly.FromDateTime(start.UtcDateTime));

        public static CreateGpsWalkRunRequest Ablauf(DateTimeOffset start) => new(
            300, null,
            [new CreateGpsWalkPointRequest(48.9, 8.4, start, 5), new CreateGpsWalkPointRequest(48.9005, 8.4005, start.AddMinutes(8), 5)]);

        public CreateTrainingSessionRequest TrainingMitOrt(TimeOnly? zeit = null, double? breite = 48.9) => new(
            HundId, new DateOnly(2026, 10, 18), 20, null,
            [new CreateTrainingExerciseRequest(null, 4, ExerciseDifficulty.Beginner, true, null, null, "Sitz")],
            StartTime: zeit ?? new TimeOnly(9, 30), Latitude: breite, Longitude: breite is null ? null : 8.4);
    }

    // ------------------------------------------------------------ Fährte

    [Fact]
    public async Task FaehrteSpeichern_FragtDenWetterdienstNichtUndReihtDasLegenEin()
    {
        var a = new Aufsetzen { };
        a.Wetterdienst.Wirft = true;
        var start = Jetzt.AddHours(-1);

        var ergebnis = await a.Fahrten.CreateAsync(a.Nutzer, Aufsetzen.FaehrteMitPunkten(a.HundId, start));

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(0, a.Wetterdienst.Aufrufe);
        Assert.Null(ergebnis.Value!.LaidTemperatureC);
        var auftrag = Assert.Single(a.Queue.Auftraege);
        Assert.Equal(WetterZiel.FaehrteGelegt, auftrag.Ziel);
        Assert.Equal(ergebnis.Value.Id, auftrag.Id);
        // Ort und Zeit des ersten automatischen Punkts, nicht des späteren Markers.
        Assert.Equal(48.9, auftrag.Breite);
        Assert.Equal(start, auftrag.Zeitpunkt);
    }

    [Fact]
    public async Task FaehrteSpeichern_SchneidetDenZeitpunktAufSekunden_DamitDieGegenprobeNachDemRoundtripPasst()
    {
        var a = new Aufsetzen();
        // Mehr als Mikrosekunden-Genauigkeit, wie sie timestamptz nicht speichert.
        var start = Jetzt.AddHours(-1).AddTicks(1234567);

        await a.Fahrten.CreateAsync(a.Nutzer, Aufsetzen.FaehrteMitPunkten(a.HundId, start));

        var auftrag = Assert.Single(a.Queue.Auftraege);
        Assert.Equal(Jetzt.AddHours(-1).AddSeconds(0), auftrag.Zeitpunkt);
        Assert.Equal(0, auftrag.Zeitpunkt.Ticks % TimeSpan.TicksPerSecond);
    }

    [Fact]
    public async Task Nachtrag_TraegtDasWetterEinUndSetztDenAbrufzeitpunkt()
    {
        var a = new Aufsetzen();
        var id = (await a.Fahrten.CreateAsync(a.Nutzer, Aufsetzen.FaehrteMitPunkten(a.HundId, Jetzt.AddHours(-1)))).Value!.Id;

        var eingetragen = await a.Nachtrag.NachtragenAsync(Assert.Single(a.Queue.Auftraege));

        Assert.True(eingetragen);
        var track = await a.Db.GpsTracks.SingleAsync(t => t.Id == id);
        Assert.Equal(18.5, track.LaidTemperatureC);
        Assert.Equal(60, track.LaidRelativeHumidity);
        Assert.Equal(0, track.LaidWeatherCode);
        Assert.Null(track.SearchTemperatureC);
        Assert.Equal(Jetzt, track.WeatherFetchedAt);
    }

    [Fact]
    public async Task ErsterAblauf_ReihtNurDasSuchenEin_WennLegenSchonWetterHat()
    {
        var a = new Aufsetzen();
        var start = Jetzt.AddHours(-3);
        var track = (await a.Fahrten.CreateAsync(a.Nutzer, Aufsetzen.FaehrteMitPunkten(a.HundId, start))).Value!;
        await a.Nachtrag.NachtragenAsync(Assert.Single(a.Queue.Auftraege));
        a.Queue.Auftraege.Clear();
        var sucheStart = start.AddHours(2);

        var lauf = await a.Fahrten.AddWalkRunAsync(a.Nutzer, track.Id, Aufsetzen.Ablauf(sucheStart));

        Assert.True(lauf.Succeeded);
        var auftrag = Assert.Single(a.Queue.Auftraege);
        Assert.Equal(WetterZiel.FaehrteGesucht, auftrag.Ziel);
        Assert.Equal(sucheStart, auftrag.Zeitpunkt);

        a.Wetterdienst.Antwort = _ => Regen;
        await a.Nachtrag.NachtragenAsync(auftrag);
        var gespeichert = await a.Db.GpsTracks.SingleAsync(t => t.Id == track.Id);
        Assert.Equal(9.5, gespeichert.SearchTemperatureC);
        Assert.Equal(18.5, gespeichert.LaidTemperatureC);
        Assert.Equal(-9.0, gespeichert.TemperatureDeltaC);
    }

    [Fact]
    public async Task GeloeschteFaehrte_WirdVomNachtragNichtWiederbelebt()
    {
        var a = new Aufsetzen();
        var id = (await a.Fahrten.CreateAsync(a.Nutzer, Aufsetzen.FaehrteMitPunkten(a.HundId, Jetzt.AddHours(-1)))).Value!.Id;
        Assert.True((await a.Fahrten.DeleteAsync(a.Nutzer, id)).Succeeded);

        var eingetragen = await a.Nachtrag.NachtragenAsync(Assert.Single(a.Queue.Auftraege));

        Assert.False(eingetragen);
        var track = await a.Db.GpsTracks.IgnoreQueryFilters().SingleAsync(t => t.Id == id);
        Assert.NotNull(track.DeletedAt);
        Assert.Null(track.LaidTemperatureC);
        Assert.Null(track.WeatherFetchedAt);
    }

    [Fact]
    public async Task VorhandenerFaehrtenwert_WirdNichtUeberschrieben()
    {
        var a = new Aufsetzen();
        var id = (await a.Fahrten.CreateAsync(a.Nutzer, Aufsetzen.FaehrteMitPunkten(a.HundId, Jetzt.AddHours(-1)))).Value!.Id;
        var track = await a.Db.GpsTracks.SingleAsync(t => t.Id == id);
        track.LaidTemperatureC = 3.0;
        await a.Db.SaveChangesAsync();

        var eingetragen = await a.Nachtrag.NachtragenAsync(Assert.Single(a.Queue.Auftraege));

        Assert.False(eingetragen);
        Assert.Equal(3.0, (await a.Db.GpsTracks.SingleAsync(t => t.Id == id)).LaidTemperatureC);
    }

    [Fact]
    public async Task WetterdienstOhneAntwortOderMitFehler_LaesstDieFaehrteUnveraendert()
    {
        var a = new Aufsetzen();
        var id = (await a.Fahrten.CreateAsync(a.Nutzer, Aufsetzen.FaehrteMitPunkten(a.HundId, Jetzt.AddHours(-1)))).Value!.Id;
        var auftrag = Assert.Single(a.Queue.Auftraege);

        a.Wetterdienst.Antwort = _ => null;
        Assert.False(await a.Nachtrag.NachtragenAsync(auftrag));

        a.Wetterdienst.Wirft = true;
        Assert.False(await a.Nachtrag.NachtragenAsync(auftrag));

        var track = await a.Db.GpsTracks.SingleAsync(t => t.Id == id);
        Assert.Null(track.LaidTemperatureC);
        Assert.Null(track.WeatherFetchedAt);
        Assert.Equal(2, a.Wetterdienst.Aufrufe);
    }

    [Fact]
    public async Task WetterLadenPerKnopf_BleibtSofortUndFuelltBeideWerte()
    {
        var a = new Aufsetzen();
        var start = Jetzt.AddHours(-3);
        var track = (await a.Fahrten.CreateAsync(a.Nutzer, Aufsetzen.FaehrteMitPunkten(a.HundId, start))).Value!;
        await a.Fahrten.AddWalkRunAsync(a.Nutzer, track.Id, Aufsetzen.Ablauf(start.AddHours(2)));
        a.Queue.Auftraege.Clear();
        a.Wetterdienst.Antwort = zeit => zeit == start ? Sonne : Regen;

        var ergebnis = await a.Fahrten.RefreshWeatherAsync(a.Nutzer, track.Id);

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(18.5, ergebnis.Value!.LaidTemperatureC);
        Assert.Equal(9.5, ergebnis.Value.SearchTemperatureC);
        Assert.Empty(a.Queue.Auftraege);
    }

    // ------------------------------------------------------------ Training

    [Fact]
    public async Task TrainingSpeichern_MitOrtUndZeit_FragtNichtUndReihtEin()
    {
        var a = new Aufsetzen();
        a.Wetterdienst.Wirft = true;

        var ergebnis = await a.Trainings.CreateAsync(a.Nutzer, a.TrainingMitOrt());

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(0, a.Wetterdienst.Aufrufe);
        Assert.Null(ergebnis.Value!.TemperatureC);
        var auftrag = Assert.Single(a.Queue.Auftraege);
        Assert.Equal(WetterZiel.Training, auftrag.Ziel);
        Assert.Equal(ergebnis.Value.Id, auftrag.Id);
        Assert.Equal(new DateTimeOffset(2026, 10, 18, 9, 30, 0, TimeSpan.Zero), auftrag.Zeitpunkt);
    }

    [Fact]
    public async Task TrainingSpeichern_OhneOrt_ReihtNichtsEin()
    {
        var a = new Aufsetzen();

        Assert.True((await a.Trainings.CreateAsync(a.Nutzer, a.TrainingMitOrt(breite: null))).Succeeded);

        Assert.Empty(a.Queue.Auftraege);
    }

    [Fact]
    public async Task TrainingNachtrag_TraegtInDieselbenFelderWieBisher()
    {
        var a = new Aufsetzen();
        var id = (await a.Trainings.CreateAsync(a.Nutzer, a.TrainingMitOrt())).Value!.Id;

        Assert.True(await a.Nachtrag.NachtragenAsync(Assert.Single(a.Queue.Auftraege)));

        var session = await a.Db.TrainingSessions.SingleAsync(s => s.Id == id);
        Assert.Equal(18.5, session.TemperatureC);
        Assert.Equal(60, session.RelativeHumidity);
        Assert.Equal(7.2, session.WindSpeedKmh);
        Assert.Equal(0, session.WeatherCode);
        Assert.Equal(Jetzt, session.WeatherFetchedAt);
    }

    [Fact]
    public async Task GeaenderteUhrzeit_VerwirftDenAltenAuftragUndDerNeueFuelltDasFeld()
    {
        var a = new Aufsetzen();
        var id = (await a.Trainings.CreateAsync(a.Nutzer, a.TrainingMitOrt())).Value!.Id;
        var alt = Assert.Single(a.Queue.Auftraege);

        // Die Uhrzeit wird korrigiert, bevor der erste Auftrag dran war.
        await a.Trainings.SetSessionContextAsync(a.Nutzer, id, new UpdateSessionContextRequest(new TimeOnly(15, 0), 48.9, 8.4, null));
        var neu = a.Queue.Auftraege[^1];
        Assert.NotEqual(alt, neu);

        Assert.False(await a.Nachtrag.NachtragenAsync(alt));
        Assert.Null((await a.Db.TrainingSessions.SingleAsync(s => s.Id == id)).TemperatureC);

        Assert.True(await a.Nachtrag.NachtragenAsync(neu));
        Assert.Equal(18.5, (await a.Db.TrainingSessions.SingleAsync(s => s.Id == id)).TemperatureC);
    }

    [Fact]
    public async Task OrtGeaendert_LeertDenAlteWertSofortUndReihtNeuEin()
    {
        var a = new Aufsetzen();
        var id = (await a.Trainings.CreateAsync(a.Nutzer, a.TrainingMitOrt())).Value!.Id;
        await a.Nachtrag.NachtragenAsync(Assert.Single(a.Queue.Auftraege));
        a.Queue.Auftraege.Clear();

        var ergebnis = await a.Trainings.SetSessionContextAsync(a.Nutzer, id, new UpdateSessionContextRequest(new TimeOnly(9, 30), 50.1, 8.7, null));

        // Lieber kein Wert als der Wert eines anderen Orts.
        Assert.Null(ergebnis.Value!.TemperatureC);
        Assert.Single(a.Queue.Auftraege);
    }

    [Fact]
    public async Task GeloeschtesTraining_WirdVomNachtragNichtWiederbelebt()
    {
        var a = new Aufsetzen();
        var id = (await a.Trainings.CreateAsync(a.Nutzer, a.TrainingMitOrt())).Value!.Id;
        Assert.True((await a.Trainings.DeleteAsync(a.Nutzer, id)).Succeeded);

        Assert.False(await a.Nachtrag.NachtragenAsync(Assert.Single(a.Queue.Auftraege)));

        var session = await a.Db.TrainingSessions.IgnoreQueryFilters().SingleAsync(s => s.Id == id);
        Assert.NotNull(session.DeletedAt);
        Assert.Null(session.TemperatureC);
    }

    [Fact]
    public async Task WetterdienstFehlerBeimTraining_LaesstDenDatensatzUnveraendert()
    {
        var a = new Aufsetzen();
        var id = (await a.Trainings.CreateAsync(a.Nutzer, a.TrainingMitOrt())).Value!.Id;
        var auftrag = Assert.Single(a.Queue.Auftraege);

        a.Wetterdienst.Wirft = true;
        Assert.False(await a.Nachtrag.NachtragenAsync(auftrag));
        a.Wetterdienst.Wirft = false;
        a.Wetterdienst.Antwort = _ => null;
        Assert.False(await a.Nachtrag.NachtragenAsync(auftrag));

        var session = await a.Db.TrainingSessions.SingleAsync(s => s.Id == id);
        Assert.Null(session.TemperatureC);
        Assert.Null(session.WeatherFetchedAt);
    }
}
