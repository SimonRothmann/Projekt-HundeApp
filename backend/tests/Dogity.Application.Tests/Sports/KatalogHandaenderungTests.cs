using System.Reflection;
using Dogity.Api.Controllers;
using Dogity.Application.Sports;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Sports;
using Dogity.Infrastructure.Identity;
using Dogity.Infrastructure.Persistence;
using Dogity.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Application.Tests.Sports;

/// <summary>
/// Paket K1: Der Katalog-Abgleich (SportCatalogSeeder) lässt Handänderungen aus
/// dem Katalog-Editor in Ruhe - wie der SachkundeSeeder bei bearbeiteten Fragen -
/// und Korrekturen aus dem Code kommen dort weiter an, wo niemand von Hand
/// geändert hat. Läuft gegen den echten Seeder (InMemory).
/// </summary>
public class KatalogHandaenderungTests
{
    private static async Task<(ApplicationDbContext Db, ServiceProvider Dienste)> GesaetAsync()
    {
        var db = InMemoryDbContext.Create();
        var dienste = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        await SportCatalogSeeder.SeedAsync(dienste);
        return (db, dienste);
    }

    private static Task<Regulation> OrdnungAsync(ApplicationDbContext db, string schluessel = "BH") =>
        db.Regulations.IgnoreQueryFilters().SingleAsync(r => r.SeedKey == schluessel && r.Sport!.Code == "BH");

    private static async Task<Guid> VersionIdAsync(ApplicationDbContext db, Guid ordnungId) =>
        (await db.RegulationVersions.Where(v => v.RegulationId == ordnungId).OrderByDescending(v => v.ValidFrom).FirstAsync()).Id;

    private static Task<List<RegulationExercise>> ZeilenAsync(ApplicationDbContext db, Guid versionId) =>
        db.RegulationExercises.IgnoreQueryFilters().Include(re => re.Exercise).Where(re => re.RegulationVersionId == versionId).ToListAsync();

    private static RegulationManagementService Verwaltung(ApplicationDbContext db) => new(db, TimeProvider.System);

    // ----- Schlüssel -------------------------------------------------------

    [Fact]
    public async Task Seeder_SetztDenSchluesselAnJederGlobalenOrdnung()
    {
        var (db, _) = await GesaetAsync();

        var ohne = await db.Regulations.Where(r => r.SeedKey == null || r.SeedKey != r.Name).CountAsync();
        Assert.Equal(0, ohne);
        Assert.True(await db.Regulations.AnyAsync());
    }

    [Fact]
    public async Task Seeder_FuelltFehlendenSchluesselUeberDenNamen_OhneDuplikat()
    {
        var (db, dienste) = await GesaetAsync();
        var anzahl = await db.Regulations.CountAsync();
        var bh = await OrdnungAsync(db);
        bh.SeedKey = null; // Bestand vor der Migration
        await db.SaveChangesAsync();

        await SportCatalogSeeder.SeedAsync(dienste);

        Assert.Equal(anzahl, await db.Regulations.CountAsync());
        Assert.Equal("BH", (await db.Regulations.SingleAsync(r => r.Id == bh.Id)).SeedKey);
    }

    // ----- Ordnung ---------------------------------------------------------

    [Fact]
    public async Task Ordnung_VonHandGeaendert_BleibtBeimNaechstenLaufStehen()
    {
        var (db, dienste) = await GesaetAsync();
        var bh = await OrdnungAsync(db);
        bh.Name = "Begleithund neu";
        bh.NameEn = "Companion";
        bh.Description = "Meine Beschreibung";
        bh.DescriptionEn = "My description";
        bh.EditedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        await SportCatalogSeeder.SeedAsync(dienste);

        var danach = await db.Regulations.SingleAsync(r => r.Id == bh.Id);
        Assert.Equal("Begleithund neu", danach.Name);
        Assert.Equal("Companion", danach.NameEn);
        Assert.Equal("Meine Beschreibung", danach.Description);
        Assert.Equal("My description", danach.DescriptionEn);
        Assert.NotNull(danach.EditedAt);
    }

