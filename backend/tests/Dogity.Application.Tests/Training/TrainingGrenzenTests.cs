using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Training;
using Dogity.Domain.Dogs;

namespace Dogity.Application.Tests.Training;

/// <summary>
/// Fachliche Grenzen einer Trainings-Eintragung, die nicht nur das Formular
/// kennt: Sonst ginge ein Datum in der Zukunft oder eine Dauer von 100000
/// Minuten per Schnittstelle oder aus der Offline-Warteschlange durch - und
/// landete in Statistik, Wochenzählung und Plan-Fortschritt.
/// </summary>
public class TrainingGrenzenTests
{
    /// <summary>15.07. um 01:30 Uhr deutscher Zeit - in UTC noch der 14.</summary>
    private static readonly DateTimeOffset Jetzt = new(2026, 7, 14, 23, 30, 0, TimeSpan.Zero);

    private sealed class FesteUhr : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    private static async Task<(TrainingService Dienst, Guid UserId, Guid DogId)> AufbauenAsync()
    {
        var db = InMemoryDbContext.Create();
        var dienst = new TrainingService(db, new FakeNotificationService(), new FakeUserLookupService(), new ExerciseMasteryService(db), new FakeWeatherEnrichmentService(), new FesteUhr());
        var userId = Guid.NewGuid();
        var hund = new Dog { Name = "Bello" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = userId });
        await db.SaveChangesAsync();
        return (dienst, userId, hund.Id);
    }

    private static CreateTrainingSessionRequest Eintrag(Guid hund, DateOnly datum, int minuten = 30) => new(hund, datum, minuten, null, []);

    [Fact]
    public async Task Datum_HeuteInVereinszeit_Gestern_GehenDurch()
    {
        var (dienst, user, hund) = await AufbauenAsync();

        // Heute ist hier der 15.: In UTC wäre es noch der 14., der Server rechnet aber in Vereinszeit.
        Assert.True((await dienst.CreateAsync(user, Eintrag(hund, new DateOnly(2026, 7, 15)))).Succeeded);
        Assert.True((await dienst.CreateAsync(user, Eintrag(hund, new DateOnly(2026, 7, 14)))).Succeeded);
    }

    [Fact]
    public async Task Datum_ImmerhinMorgen_WirdAbgewiesen()
    {
        var (dienst, user, hund) = await AufbauenAsync();

        var ergebnis = await dienst.CreateAsync(user, Eintrag(hund, new DateOnly(2026, 7, 16)));

        Assert.False(ergebnis.Succeeded);
        Assert.False(ergebnis.IsNotFound);
        Assert.Equal("Das Datum darf nicht in der Zukunft liegen.", ergebnis.Errors[0]);
    }

    [Fact]
    public async Task Verschieben_InDieZukunft_WirdAbgewiesen_HeuteGeht()
    {
        var (dienst, user, hund) = await AufbauenAsync();
        var angelegt = await dienst.CreateAsync(user, Eintrag(hund, new DateOnly(2026, 7, 10)));

        var zuspaet = await dienst.MoveTrainingDayAsync(user, angelegt.Value!.Id, new DateOnly(2026, 7, 16));

        Assert.False(zuspaet.Succeeded);
        Assert.False(zuspaet.IsNotFound);
        Assert.Equal("Das Datum darf nicht in der Zukunft liegen.", zuspaet.Errors[0]);
        Assert.Equal(new DateOnly(2026, 7, 10), (await dienst.GetByIdAsync(user, angelegt.Value.Id)).Value!.Date);
        // Heute in Vereinszeit (15.) ist erlaubt, obwohl es in UTC noch der 14. ist.
        Assert.True((await dienst.MoveTrainingDayAsync(user, angelegt.Value.Id, new DateOnly(2026, 7, 15))).Succeeded);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(TrainingService.MaxDauerMinuten, true)]
    [InlineData(TrainingService.MaxDauerMinuten + 1, false)]
    [InlineData(100_000, false)]
    [InlineData(0, false)]
    public async Task Dauer_HoechstensDieObergrenze(int minuten, bool erlaubt)
    {
        var (dienst, user, hund) = await AufbauenAsync();

        var ergebnis = await dienst.CreateAsync(user, Eintrag(hund, new DateOnly(2026, 7, 15), minuten));

        Assert.Equal(erlaubt, ergebnis.Succeeded);
        if (!erlaubt) Assert.False(ergebnis.IsNotFound);
    }

    [Fact]
    public async Task Dauer_ZuLang_NenntDieGrenze()
    {
        var (dienst, user, hund) = await AufbauenAsync();

        var ergebnis = await dienst.CreateAsync(user, Eintrag(hund, new DateOnly(2026, 7, 15), 601));

        Assert.Equal("Die Dauer darf höchstens 600 Minuten betragen.", ergebnis.Errors[0]);
    }
}
