using Dogity.Application.Abstractions;
using Dogity.Application.Sports;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Sports;

namespace Dogity.Application.Tests.Sports;

/// <summary>
/// Welche Fassung einer Prüfungsordnung gilt: die jüngste, die schon begonnen
/// hat; bei gleichem Beginn die später angelegte; gibt es nur künftige, die
/// früheste. Die Regel steht an einer Stelle (RegulationVersionQueries) und
/// wirkt bei Katalog, Plan, Höchstpunkten und Prüfungsreife gleich.
/// </summary>
public class RegulationVersionQueriesTests
{
    private static readonly DateOnly Heute = new(2026, 10, 31);

    private static RegulationVersion Fassung(string label, DateOnly gueltigAb, int angelegtVorTagen = 0) => new()
    {
        RegulationId = Guid.Empty,
        VersionLabel = label,
        ValidFrom = gueltigAb,
        CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(-angelegtVorTagen),
    };

    [Fact]
    public void EineBegonneneFassung_Gilt()
    {
        var fassungen = new[] { Fassung("2025", new DateOnly(2025, 1, 1)) };

        Assert.Equal("2025", fassungen.AktuelleFassung(Heute)!.VersionLabel);
    }

    [Fact]
    public void DieJuengsteBegonnene_GewinntGegenAeltere()
    {
        var fassungen = new[] { Fassung("2024", new DateOnly(2024, 1, 1)), Fassung("2026", new DateOnly(2026, 1, 1)), Fassung("2025", new DateOnly(2025, 1, 1)) };

        Assert.Equal("2026", fassungen.AktuelleFassung(Heute)!.VersionLabel);
    }

    [Fact]
    public void EineKuenftigeFassung_GiltErstAbIhremTag()
    {
        var fassungen = new[] { Fassung("2025", new DateOnly(2025, 1, 1)), Fassung("2027", new DateOnly(2027, 1, 1)) };

        Assert.Equal("2025", fassungen.AktuelleFassung(Heute)!.VersionLabel);
        Assert.Equal("2027", fassungen.AktuelleFassung(new DateOnly(2027, 1, 1))!.VersionLabel);
        Assert.Equal("2025", fassungen.AktuelleFassung(new DateOnly(2026, 12, 31))!.VersionLabel);
    }

    [Fact]
    public void GibtEsNurKuenftige_GiltDieFruehesteVonIhnen()
    {
        // Eine Ordnung, die vor ihrem Stichtag schon angeboten wird, soll Übungen haben.
        var fassungen = new[] { Fassung("2028", new DateOnly(2028, 1, 1)), Fassung("2027", new DateOnly(2027, 1, 1)) };

        Assert.Equal("2027", fassungen.AktuelleFassung(Heute)!.VersionLabel);
    }

    [Fact]
    public void BeiGleichemBeginn_GewinntDieSpaeterAngelegte_UnabhaengigVonDerReihenfolge()
    {
        var alt = Fassung("alt", new DateOnly(2026, 1, 1), angelegtVorTagen: 30);
        var neu = Fassung("neu", new DateOnly(2026, 1, 1), angelegtVorTagen: 1);

        Assert.Equal("neu", new[] { alt, neu }.AktuelleFassung(Heute)!.VersionLabel);
        Assert.Equal("neu", new[] { neu, alt }.AktuelleFassung(Heute)!.VersionLabel);
    }

    [Fact]
    public void BeiGleichemBeginnUndZeitpunkt_EntscheidetDieId_NichtDerZufall()
    {
        var a = Fassung("a", new DateOnly(2026, 1, 1));
        var b = Fassung("b", new DateOnly(2026, 1, 1));
        var kleinere = a.Id.CompareTo(b.Id) < 0 ? a : b;

        Assert.Same(kleinere, new[] { a, b }.AktuelleFassung(Heute));
        Assert.Same(kleinere, new[] { b, a }.AktuelleFassung(Heute));
    }

    [Fact]
    public void OhneFassung_Null()
    {
        Assert.Null(Array.Empty<RegulationVersion>().AktuelleFassung(Heute));
    }

    [Fact]
    public async Task DerKatalog_ZeigtDieGueltigeFassung_NichtDieKuenftige()
    {
        var db = InMemoryDbContext.Create();
        var sport = new Sport { Code = "BH", Name = "BH" };
        var ordnung = new Regulation { SportId = sport.Id, Name = "FCI-BH" };
        var gueltig = new RegulationVersion { RegulationId = ordnung.Id, VersionLabel = "2025", ValidFrom = new DateOnly(2025, 1, 1) };
        var kuenftig = new RegulationVersion { RegulationId = ordnung.Id, VersionLabel = "2027", ValidFrom = new DateOnly(2027, 1, 1) };
        var uebung = new Exercise { SportId = sport.Id, Name = "Sitz" };
        db.Sports.Add(sport);
        db.Regulations.Add(ordnung);
        db.RegulationVersions.AddRange(gueltig, kuenftig);
        db.Exercises.Add(uebung);
        db.RegulationExercises.Add(new RegulationExercise { RegulationVersionId = gueltig.Id, ExerciseId = uebung.Id, MaxPoints = 10 });
        await db.SaveChangesAsync();
        // 31.10.2026 um 12 Uhr: die 2027er Fassung hat noch nicht begonnen.
        var dienst = new SportCatalogService(db, new FesteUhr(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero)));

        var detail = (await dienst.GetRegulationDetailAsync(ordnung.Id)).Value!;
        var liste = (await dienst.GetRegulationsAsync(sport.Id)).Value!;

        Assert.Equal("2025", detail.CurrentVersion.VersionLabel);
        Assert.Single(detail.Exercises);
        Assert.Equal(new DateOnly(2025, 1, 1), Assert.Single(liste).CurrentVersionValidFrom);
    }

    [Fact]
    public async Task ZumStichtagInVereinszeit_WechseltDerKatalogAufDieNeueFassung()
    {
        var db = InMemoryDbContext.Create();
        var sport = new Sport { Code = "BH", Name = "BH" };
        var ordnung = new Regulation { SportId = sport.Id, Name = "FCI-BH" };
        db.Sports.Add(sport);
        db.Regulations.Add(ordnung);
        db.RegulationVersions.AddRange(
            new RegulationVersion { RegulationId = ordnung.Id, VersionLabel = "2026", ValidFrom = new DateOnly(2026, 1, 1) },
            new RegulationVersion { RegulationId = ordnung.Id, VersionLabel = "2027", ValidFrom = new DateOnly(2027, 1, 1) });
        await db.SaveChangesAsync();

        // 31.12. um 23:30 Uhr UTC ist es in Deutschland schon der 1.1.2027.
        var kurzNachMitternacht = new SportCatalogService(db, new FesteUhr(new DateTimeOffset(2026, 12, 31, 23, 30, 0, TimeSpan.Zero)));
        var davor = new SportCatalogService(db, new FesteUhr(new DateTimeOffset(2026, 12, 31, 22, 30, 0, TimeSpan.Zero)));

        Assert.Equal("2027", (await kurzNachMitternacht.GetRegulationDetailAsync(ordnung.Id)).Value!.CurrentVersion.VersionLabel);
        Assert.Equal("2026", (await davor.GetRegulationDetailAsync(ordnung.Id)).Value!.CurrentVersion.VersionLabel);
    }

    private sealed class FesteUhr(DateTimeOffset jetzt) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => jetzt;
    }
}