    [Fact]
    public async Task Umbenennen_ErzeugtBeimNaechstenLaufKeinDuplikat()
    {
        var (db, dienste) = await GesaetAsync();
        var anzahl = await db.Regulations.CountAsync();
        var bh = await OrdnungAsync(db);
        var umbenennen = await Verwaltung(db).UpdateRegulationAsync(Guid.NewGuid(), true, bh.Id,
            new UpdateRegulationRequest("Begleithund neu", bh.Description, bh.SourceUrl, bh.CountryCode, bh.NameEn, bh.DescriptionEn));
        Assert.True(umbenennen.Succeeded);

        await SportCatalogSeeder.SeedAsync(dienste);

        Assert.Equal(anzahl, await db.Regulations.CountAsync());
        var danach = await db.Regulations.SingleAsync(r => r.Name == "Begleithund neu");
        Assert.Equal(bh.Id, danach.Id);
        Assert.False(await db.Regulations.AnyAsync(r => r.Name == "BH" && r.SportId == bh.SportId));
    }

    [Fact]
    public async Task Ordnung_OhneHandaenderung_BekommtKorrekturAusDemSeed()
    {
        var (db, dienste) = await GesaetAsync();
        var bh = await OrdnungAsync(db);
        var katalogName = bh.Name;
        var katalogText = bh.Description;
        bh.Name = "Irgendwas";
        bh.Description = "Veraltet";
        await db.SaveChangesAsync();

        await SportCatalogSeeder.SeedAsync(dienste);

        var danach = await db.Regulations.SingleAsync(r => r.Id == bh.Id);
        Assert.Equal(katalogName, danach.Name);
        Assert.Equal(katalogText, danach.Description);
    }

    [Fact]
    public async Task Ordnung_Zuruecknehmen_StelltDieKatalogfassungBeimNaechstenLaufWiederHer()
    {
        var (db, dienste) = await GesaetAsync();
        var bh = await OrdnungAsync(db);
        var katalogName = bh.Name;
        var katalogText = bh.Description;
        var katalogEn = bh.NameEn;
        Assert.True((await Verwaltung(db).UpdateRegulationAsync(Guid.NewGuid(), true, bh.Id,
            new UpdateRegulationRequest("Begleithund neu", "Meine Beschreibung", bh.SourceUrl, bh.CountryCode, "Companion", "Mine"))).Succeeded);
        await SportCatalogSeeder.SeedAsync(dienste);
        Assert.Equal("Begleithund neu", (await db.Regulations.SingleAsync(r => r.Id == bh.Id)).Name);

        var zurueck = await Verwaltung(db).RevertRegulationAsync(Guid.NewGuid(), true, bh.Id);
        Assert.True(zurueck.Succeeded);
        Assert.Null((await db.Regulations.SingleAsync(r => r.Id == bh.Id)).EditedAt);
        await SportCatalogSeeder.SeedAsync(dienste);

        var danach = await db.Regulations.SingleAsync(r => r.Id == bh.Id);
        Assert.Equal(katalogName, danach.Name);
        Assert.Equal(katalogText, danach.Description);
        Assert.Equal(katalogEn, danach.NameEn);
        Assert.Null(danach.EditedAt);
    }

    // ----- Übungszuordnung -------------------------------------------------

    [Fact]
    public async Task Zuordnung_VonHandGeaendert_BehaeltPunkteUndHinweis()
    {
        var (db, dienste) = await GesaetAsync();
        var bh = await OrdnungAsync(db);
        var versionId = await VersionIdAsync(db, bh.Id);
        var zeile = (await ZeilenAsync(db, versionId)).First(re => re.MaxPoints > 0);
        var uebungId = zeile.ExerciseId;
        Assert.True((await Verwaltung(db).UpdateRegulationExerciseAsync(Guid.NewGuid(), true, bh.Id, uebungId,
            new UpdateRegulationExerciseRequest(!zeile.IsMandatory, zeile.MaxPoints + 7, "Meine Notiz"))).Succeeded);
        var punkte = zeile.MaxPoints;

        await SportCatalogSeeder.SeedAsync(dienste);

        var danach = (await ZeilenAsync(db, versionId)).Single(re => re.ExerciseId == uebungId);
        Assert.Equal(punkte, danach.MaxPoints);
        Assert.Equal("Meine Notiz", danach.ScoringNotes);
        Assert.NotNull(danach.EditedAt);
    }

