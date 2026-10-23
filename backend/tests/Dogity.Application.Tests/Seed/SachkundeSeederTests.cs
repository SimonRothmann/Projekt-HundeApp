using Dogity.Application.Learning;
using Dogity.Infrastructure.Persistence;
using Dogity.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Application.Tests.Seed;

/// <summary>
/// Der Seeder mit den ECHTEN Katalogdaten: die Prüfungsgröße kommt aus der
/// JSON-Datei, und die Zusammenstellung der Erwachsenenprüfung ist die, die der
/// Betreiber festgelegt hat (A 8 · B 7 · C 4 · D 4 · E 2).
/// </summary>
public class SachkundeSeederTests
{
    private static async Task<ServiceProvider> GesaetAsync()
    {
        var datenbank = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(datenbank));
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        await SachkundeSeeder.SeedAsync(scope.ServiceProvider);
        return provider;
    }

    [Fact]
    public async Task Kataloge_BekommenDiePruefungsgroesseAusDerDatei()
    {
        await using var provider = await GesaetAsync();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(25, (await db.QuizCatalogs.SingleAsync(c => c.Code == "SWHV-BHVT-ERW")).ExamQuestionCount);
        Assert.Equal(15, (await db.QuizCatalogs.SingleAsync(c => c.Code == "SWHV-BHVT-JGD")).ExamQuestionCount);
    }

    [Fact]
    public async Task ZweitesSeeden_AendertNichtsAnDerPruefungsgroesse()
    {
        await using var provider = await GesaetAsync();
        using (var scope = provider.CreateScope())
            await SachkundeSeeder.SeedAsync(scope.ServiceProvider);
        using var pruef = provider.CreateScope();
        var db = pruef.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(2, await db.QuizCatalogs.CountAsync());
        Assert.Equal(25, (await db.QuizCatalogs.SingleAsync(c => c.Code == "SWHV-BHVT-ERW")).ExamQuestionCount);
    }

    [Fact]
    public async Task Erwachsenenpruefung_HatDieFestgelegteZusammenstellung()
    {
        await using var provider = await GesaetAsync();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dienst = new SachkundeService(db, TimeProvider.System);

        var pruefung = (await dienst.StartExamAsync("SWHV-BHVT-ERW")).Value!;

        Assert.Equal(25, pruefung.QuestionCount);
        Assert.Equal(50, pruefung.MaxPoints);
        Assert.Equal(35, pruefung.PassPoints);
        var jeKomplex = pruefung.Questions.GroupBy(q => q.Section).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(new Dictionary<string, int> { ["A"] = 8, ["B"] = 7, ["C"] = 4, ["D"] = 4, ["E"] = 2 }, jeKomplex);
        // Die drei Freitextfragen (D5, D6, D8) sind nie dabei.
        Assert.DoesNotContain(pruefung.Questions, q => q.Kind == "FreeText");
    }

    [Fact]
    public async Task Jugendpruefung_Stellt15Fragen_ZufaelligAus40()
    {
        await using var provider = await GesaetAsync();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dienst = new SachkundeService(db, TimeProvider.System);

        var pruefung = (await dienst.StartExamAsync("SWHV-BHVT-JGD")).Value!;

        Assert.Equal(15, pruefung.QuestionCount);
        Assert.Equal(30, pruefung.MaxPoints);
        Assert.Equal(21, pruefung.PassPoints);
        Assert.Equal(15, pruefung.Questions.Select(q => q.Id).Distinct().Count());

        // Zufällig aus den 40: zwei Ziehungen sind nicht dieselbe Auswahl (die Chance
        // auf Gleichheit liegt bei 1 : 40 Milliarden).
        var zweite = (await dienst.StartExamAsync("SWHV-BHVT-JGD")).Value!;
        Assert.NotEqual(
            pruefung.Questions.Select(q => q.Id).Order(), zweite.Questions.Select(q => q.Id).Order());
    }

    [Fact]
    public async Task Katalogliste_ZeigtDieInfoBeiderKataloge()
    {
        await using var provider = await GesaetAsync();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dienst = new SachkundeService(db, TimeProvider.System);

        var kataloge = (await dienst.GetCatalogsAsync()).Value!.ToDictionary(k => k.Code);

        Assert.Equal(new QuizExamInfoDto(25, 50, 35, 70), kataloge["SWHV-BHVT-ERW"].Exam);
        Assert.Equal(new QuizExamInfoDto(15, 30, 21, 70), kataloge["SWHV-BHVT-JGD"].Exam);
    }

    [Fact]
    public async Task SeederSetztDiePruefungsgroesseImmerAusDerDatei()
    {
        // Die Datei ist die einzige Quelle: ein von Hand geleerter Wert kommt
        // beim nächsten Start wieder, es gibt keinen zweiten Pfad, der ihn setzt.
        await using var provider = await GesaetAsync();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var katalog = await db.QuizCatalogs.SingleAsync(c => c.Code == "SWHV-BHVT-ERW");
        katalog.ExamQuestionCount = null;
        await db.SaveChangesAsync();

        await SachkundeSeeder.SeedAsync(scope.ServiceProvider);

        Assert.Equal(25, (await db.QuizCatalogs.SingleAsync(c => c.Code == "SWHV-BHVT-ERW")).ExamQuestionCount);
    }
}