    [Fact]
    public async Task Zuordnung_VonHandEntfernt_BleibtEntfernt_OhneNeueZeile()
    {
        var (db, dienste) = await GesaetAsync();
        var bh = await OrdnungAsync(db);
        var versionId = await VersionIdAsync(db, bh.Id);
        var uebungId = (await ZeilenAsync(db, versionId)).First().ExerciseId;
        var vorher = (await ZeilenAsync(db, versionId)).Count;
        Assert.True((await Verwaltung(db).RemoveRegulationExerciseAsync(Guid.NewGuid(), true, bh.Id, uebungId)).Succeeded);

        await SportCatalogSeeder.SeedAsync(dienste);

        var zeilen = await ZeilenAsync(db, versionId);
        Assert.Equal(vorher, zeilen.Count);
        var entfernt = zeilen.Single(re => re.ExerciseId == uebungId);
        Assert.NotNull(entfernt.DeletedAt);
        Assert.NotNull(entfernt.EditedAt);
    }

    [Fact]
    public async Task Zuordnung_VonHandHinzugefuegt_WirdNichtAlsNichtImSeedEntfernt()
    {
        var (db, dienste) = await GesaetAsync();
        var bh = await OrdnungAsync(db);
        var versionId = await VersionIdAsync(db, bh.Id);
        var uebung = new Exercise { SportId = bh.SportId, Name = "Eigene Übung" };
        db.Exercises.Add(uebung);
        await db.SaveChangesAsync();
        Assert.True((await Verwaltung(db).AddRegulationExerciseAsync(Guid.NewGuid(), true, bh.Id,
            new AddRegulationExerciseRequest(uebung.Id, true, 5, null))).Succeeded);

        await SportCatalogSeeder.SeedAsync(dienste);

        var zeile = (await ZeilenAsync(db, versionId)).Single(re => re.ExerciseId == uebung.Id);
        Assert.Null(zeile.DeletedAt);
        Assert.Equal(5, zeile.MaxPoints);
    }

    [Fact]
    public async Task Zuordnung_OhneHandaenderung_BekommtKorrekturAusDemSeed()
    {
        var (db, dienste) = await GesaetAsync();
        var bh = await OrdnungAsync(db);
        var versionId = await VersionIdAsync(db, bh.Id);
        var zeilen = await ZeilenAsync(db, versionId);
        var punkte = zeilen.First(re => re.MaxPoints > 0);
        var katalogPunkte = punkte.MaxPoints;
        var geloescht = zeilen.First(re => re.ExerciseId != punkte.ExerciseId);
        punkte.MaxPoints = 1;
        geloescht.DeletedAt = DateTimeOffset.UtcNow;
        var fremd = new Exercise { SportId = bh.SportId, Name = "Nicht im Seed" };
        db.Exercises.Add(fremd);
        db.RegulationExercises.Add(new RegulationExercise { RegulationVersionId = versionId, ExerciseId = fremd.Id, MaxPoints = 3 });
        await db.SaveChangesAsync();

        await SportCatalogSeeder.SeedAsync(dienste);

        var danach = await ZeilenAsync(db, versionId);
        Assert.Equal(katalogPunkte, danach.Single(re => re.ExerciseId == punkte.ExerciseId).MaxPoints);
        Assert.Null(danach.Single(re => re.ExerciseId == geloescht.ExerciseId).DeletedAt);
        Assert.DoesNotContain(danach, re => re.ExerciseId == fremd.Id);
    }

    [Fact]
    public async Task Zuordnung_Zuruecknehmen_BelebtEntfernteKatalogzeileWieder_UndStelltPunkteHer()
    {
        var (db, dienste) = await GesaetAsync();
        var bh = await OrdnungAsync(db);
        var versionId = await VersionIdAsync(db, bh.Id);
        var zeilen = await ZeilenAsync(db, versionId);
        var geaendert = zeilen.First(re => re.MaxPoints > 0);
        var entfernt = zeilen.First(re => re.ExerciseId != geaendert.ExerciseId);
        var katalogPunkte = geaendert.MaxPoints;
        var verwaltung = Verwaltung(db);
        Assert.True((await verwaltung.UpdateRegulationExerciseAsync(Guid.NewGuid(), true, bh.Id, geaendert.ExerciseId,
            new UpdateRegulationExerciseRequest(geaendert.IsMandatory, katalogPunkte + 5, null))).Succeeded);
        Assert.True((await verwaltung.RemoveRegulationExerciseAsync(Guid.NewGuid(), true, bh.Id, entfernt.ExerciseId)).Succeeded);
        await SportCatalogSeeder.SeedAsync(dienste);

        Assert.True((await verwaltung.RevertRegulationExerciseAsync(Guid.NewGuid(), true, bh.Id, geaendert.ExerciseId)).Succeeded);
        Assert.True((await verwaltung.RevertRegulationExerciseAsync(Guid.NewGuid(), true, bh.Id, entfernt.ExerciseId)).Succeeded);
        await SportCatalogSeeder.SeedAsync(dienste);

        var danach = await ZeilenAsync(db, versionId);
        Assert.Equal(katalogPunkte, danach.Single(re => re.ExerciseId == geaendert.ExerciseId).MaxPoints);
        Assert.Null(danach.Single(re => re.ExerciseId == entfernt.ExerciseId).DeletedAt);
        Assert.All(danach, re => Assert.Null(re.EditedAt));
    }

    [Fact]
    public async Task Zuordnung_HinzugefuegtUndZurueckgenommen_RaeumtDerNaechsteLaufWeg()
    {
        var (db, dienste) = await GesaetAsync();
        var bh = await OrdnungAsync(db);
        var versionId = await VersionIdAsync(db, bh.Id);
        var uebung = new Exercise { SportId = bh.SportId, Name = "Eigene Übung" };
        db.Exercises.Add(uebung);
        await db.SaveChangesAsync();
        var verwaltung = Verwaltung(db);
        Assert.True((await verwaltung.AddRegulationExerciseAsync(Guid.NewGuid(), true, bh.Id, new AddRegulationExerciseRequest(uebung.Id, true, 5, null))).Succeeded);
        Assert.True((await verwaltung.RevertRegulationExerciseAsync(Guid.NewGuid(), true, bh.Id, uebung.Id)).Succeeded);

        await SportCatalogSeeder.SeedAsync(dienste);

        Assert.DoesNotContain(await ZeilenAsync(db, versionId), re => re.ExerciseId == uebung.Id);
    }

    // ----- Verwaltung: EditedAt nur bei echter Änderung ---------------------

    [Fact]
    public async Task Speichern_OhneAenderung_SetztEditedAtNicht()
    {
        var (db, _) = await GesaetAsync();
        var bh = await OrdnungAsync(db);
        var versionId = await VersionIdAsync(db, bh.Id);
        var zeile = (await ZeilenAsync(db, versionId)).First();
        var verwaltung = Verwaltung(db);

        var ordnung = await verwaltung.UpdateRegulationAsync(Guid.NewGuid(), true, bh.Id,
            new UpdateRegulationRequest(bh.Name, bh.Description, bh.SourceUrl, bh.CountryCode, bh.NameEn, bh.DescriptionEn));
        var uebung = await verwaltung.UpdateRegulationExerciseAsync(Guid.NewGuid(), true, bh.Id, zeile.ExerciseId,
            new UpdateRegulationExerciseRequest(zeile.IsMandatory, zeile.MaxPoints, zeile.ScoringNotes, zeile.ScoringNotesEn));

        Assert.True(ordnung.Succeeded);
        Assert.Null(ordnung.Value!.EditedAt);
        Assert.True(uebung.Succeeded);
        Assert.Null((await db.RegulationExercises.SingleAsync(re => re.Id == zeile.Id)).EditedAt);
    }

    [Fact]
    public async Task Speichern_NurQuelleOderLand_SetztEditedAtNicht()
    {
        var (db, _) = await GesaetAsync();
        var bh = await OrdnungAsync(db);

        var ergebnis = await Verwaltung(db).UpdateRegulationAsync(Guid.NewGuid(), true, bh.Id,
            new UpdateRegulationRequest(bh.Name, bh.Description, "https://example.org/po", "AT", bh.NameEn, bh.DescriptionEn));

        Assert.True(ergebnis.Succeeded);
        Assert.Null(ergebnis.Value!.EditedAt);
    }

    [Fact]
    public async Task Speichern_EineAenderung_SetztEditedAtAnOrdnungUndZeile()
    {
        var (db, _) = await GesaetAsync();
        var bh = await OrdnungAsync(db);
        var versionId = await VersionIdAsync(db, bh.Id);
        var zeile = (await ZeilenAsync(db, versionId)).First();
        var verwaltung = Verwaltung(db);

        var ordnung = await verwaltung.UpdateRegulationAsync(Guid.NewGuid(), true, bh.Id,
            new UpdateRegulationRequest(bh.Name, "Neue Beschreibung", bh.SourceUrl, bh.CountryCode, bh.NameEn, bh.DescriptionEn));
        await verwaltung.UpdateRegulationExerciseAsync(Guid.NewGuid(), true, bh.Id, zeile.ExerciseId,
            new UpdateRegulationExerciseRequest(zeile.IsMandatory, zeile.MaxPoints + 1, zeile.ScoringNotes, zeile.ScoringNotesEn));

        Assert.NotNull(ordnung.Value!.EditedAt);
        Assert.NotNull((await db.RegulationExercises.SingleAsync(re => re.Id == zeile.Id)).EditedAt);
    }

    [Fact]
    public async Task Vereinsordnung_BekommtKeinEditedAt()
    {
        var db = InMemoryDbContext.Create();
        var verein = new Dogity.Domain.Community.Club { Name = "Testverein" };
        db.Clubs.Add(verein);
        var sport = new Sport { Code = "V1", Name = "Vereinssport", ClubId = verein.Id };
        db.Sports.Add(sport);
        var ordnung = new Regulation { SportId = sport.Id, Name = "Eigene PO" };
        db.Regulations.Add(ordnung);
        var trainer = Guid.NewGuid();
        db.ClubTrainers.Add(new Dogity.Domain.Community.ClubTrainer { ClubId = verein.Id, UserId = trainer });
        await db.SaveChangesAsync();

        var ergebnis = await Verwaltung(db).UpdateRegulationAsync(trainer, false, ordnung.Id,
            new UpdateRegulationRequest("Eigene PO 2", null, null, null));

        Assert.True(ergebnis.Succeeded);
        Assert.Null(ergebnis.Value!.EditedAt);
        // Zurücknehmen gibt es nur für den Katalog.
        Assert.False((await Verwaltung(db).RevertRegulationAsync(trainer, false, ordnung.Id)).Succeeded);
    }

    // ----- Zurücknehmen: Rechte ---------------------------------------------

    [Fact]
    public async Task Zuruecknehmen_OhneAdmin_SchlaegtFehl_UndAendertNichts()
    {
        var (db, _) = await GesaetAsync();
        var bh = await OrdnungAsync(db);
        var versionId = await VersionIdAsync(db, bh.Id);
        var zeile = (await ZeilenAsync(db, versionId)).First();
        var verwaltung = Verwaltung(db);
        await verwaltung.UpdateRegulationAsync(Guid.NewGuid(), true, bh.Id,
            new UpdateRegulationRequest(bh.Name, "Neue Beschreibung", bh.SourceUrl, bh.CountryCode, bh.NameEn, bh.DescriptionEn));
        await verwaltung.UpdateRegulationExerciseAsync(Guid.NewGuid(), true, bh.Id, zeile.ExerciseId,
            new UpdateRegulationExerciseRequest(zeile.IsMandatory, zeile.MaxPoints + 1, zeile.ScoringNotes, zeile.ScoringNotesEn));

        Assert.False((await verwaltung.RevertRegulationAsync(Guid.NewGuid(), false, bh.Id)).Succeeded);
        Assert.False((await verwaltung.RevertRegulationExerciseAsync(Guid.NewGuid(), false, bh.Id, zeile.ExerciseId)).Succeeded);

        Assert.NotNull((await db.Regulations.SingleAsync(r => r.Id == bh.Id)).EditedAt);
        Assert.NotNull((await db.RegulationExercises.SingleAsync(re => re.Id == zeile.Id)).EditedAt);
    }

    [Fact]
    public async Task Zuruecknehmen_OhneHandaenderung_SchlaegtFehl()
    {
        var (db, _) = await GesaetAsync();
        var bh = await OrdnungAsync(db);
        var versionId = await VersionIdAsync(db, bh.Id);
        var zeile = (await ZeilenAsync(db, versionId)).First();

        Assert.False((await Verwaltung(db).RevertRegulationAsync(Guid.NewGuid(), true, bh.Id)).Succeeded);
        Assert.False((await Verwaltung(db).RevertRegulationExerciseAsync(Guid.NewGuid(), true, bh.Id, zeile.ExerciseId)).Succeeded);
    }

    [Theory]
    [InlineData(nameof(SportsController.RevertRegulation))]
    [InlineData(nameof(SportsController.RevertRegulationExercise))]
    public void ZuruecknehmenEndpunkte_SindAufAdminsBeschraenkt(string methode)
    {
        var m = typeof(SportsController).GetMethod(methode)!;

        Assert.Null(m.GetCustomAttribute<AllowAnonymousAttribute>());
        var attribut = Assert.Single(m.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(Roles.Admin, attribut.Roles);
    }

    // ----- Admin-Übernahme (Import) -----------------------------------------

    private static RegulationImportService Import(ApplicationDbContext db) => new(db, null!, TimeProvider.System);

    [Fact]
    public async Task Import_ErgaenzteUebung_BleibtBeimNaechstenLaufErhalten()
    {
        var (db, dienste) = await GesaetAsync();
        var bh = await OrdnungAsync(db);
        var versionId = await VersionIdAsync(db, bh.Id);

        var ergebnis = await Import(db).ApplyAsync(new ApplyRegulationImportRequest(bh.Id, [new ApplyExerciseCandidate("Nur per Import", 7)]));
        Assert.True(ergebnis.Succeeded);

        await SportCatalogSeeder.SeedAsync(dienste);

        var zeile = Assert.Single(await ZeilenAsync(db, versionId), re => re.Exercise!.Name == "Nur per Import");
        Assert.NotNull(zeile.EditedAt);
        Assert.Null(zeile.DeletedAt);
        Assert.Equal(7, zeile.MaxPoints);
    }

    [Fact]
    public async Task Import_GeaenderteUndWiederbelebtePunkte_BleibenBeimNaechstenLaufErhalten()
    {
        var (db, dienste) = await GesaetAsync();
        var bh = await OrdnungAsync(db);
        var versionId = await VersionIdAsync(db, bh.Id);
        var zeilen = await ZeilenAsync(db, versionId);
        var geaendert = zeilen[0];
        var entfernt = zeilen[1];
        var neuePunkte = geaendert.MaxPoints + 3;
        var entferntPunkte = entfernt.MaxPoints;
        entfernt.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var ergebnis = await Import(db).ApplyAsync(new ApplyRegulationImportRequest(bh.Id,
            [new ApplyExerciseCandidate(geaendert.Exercise!.Name, neuePunkte), new ApplyExerciseCandidate(entfernt.Exercise!.Name, entferntPunkte)]));
        Assert.True(ergebnis.Succeeded);

        await SportCatalogSeeder.SeedAsync(dienste);

        var danach = await ZeilenAsync(db, versionId);
        Assert.Equal(neuePunkte, danach.Single(re => re.Id == geaendert.Id).MaxPoints);
        Assert.NotNull(danach.Single(re => re.Id == geaendert.Id).EditedAt);
        var belebt = danach.Single(re => re.Id == entfernt.Id);
        Assert.Null(belebt.DeletedAt);
        Assert.NotNull(belebt.EditedAt);
    }

    [Fact]
    public async Task Import_OhneAenderung_LaesstDieZeileUnmarkiert()
    {
        var (db, _) = await GesaetAsync();
        var bh = await OrdnungAsync(db);
        var versionId = await VersionIdAsync(db, bh.Id);
        var zeile = (await ZeilenAsync(db, versionId)).First();

        await Import(db).ApplyAsync(new ApplyRegulationImportRequest(bh.Id, [new ApplyExerciseCandidate(zeile.Exercise!.Name, zeile.MaxPoints)]));

        Assert.Null((await db.RegulationExercises.SingleAsync(re => re.Id == zeile.Id)).EditedAt);
    }

    // ----- Ausbildungsweg über den Schlüssel --------------------------------

    [Fact]
    public void Folgestufen_FuehrenUeberDenSchluessel_AuchNachUmbenennung()
    {
        var dto = new RegulationDto(Guid.NewGuid(), "Begleithund neu", null, null, null, null, null, null, SeedKey: "BH");

        Assert.Equal(["FCI-IGP 1", "IBGH1", "FCI-FPr 1", "FCI-UPr 1"], dto.NextStageNames);
    }
}
