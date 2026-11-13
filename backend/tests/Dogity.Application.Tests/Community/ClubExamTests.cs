using Dogity.Application.Common;
using Dogity.Application.Community;
using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;
using Dogity.Domain.Sports;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Prüfungstage des Vereins (Paket V4, siehe docs/BERECHTIGUNGEN.md): wer sie führen darf, wer ins Team darf,
/// was die Prüfungsleitung von der Prüfungsreife sieht, und was ein Ergebnis am Hund bewirkt - bis zur
/// Korrektur und zum Fall, dass die Familie ihr Ziel inzwischen selbst geändert hat.
/// </summary>
public class ClubExamTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Heute = new(2026, 10, 20);
    private static readonly DateOnly Vergangen = new(2026, 10, 10);
    private static readonly DateOnly Kommend = new(2026, 11, 14);

    private sealed class FesteUhr : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    /// <summary>Eine Ordnung "Prüfung A" mit drei Übungen à 100 Punkte (300) - und eine ohne Punkte.</summary>
    private sealed record Welt(
        ApplicationDbContext Db,
        FakeUserLookupService Nutzer,
        FakeNotificationService Meldungen,
        ClubExamService Dienst,
        GoalService Ziele,
        Club Verein,
        Guid Leitung,
        Guid Mitglied,
        Dog Bello,
        Dog Emma,
        Regulation Pruefung,
        Regulation OhnePunkte,
        List<Exercise> Uebungen);

    private static async Task<Welt> AufbauenAsync()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = new FakeUserLookupService();
        var meldungen = new FakeNotificationService();
        var uhr = new FesteUhr();
        var ziele = new GoalService(db, uhr, meldungen, new ExerciseMasteryService(db));

        var verein = new Club { Name = "Hundesportverein Nord" };
        db.Clubs.Add(verein);
        var leitung = Guid.NewGuid();
        nutzer.Register(leitung, "leitung@example.org", "Lena", "Leitung");
        db.ClubTrainers.Add(new ClubTrainer { ClubId = verein.Id, UserId = leitung, Role = ClubRole.Ausbildungswart });

        var mitglied = Guid.NewGuid();
        nutzer.Register(mitglied, "max@example.org", "Max", "Mustermann");
        db.ClubMemberships.Add(new ClubMembership { ClubId = verein.Id, UserId = mitglied, Status = ClubMembershipStatus.Approved });
        var bello = new Dog { Name = "Bello", Breed = "Beagle", Birthday = new DateOnly(2022, 3, 1) };
        var emma = new Dog { Name = "Emma", Breed = "Aussie" };
        db.Dogs.AddRange(bello, emma);
        db.DogOwners.AddRange(
            new DogOwner { DogId = bello.Id, UserId = mitglied },
            new DogOwner { DogId = emma.Id, UserId = mitglied });

        var sport = new Sport { Code = "X", Name = "Sport X" };
        db.Sports.Add(sport);
        var pruefung = new Regulation { SportId = sport.Id, Name = "Prüfung A", TheoryCatalogCode = "THEORIE-X" };
        var ohnePunkte = new Regulation { SportId = sport.Id, Name = "Prüfung B" };
        db.Regulations.AddRange(pruefung, ohnePunkte);
        var fassung = new RegulationVersion { RegulationId = pruefung.Id, VersionLabel = "2025", ValidFrom = new DateOnly(2025, 1, 1) };
        var fassungB = new RegulationVersion { RegulationId = ohnePunkte.Id, VersionLabel = "2025", ValidFrom = new DateOnly(2025, 1, 1) };
        db.RegulationVersions.AddRange(fassung, fassungB);
        var uebungen = new List<Exercise>();
        for (var i = 0; i < 3; i++)
        {
            var uebung = new Exercise { Name = $"Übung {i}", SportId = sport.Id, Difficulty = ExerciseDifficulty.Beginner };
            uebungen.Add(uebung);
            db.Exercises.Add(uebung);
            db.RegulationExercises.Add(new RegulationExercise { RegulationVersionId = fassung.Id, ExerciseId = uebung.Id, MaxPoints = 100, SortOrder = i });
            db.RegulationExercises.Add(new RegulationExercise { RegulationVersionId = fassungB.Id, ExerciseId = uebung.Id, MaxPoints = 0, SortOrder = i });
        }
        await db.SaveChangesAsync();

        return new Welt(db, nutzer, meldungen, new ClubExamService(db, nutzer, meldungen, ziele, uhr), ziele,
            verein, leitung, mitglied, bello, emma, pruefung, ohnePunkte, uebungen);
    }

    private static SaveClubExamRequest Tag(DateOnly datum, params Guid[] ordnungen) =>
        new(datum, new TimeOnly(10, 0), "Vereinsgelände", "Rita Richter", null, null, ordnungen);

    private static async Task<Guid> TagAnlegenAsync(Welt w, DateOnly? datum = null, params Guid[] ordnungen)
    {
        var tag = await w.Dienst.CreateAsync(w.Leitung, w.Verein.Id, Tag(datum ?? Vergangen, ordnungen));
        Assert.True(tag.Succeeded, string.Join(";", tag.Errors));
        return tag.Value!.Id;
    }

    private static AddClubExamEntryRequest Team(Welt w, Dog hund, Regulation? ordnung = null) =>
        new((ordnung ?? w.Pruefung).Id, w.Mitglied, hund.Id, null, null, null);

    private static async Task<Guid> TeamEintragenAsync(Welt w, Guid tag, Dog hund, Regulation? ordnung = null)
    {
        var ergebnis = await w.Dienst.AddEntryAsync(w.Leitung, w.Verein.Id, tag, Team(w, hund, ordnung));
        Assert.True(ergebnis.Succeeded, string.Join(";", ergebnis.Errors));
        return (await w.Db.ClubExamEntries.AsNoTracking().Where(e => e.ExamId == tag && e.DogId == hund.Id && e.RegulationId == (ordnung ?? w.Pruefung).Id).SingleAsync()).Id;
    }

    private static Task<Result> ErgebnisAsync(Welt w, Guid tag, Guid eintrag, ClubExamResult art, int? punkte = null, string? notiz = null) =>
        w.Dienst.SetResultAsync(w.Leitung, w.Verein.Id, tag, eintrag, new SetClubExamResultRequest(art, punkte, notiz));

    /// <summary>Ein aktives Ziel zur Ordnung - mit leerem Plan, wie jedes Ziel.</summary>
    private static async Task<Goal> AktivesZielAsync(Welt w, Dog hund, Regulation? ordnung = null, DateOnly? zieldatum = null)
    {
        var o = ordnung ?? w.Pruefung;
        var ziel = new Goal { DogId = hund.Id, SportId = o.SportId, RegulationId = o.Id, TargetDate = zieldatum ?? Kommend, Status = GoalStatus.Active };
        ziel.TrainingPlan = new TrainingPlan { GoalId = ziel.Id, Goal = ziel };
        w.Db.Goals.Add(ziel);
        await w.Db.SaveChangesAsync();
        return ziel;
    }

    // ------------------------------------------------------------ Rechte

    public static TheoryData<ClubRole, bool> Zugriff => new()
    {
        { 0, false },
        { ClubRole.Training, false },
        { ClubRole.Vorstand, false },
        { ClubRole.Mitgliederverwaltung, false },
        { ClubRole.Training | ClubRole.Vorstand | ClubRole.Mitgliederverwaltung, false },
        { ClubRole.Ausbildungswart, true },
        { ClubRole.Verwaltung, true },
        { ClubRole.Training | ClubRole.Ausbildungswart, true },
    };

    [Theory]
    [MemberData(nameof(Zugriff))]
    public async Task JedeAktion_NurFuerAusbildungswartUndVerwaltung_SonstNichtGefunden(ClubRole rollen, bool erlaubt)
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Kommend);
        var eintrag = await TeamEintragenAsync(w, tag, w.Bello);

        var person = Guid.NewGuid();
        w.Nutzer.Register(person, "p@example.org", "Pia", "Person");
        if (rollen != 0) w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = w.Verein.Id, UserId = person, Role = rollen });
        await w.Db.SaveChangesAsync();
        var id = w.Verein.Id;

        var ergebnisse = new List<Result>
        {
            await w.Dienst.GetListAsync(person, id),
            await w.Dienst.GetAsync(person, id, tag),
            await w.Dienst.GetCandidatesAsync(person, id),
            await w.Dienst.CreateAsync(person, id, Tag(Kommend)),
            await w.Dienst.UpdateAsync(person, id, tag, Tag(Kommend)),
            await w.Dienst.AddEntryAsync(person, id, tag, Team(w, w.Emma)),
            await w.Dienst.MoveEntryAsync(person, id, tag, eintrag, 1),
            await w.Dienst.SetResultAsync(person, id, tag, eintrag, new SetClubExamResultRequest(ClubExamResult.Failed, null, null)),
            await w.Dienst.RemoveEntryAsync(person, id, tag, eintrag),
            await w.Dienst.CancelAsync(person, id, tag),
            await w.Dienst.DeleteAsync(person, id, tag),
        };

        foreach (var ergebnis in ergebnisse)
        {
            if (erlaubt) Assert.False(ergebnis.IsNotFound);
            else
            {
                Assert.False(ergebnis.Succeeded);
                Assert.True(ergebnis.IsNotFound);
            }
        }
        // Nichts davon hat bei einer Person ohne Recht etwas geändert.
        if (!erlaubt)
        {
            Assert.Equal(1, await w.Db.ClubExams.CountAsync());
            Assert.Equal(1, await w.Db.ClubExamEntries.CountAsync());
        }
    }

    [Fact]
    public async Task FremderVerein_IstNichtGefunden_AuchMitRolleImEigenen()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Kommend);
        var anderer = new Club { Name = "Hundesportverein Süd" };
        w.Db.Clubs.Add(anderer);
        var fremde = Guid.NewGuid();
        w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = anderer.Id, UserId = fremde, Role = ClubRole.Verwaltung });
        await w.Db.SaveChangesAsync();

        // Mit der eigenen Rolle, aber der Prüfung des anderen Vereins: nicht gefunden - auch unter dem eigenen Vereinsnamen.
        Assert.True((await w.Dienst.GetAsync(fremde, anderer.Id, tag)).IsNotFound);
        Assert.True((await w.Dienst.GetAsync(fremde, w.Verein.Id, tag)).IsNotFound);
        Assert.Empty((await w.Dienst.GetListAsync(fremde, anderer.Id)).Value!);
    }

    // ------------------------------------------------------------ Prüfungstag

    [Fact]
    public async Task Anlegen_SpeichertTagUndAngeboteneOrdnungen()
    {
        var w = await AufbauenAsync();

        var ergebnis = await w.Dienst.CreateAsync(w.Leitung, w.Verein.Id, Tag(Kommend, w.Pruefung.Id, w.OhnePunkte.Id, w.Pruefung.Id));

        var dto = ergebnis.Value!;
        Assert.Equal(Kommend, dto.Date);
        Assert.Equal("Vereinsgelände", dto.Location);
        Assert.Equal(ClubExamStatus.Planned, dto.Status);
        Assert.Equal(2, dto.OfferedRegulations.Count);
        Assert.Equal(300, dto.OfferedRegulations.Single(o => o.Id == w.Pruefung.Id).MaxPoints);
        Assert.Null(dto.OfferedRegulations.Single(o => o.Id == w.OhnePunkte.Id).MaxPoints);
    }

    [Fact]
    public async Task Anlegen_PrueftEingaben()
    {
        var w = await AufbauenAsync();
        var id = w.Verein.Id;

        Assert.False((await w.Dienst.CreateAsync(w.Leitung, id, Tag(default))).Succeeded);
        Assert.False((await w.Dienst.CreateAsync(w.Leitung, id, Tag(Kommend) with { Location = "  " })).Succeeded);
        Assert.False((await w.Dienst.CreateAsync(w.Leitung, id, Tag(Kommend, Guid.NewGuid()))).Succeeded);
        Assert.Empty(await w.Db.ClubExams.ToListAsync());
    }

    [Fact]
    public async Task Textlaengen_WerdenImServicePeprueft()
    {
        var w = await AufbauenAsync();
        var id = w.Verein.Id;
        var tag = await TagAnlegenAsync(w, Kommend);

        Assert.False((await w.Dienst.CreateAsync(w.Leitung, id, Tag(Kommend) with { Location = new string('o', Textlaengen.PruefungsOrt + 1) })).Succeeded);
        Assert.False((await w.Dienst.CreateAsync(w.Leitung, id, Tag(Kommend) with { JudgeName = new string('r', Textlaengen.PruefungsRichter + 1) })).Succeeded);
        Assert.False((await w.Dienst.CreateAsync(w.Leitung, id, Tag(Kommend) with { Note = new string('b', Textlaengen.PruefungsBemerkung + 1) })).Succeeded);
        Assert.True((await w.Dienst.CreateAsync(w.Leitung, id, Tag(Kommend) with { Location = new string('o', Textlaengen.PruefungsOrt) })).Succeeded);

        var zuLang = new string('n', Textlaengen.GastName + 1);
        Assert.False((await w.Dienst.AddEntryAsync(w.Leitung, id, tag, new(w.Pruefung.Id, null, null, zuLang, "Rex", null))).Succeeded);
        Assert.False((await w.Dienst.AddEntryAsync(w.Leitung, id, tag, new(w.Pruefung.Id, null, null, "Gerd", zuLang, null))).Succeeded);
        Assert.False((await w.Dienst.AddEntryAsync(w.Leitung, id, tag, new(w.Pruefung.Id, null, null, "Gerd", "Rex", new string('i', Textlaengen.GastInfo + 1)))).Succeeded);
    }

    [Fact]
    public async Task Aendern_ErsetztAngeboteneOrdnungen()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Kommend, w.Pruefung.Id);

        var ergebnis = await w.Dienst.UpdateAsync(w.Leitung, w.Verein.Id, tag, Tag(Kommend.AddDays(1), w.OhnePunkte.Id) with { Location = "Wald" });

        var dto = ergebnis.Value!;
        Assert.Equal(Kommend.AddDays(1), dto.Date);
        Assert.Equal("Wald", dto.Location);
        Assert.Equal([w.OhnePunkte.Id], dto.OfferedRegulations.Select(o => o.Id));
        // Die entfernte Ordnung bleibt als weich gelöschte Zeile, die wieder angebotene bekommt eine neue.
        Assert.Equal(2, await w.Db.ClubExamRegulations.IgnoreQueryFilters().CountAsync());
        var nochmal = await w.Dienst.UpdateAsync(w.Leitung, w.Verein.Id, tag, Tag(Kommend, w.Pruefung.Id, w.OhnePunkte.Id));
        Assert.Equal(2, nochmal.Value!.OfferedRegulations.Count);
    }

    [Fact]
    public async Task Aendern_DatumGesperrt_SobaldErgebnisseDa()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Vergangen);
        var eintrag = await TeamEintragenAsync(w, tag, w.Bello);
        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Failed);

        Assert.False((await w.Dienst.UpdateAsync(w.Leitung, w.Verein.Id, tag, Tag(Vergangen.AddDays(1)))).Succeeded);
        Assert.True((await w.Dienst.UpdateAsync(w.Leitung, w.Verein.Id, tag, Tag(Vergangen) with { Location = "Anderswo" })).Succeeded);
    }

    // ------------------------------------------------------------ Teams

    [Fact]
    public async Task Eintragen_Mitglied_NurMitEigenemHundAusDemVerein()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Kommend);
        var id = w.Verein.Id;

        var fremder = Guid.NewGuid();
        w.Nutzer.Register(fremder, "fremd@example.org", "Franz", "Fremd");
        var fremderHund = new Dog { Name = "Rex" };
        w.Db.Dogs.Add(fremderHund);
        w.Db.DogOwners.Add(new DogOwner { DogId = fremderHund.Id, UserId = fremder });
        var archiviert = new Dog { Name = "Opa", ArchivedAt = Jetzt };
        w.Db.Dogs.Add(archiviert);
        w.Db.DogOwners.Add(new DogOwner { DogId = archiviert.Id, UserId = w.Mitglied });
        await w.Db.SaveChangesAsync();

        // Kein Mitglied dieses Vereins.
        Assert.False((await w.Dienst.AddEntryAsync(w.Leitung, id, tag, new(w.Pruefung.Id, fremder, fremderHund.Id, null, null, null))).Succeeded);
        // Der Hund eines anderen.
        Assert.False((await w.Dienst.AddEntryAsync(w.Leitung, id, tag, new(w.Pruefung.Id, w.Mitglied, fremderHund.Id, null, null, null))).Succeeded);
        // Ein archivierter Hund.
        Assert.False((await w.Dienst.AddEntryAsync(w.Leitung, id, tag, new(w.Pruefung.Id, w.Mitglied, archiviert.Id, null, null, null))).Succeeded);
        // Nur das Mitglied oder nur der Hund.
        Assert.False((await w.Dienst.AddEntryAsync(w.Leitung, id, tag, new(w.Pruefung.Id, w.Mitglied, null, null, null, null))).Succeeded);
        Assert.False((await w.Dienst.AddEntryAsync(w.Leitung, id, tag, new(w.Pruefung.Id, null, w.Bello.Id, null, null, null))).Succeeded);
        // Mitglied und Gast zugleich.
        Assert.False((await w.Dienst.AddEntryAsync(w.Leitung, id, tag, new(w.Pruefung.Id, w.Mitglied, w.Bello.Id, "Gerd", null, null))).Succeeded);
        Assert.Empty(await w.Db.ClubExamEntries.ToListAsync());

        Assert.True((await w.Dienst.AddEntryAsync(w.Leitung, id, tag, Team(w, w.Bello))).Succeeded);
    }

    [Fact]
    public async Task Eintragen_MitgliedMitNurRolle_ZaehltAlsZugehoerig()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Kommend);
        // Die Leitung selbst hat nur eine Rolle, keine Mitgliedschaft - und einen Hund.
        var hund = new Dog { Name = "Lenas Hund" };
        w.Db.Dogs.Add(hund);
        w.Db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = w.Leitung });
        await w.Db.SaveChangesAsync();

        var ergebnis = await w.Dienst.AddEntryAsync(w.Leitung, w.Verein.Id, tag, new(w.Pruefung.Id, w.Leitung, hund.Id, null, null, null));

        Assert.True(ergebnis.Succeeded);
    }

    [Fact]
    public async Task Eintragen_Doppelmeldung_ImselbenTagUndDerselbenOrdnungWirdVerhindert()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Kommend);
        await TeamEintragenAsync(w, tag, w.Bello);

        Assert.False((await w.Dienst.AddEntryAsync(w.Leitung, w.Verein.Id, tag, Team(w, w.Bello))).Succeeded);
        // Eine andere Ordnung oder ein anderer Tag ist erlaubt.
        Assert.True((await w.Dienst.AddEntryAsync(w.Leitung, w.Verein.Id, tag, Team(w, w.Bello, w.OhnePunkte))).Succeeded);
        var zweiter = await TagAnlegenAsync(w, Kommend.AddDays(7));
        Assert.True((await w.Dienst.AddEntryAsync(w.Leitung, w.Verein.Id, zweiter, Team(w, w.Bello))).Succeeded);
    }

    [Fact]
    public async Task Eintragen_Gast_BrauchtNamen_UndDoppeltGiltOhneGrossschreibung()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Kommend);
        var id = w.Verein.Id;

        Assert.False((await w.Dienst.AddEntryAsync(w.Leitung, id, tag, new(w.Pruefung.Id, null, null, "Gerd", "  ", null))).Succeeded);
        Assert.False((await w.Dienst.AddEntryAsync(w.Leitung, id, tag, new(w.Pruefung.Id, null, null, null, "Rex", null))).Succeeded);

        Assert.True((await w.Dienst.AddEntryAsync(w.Leitung, id, tag, new(w.Pruefung.Id, null, null, "  Gerd Gast ", "Rex", " Hundefreunde, Boxer "))).Succeeded);
        Assert.False((await w.Dienst.AddEntryAsync(w.Leitung, id, tag, new(w.Pruefung.Id, null, null, "gerd gast", "REX", null))).Succeeded);

        var dto = (await w.Dienst.GetAsync(w.Leitung, id, tag)).Value!.Entries.Single();
        Assert.Equal(ClubExamTeamKind.Guest, dto.Kind);
        Assert.Equal("Gerd Gast", dto.HandlerName);
        Assert.Equal("Hundefreunde, Boxer", dto.Info);
        Assert.Null(dto.Reife);
    }

    [Fact]
    public async Task Eintragen_NurAngeboteneOrdnung_UndNichtBeiAbsage()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Kommend, w.Pruefung.Id);

        Assert.False((await w.Dienst.AddEntryAsync(w.Leitung, w.Verein.Id, tag, Team(w, w.Bello, w.OhnePunkte))).Succeeded);
        Assert.True((await w.Dienst.AddEntryAsync(w.Leitung, w.Verein.Id, tag, Team(w, w.Bello))).Succeeded);

        await w.Dienst.CancelAsync(w.Leitung, w.Verein.Id, tag);
        Assert.False((await w.Dienst.AddEntryAsync(w.Leitung, w.Verein.Id, tag, Team(w, w.Emma))).Succeeded);
    }

    [Fact]
    public async Task Kandidaten_NurMitgliederMitAktivenHunden()
    {
        var w = await AufbauenAsync();
        var ohneHund = Guid.NewGuid();
        w.Nutzer.Register(ohneHund, "o@example.org", "Olga", "Ohnehund");
        w.Db.ClubMemberships.Add(new ClubMembership { ClubId = w.Verein.Id, UserId = ohneHund, Status = ClubMembershipStatus.Approved });
        var offen = Guid.NewGuid();
        w.Nutzer.Register(offen, "x@example.org", "Xaver", "Anfrage");
        w.Db.ClubMemberships.Add(new ClubMembership { ClubId = w.Verein.Id, UserId = offen, Status = ClubMembershipStatus.Pending });
        var hundDerAnfrage = new Dog { Name = "Pinscher" };
        w.Db.Dogs.Add(hundDerAnfrage);
        w.Db.DogOwners.Add(new DogOwner { DogId = hundDerAnfrage.Id, UserId = offen });
        var archiviert = new Dog { Name = "Opa", ArchivedAt = Jetzt };
        w.Db.Dogs.Add(archiviert);
        w.Db.DogOwners.Add(new DogOwner { DogId = archiviert.Id, UserId = w.Mitglied });
        await w.Db.SaveChangesAsync();

        var kandidaten = (await w.Dienst.GetCandidatesAsync(w.Leitung, w.Verein.Id)).Value!;

        var max = Assert.Single(kandidaten);
        Assert.Equal("Mustermann", max.LastName);
        Assert.Equal(["Bello", "Emma"], max.Dogs.Select(d => d.Name));
    }

    [Fact]
    public async Task Reihenfolge_HochUndRunter_NurInnerhalbDerOrdnung()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Kommend);
        var bello = await TeamEintragenAsync(w, tag, w.Bello);
        var emma = await TeamEintragenAsync(w, tag, w.Emma);
        var andere = await TeamEintragenAsync(w, tag, w.Bello, w.OhnePunkte);
        string[] Namen(ClubExamDto d, Regulation o) => d.Entries.Where(e => e.RegulationId == o.Id).Select(e => e.DogName!).ToArray();

        await w.Dienst.MoveEntryAsync(w.Leitung, w.Verein.Id, tag, emma, -1);
        var dto = (await w.Dienst.GetAsync(w.Leitung, w.Verein.Id, tag)).Value!;
        Assert.Equal(["Emma", "Bello"], Namen(dto, w.Pruefung));
        Assert.Equal([1, 2], dto.Entries.Where(e => e.RegulationId == w.Pruefung.Id).Select(e => e.StartNumber));
        Assert.Equal(["Bello"], Namen(dto, w.OhnePunkte));

        await w.Dienst.MoveEntryAsync(w.Leitung, w.Verein.Id, tag, emma, 1);
        Assert.Equal(["Bello", "Emma"], Namen((await w.Dienst.GetAsync(w.Leitung, w.Verein.Id, tag)).Value!, w.Pruefung));

        // Am Rand passiert nichts, ein ungültiger Schritt wird abgewiesen.
        Assert.True((await w.Dienst.MoveEntryAsync(w.Leitung, w.Verein.Id, tag, bello, -1)).Succeeded);
        Assert.False((await w.Dienst.MoveEntryAsync(w.Leitung, w.Verein.Id, tag, andere, 2)).Succeeded);
    }

    // ------------------------------------------------------------ Prüfungsreife

    [Fact]
    public async Task Reife_ZeigtAmpelUndFehlendes_ObwohlDieTheorieNichtVorliegt()
    {
        var w = await AufbauenAsync();
        await AktivesZielAsync(w, w.Bello);
        // Alle drei Übungen sicher: Mit Theorieforderung (TheoryCatalogCode) wäre die Ampel ohne Übungsprüfung nie grün.
        foreach (var uebung in w.Uebungen)
            w.Db.ExerciseMasteries.Add(new ExerciseMastery
            {
                DogId = w.Bello.Id, ExerciseId = uebung.Id, SessionCount = 3, Box = 4, RecentAvgRating = 4.6, LastTrainedAt = Jetzt.AddDays(-3),
            });
        await w.Db.SaveChangesAsync();
        var tag = await TagAnlegenAsync(w, Kommend);
        await TeamEintragenAsync(w, tag, w.Bello);

        var reife = (await w.Dienst.GetAsync(w.Leitung, w.Verein.Id, tag)).Value!.Entries.Single().Reife;

        Assert.NotNull(reife);
        Assert.Equal(Reifeampel.Gruen, reife.Ampel);
        Assert.Equal(3, reife.Sicher);
        Assert.Empty(reife.Fehlend);
    }

    [Fact]
    public async Task Reife_ListetNurOffeneUndWackeligeUebungen_OhneTagebuchdaten()
    {
        var w = await AufbauenAsync();
        await AktivesZielAsync(w, w.Bello);
        w.Db.ExerciseMasteries.Add(new ExerciseMastery
        {
            DogId = w.Bello.Id, ExerciseId = w.Uebungen[0].Id, SessionCount = 3, Box = 4, RecentAvgRating = 4.6, LastTrainedAt = Jetzt.AddDays(-3),
        });
        w.Db.ExerciseMasteries.Add(new ExerciseMastery
        {
            DogId = w.Bello.Id, ExerciseId = w.Uebungen[1].Id, SessionCount = 2, Box = 2, RecentAvgRating = 3.0, LastTrainedAt = Jetzt.AddDays(-5),
        });
        await w.Db.SaveChangesAsync();
        var tag = await TagAnlegenAsync(w, Kommend);
        await TeamEintragenAsync(w, tag, w.Bello);

        var reife = (await w.Dienst.GetAsync(w.Leitung, w.Verein.Id, tag)).Value!.Entries.Single().Reife!;

        Assert.Equal((1, 1, 1), (reife.Sicher, reife.Wackelig, reife.Offen));
        Assert.Equal(Reifeampel.Rot, reife.Ampel);
        Assert.Equal(2, reife.Fehlend.Count);
        Assert.DoesNotContain(reife.Fehlend, u => u.ExerciseId == w.Uebungen[0].Id);
        Assert.Contains(reife.Fehlend, u => u.ExerciseId == w.Uebungen[1].Id && u.Zustand == ReifeZustand.Wackelig);
        Assert.Contains(reife.Fehlend, u => u.ExerciseId == w.Uebungen[2].Id && u.Zustand == ReifeZustand.Offen);
        // Die Antwort kennt weder Bewertungen noch Tage noch Pausen noch Theorie - es gibt dafür gar keine Felder.
        var felder = typeof(ClubExamReifeDto).GetProperties().Select(p => p.Name).Concat(typeof(ClubExamReifeUebungDto).GetProperties().Select(p => p.Name));
        Assert.DoesNotContain(felder, f => f is "Theorie" or "Bewertung" or "TageSeit" or "PausenTage" or "Pausiert");
    }

    [Fact]
    public async Task Reife_OhneLaufendesZielInDerselbenOrdnung_KeineAmpel()
    {
        var w = await AufbauenAsync();
        // Ein Ziel zu einer anderen Ordnung zählt nicht.
        await AktivesZielAsync(w, w.Bello, w.OhnePunkte);
        var tag = await TagAnlegenAsync(w, Kommend);
        await TeamEintragenAsync(w, tag, w.Bello);

        Assert.Null((await w.Dienst.GetAsync(w.Leitung, w.Verein.Id, tag)).Value!.Entries.Single().Reife);
    }

    // ------------------------------------------------------------ Ergebnis -> Ziel

    [Fact]
    public async Task Bestanden_MitLaufendemZiel_MachtDasZielErreicht()
    {
        var w = await AufbauenAsync();
        var ziel = await AktivesZielAsync(w, w.Bello);
        var tag = await TagAnlegenAsync(w, Vergangen);
        var eintrag = await TeamEintragenAsync(w, tag, w.Bello);

        var ergebnis = await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Passed, 272, "  Sicher geführt  ");

        Assert.True(ergebnis.Succeeded, string.Join(";", ergebnis.Errors));
        var gespeichert = await w.Db.Goals.AsNoTracking().SingleAsync();
        Assert.Equal(ziel.Id, gespeichert.Id);
        Assert.Equal(GoalStatus.Achieved, gespeichert.Status);
        Assert.Equal(Vergangen, gespeichert.ExamDate);
        Assert.Equal(272, gespeichert.ExamScore);
        Assert.Equal("Sicher geführt", gespeichert.ExamNote);
        var e = await w.Db.ClubExamEntries.AsNoTracking().SingleAsync();
        Assert.Equal((ClubExamResult.Passed, ziel.Id, false), (e.Result, e.GoalId, e.GoalCreated));
        Assert.Equal(w.Leitung, e.ResultByUserId);
        Assert.NotNull(e.ResultEnteredAt);
    }

    [Fact]
    public async Task Bestanden_OhneLaufendesZiel_LegtErreichtesZielAn()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Vergangen);
        var eintrag = await TeamEintragenAsync(w, tag, w.Bello);

        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Passed, null, null);

        var ziel = await w.Db.Goals.Include(g => g.TrainingPlan).AsNoTracking().SingleAsync();
        Assert.Equal(w.Bello.Id, ziel.DogId);
        Assert.Equal((w.Pruefung.SportId, w.Pruefung.Id), (ziel.SportId, ziel.RegulationId));
        Assert.Equal(GoalStatus.Achieved, ziel.Status);
        Assert.Equal((Vergangen, Vergangen), (ziel.TargetDate, ziel.ExamDate));
        Assert.Null(ziel.ExamScore);
        Assert.Null(ziel.ExamNote);
        Assert.Empty(await w.Db.TrainingPlanItems.ToListAsync());
        var e = await w.Db.ClubExamEntries.AsNoTracking().SingleAsync();
        Assert.Equal((ziel.Id, true), (e.GoalId, e.GoalCreated));

        // Die Familie sieht die Leistung wie gewohnt.
        var leistungen = (await w.Ziele.GetByDogAsync(w.Mitglied, w.Bello.Id)).Value!;
        Assert.Equal(GoalStatus.Achieved, Assert.Single(leistungen).Status);
    }

    [Theory]
    [InlineData(ClubExamResult.Failed)]
    [InlineData(ClubExamResult.NotStarted)]
    public async Task NichtBestanden_LaesstDasZielUnberuehrt(ClubExamResult art)
    {
        var w = await AufbauenAsync();
        await AktivesZielAsync(w, w.Bello);
        var tag = await TagAnlegenAsync(w, Vergangen);
        var eintrag = await TeamEintragenAsync(w, tag, w.Bello);

        await ErgebnisAsync(w, tag, eintrag, art, null, "knapp");

        var ziel = await w.Db.Goals.AsNoTracking().SingleAsync();
        Assert.Equal(GoalStatus.Active, ziel.Status);
        Assert.Null(ziel.ExamDate);
        var e = await w.Db.ClubExamEntries.AsNoTracking().SingleAsync();
        Assert.Equal((art, "knapp", (Guid?)null), (e.Result, e.ResultNote, e.GoalId));
    }

    [Fact]
    public async Task Aenderung_BestandenZuNichtBestanden_LoeschtEinDafuerAngelegtesZiel()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Vergangen);
        var eintrag = await TeamEintragenAsync(w, tag, w.Bello);
        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Passed);

        var ergebnis = await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Failed, null, "doch nicht");

        Assert.True(ergebnis.Succeeded);
        Assert.Empty(await w.Db.Goals.ToListAsync());
        var e = await w.Db.ClubExamEntries.AsNoTracking().SingleAsync();
        Assert.Equal((ClubExamResult.Failed, (Guid?)null, false), (e.Result, e.GoalId, e.GoalCreated));
    }

    [Fact]
    public async Task Aenderung_BestandenZuNichtBestanden_MachtEinVorherAktivesZielWiederAktiv()
    {
        var w = await AufbauenAsync();
        var ziel = await AktivesZielAsync(w, w.Bello);
        var tag = await TagAnlegenAsync(w, Vergangen);
        var eintrag = await TeamEintragenAsync(w, tag, w.Bello);
        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Passed, 250, "gut");

        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Failed);

        var gespeichert = await w.Db.Goals.AsNoTracking().SingleAsync();
        Assert.Equal(ziel.Id, gespeichert.Id);
        Assert.Equal(GoalStatus.Active, gespeichert.Status);
        Assert.Null(gespeichert.ExamDate);
        Assert.Null(gespeichert.ExamScore);
        Assert.Null(gespeichert.ExamNote);
    }

    [Fact]
    public async Task Aenderung_BestandenMitAndererPunktzahl_ZiehtDasZielNach()
    {
        var w = await AufbauenAsync();
        await AktivesZielAsync(w, w.Bello);
        var tag = await TagAnlegenAsync(w, Vergangen);
        var eintrag = await TeamEintragenAsync(w, tag, w.Bello);
        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Passed, 250, "gut");

        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Passed, 260, null);

        var ziel = await w.Db.Goals.AsNoTracking().SingleAsync();
        Assert.Equal((GoalStatus.Achieved, 260, (string?)null), (ziel.Status, ziel.ExamScore, ziel.ExamNote));
    }

    [Fact]
    public async Task Aenderung_FamilieHatDasZielGeaendert_GewinntDieFamilie()
    {
        var w = await AufbauenAsync();
        await AktivesZielAsync(w, w.Bello);
        var tag = await TagAnlegenAsync(w, Vergangen);
        var eintrag = await TeamEintragenAsync(w, tag, w.Bello);
        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Passed, 250, "gut");
        var zielId = (await w.Db.Goals.AsNoTracking().SingleAsync()).Id;
        // Die Familie korrigiert die Punkte selbst.
        var familie = await w.Ziele.UpdateExamResultAsync(w.Mitglied, zielId, new UpdateExamResultRequest(Vergangen, 281, "Familie weiß es besser"));
        Assert.True(familie.Succeeded);

        // Weder eine Punktänderung noch ein Zurücknehmen der Prüfungsleitung überschreibt das.
        Assert.True((await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Passed, 260, "neu")).Succeeded);
        var ziel = await w.Db.Goals.AsNoTracking().SingleAsync();
        Assert.Equal((GoalStatus.Achieved, 281, "Familie weiß es besser"), (ziel.Status, ziel.ExamScore, ziel.ExamNote));
        Assert.Equal(260, (await w.Db.ClubExamEntries.AsNoTracking().SingleAsync()).Score);

        Assert.True((await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Failed)).Succeeded);
        ziel = await w.Db.Goals.AsNoTracking().SingleAsync();
        Assert.Equal((GoalStatus.Achieved, 281), (ziel.Status, ziel.ExamScore));
        var e = await w.Db.ClubExamEntries.AsNoTracking().SingleAsync();
        Assert.Equal(ClubExamResult.Failed, e.Result);
        Assert.Null(e.GoalId);
    }

    [Fact]
    public async Task Aenderung_FamilieHatDasZielGeloescht_GewinntDieFamilie()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Vergangen);
        var eintrag = await TeamEintragenAsync(w, tag, w.Bello);
        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Passed);
        var zielId = (await w.Db.Goals.AsNoTracking().SingleAsync()).Id;
        Assert.True((await w.Ziele.DeleteAsync(w.Mitglied, zielId)).Succeeded);

        // Ein neues Ziel entsteht nicht von selbst: Die Leistung war gelöscht.
        Assert.True((await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Passed, 100, null)).Succeeded);
        Assert.Empty(await w.Db.Goals.ToListAsync());
        Assert.True((await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Failed)).Succeeded);
        Assert.Empty(await w.Db.Goals.ToListAsync());
        Assert.Equal(ClubExamResult.Failed, (await w.Db.ClubExamEntries.AsNoTracking().SingleAsync()).Result);
    }

    [Fact]
    public async Task Ergebnis_ZuruecksetzenAufOffen_NimmtDieLeistungWiederMit()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Vergangen);
        var eintrag = await TeamEintragenAsync(w, tag, w.Bello);
        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Passed, 100, "x");

        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Open);

        Assert.Empty(await w.Db.Goals.ToListAsync());
        var e = await w.Db.ClubExamEntries.AsNoTracking().SingleAsync();
        Assert.Equal((ClubExamResult.Open, (int?)null, (string?)null), (e.Result, e.Score, e.ResultNote));
        Assert.Null(e.ResultEnteredAt);
        // Wieder bestanden: ein neues Ziel.
        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Passed);
        Assert.Single(await w.Db.Goals.ToListAsync());
    }

    [Fact]
    public async Task Ergebnis_GastTeam_BrauchtKeinZiel()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Vergangen);
        await w.Dienst.AddEntryAsync(w.Leitung, w.Verein.Id, tag, new(w.Pruefung.Id, null, null, "Gerd", "Rex", null));
        var eintrag = (await w.Db.ClubExamEntries.SingleAsync()).Id;

        Assert.True((await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Passed, 200, null)).Succeeded);

        Assert.Empty(await w.Db.Goals.ToListAsync());
        Assert.Empty(w.Meldungen.Created);
    }

    [Fact]
    public async Task Ergebnis_PrueftPunkteDatumUndNotiz()
    {
        var w = await AufbauenAsync();
        var vergangen = await TagAnlegenAsync(w, Vergangen);
        var zukunft = await TagAnlegenAsync(w, Kommend);
        var heute = await TeamEintragenAsync(w, vergangen, w.Bello);
        var spaeter = await TeamEintragenAsync(w, zukunft, w.Bello);
        var ohnePunkte = await TeamEintragenAsync(w, vergangen, w.Bello, w.OhnePunkte);

        Assert.False((await ErgebnisAsync(w, vergangen, heute, ClubExamResult.Passed, 301)).Succeeded);
        Assert.False((await ErgebnisAsync(w, vergangen, heute, ClubExamResult.Failed, -1)).Succeeded);
        Assert.False((await ErgebnisAsync(w, vergangen, heute, ClubExamResult.Passed, 100, new string('x', Textlaengen.PruefungsNotiz + 1))).Succeeded);
        Assert.False((await ErgebnisAsync(w, vergangen, heute, ClubExamResult.NotStarted, 10)).Succeeded);
        Assert.False((await ErgebnisAsync(w, vergangen, heute, ClubExamResult.Open, 10)).Succeeded);
        Assert.False((await ErgebnisAsync(w, vergangen, heute, (ClubExamResult)99)).Succeeded);
        // Vor dem Prüfungstag gibt es kein Ergebnis.
        Assert.False((await ErgebnisAsync(w, zukunft, spaeter, ClubExamResult.Passed)).Succeeded);
        // Ohne Punktwertung der Ordnung gibt es keine Obergrenze.
        Assert.True((await ErgebnisAsync(w, vergangen, ohnePunkte, ClubExamResult.Passed, 5)).Succeeded);
        Assert.True((await ErgebnisAsync(w, vergangen, heute, ClubExamResult.Passed, 300)).Succeeded);
        Assert.Equal(ClubExamResult.Open, (await w.Db.ClubExamEntries.AsNoTracking().SingleAsync(e => e.Id == spaeter)).Result);
    }

    [Fact]
    public async Task Status_WirdFertigMitDemLetztenErgebnis_UndWiederGeplantMitZuruecksetzen()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Vergangen);
        var bello = await TeamEintragenAsync(w, tag, w.Bello);
        var emma = await TeamEintragenAsync(w, tag, w.Emma);
        async Task<ClubExamStatus> StatusAsync() => (await w.Dienst.GetAsync(w.Leitung, w.Verein.Id, tag)).Value!.Status;

        await ErgebnisAsync(w, tag, bello, ClubExamResult.Failed);
        Assert.Equal(ClubExamStatus.Planned, await StatusAsync());
        await ErgebnisAsync(w, tag, emma, ClubExamResult.NotStarted);
        Assert.Equal(ClubExamStatus.Done, await StatusAsync());
        await ErgebnisAsync(w, tag, emma, ClubExamResult.Open);
        Assert.Equal(ClubExamStatus.Planned, await StatusAsync());
        await ErgebnisAsync(w, tag, emma, ClubExamResult.Failed);
        Assert.Equal(ClubExamStatus.Done, await StatusAsync());
        // Ein neues Team macht den fertigen Tag wieder offen.
        await w.Dienst.AddEntryAsync(w.Leitung, w.Verein.Id, tag, new(w.Pruefung.Id, null, null, "Gerd", "Rex", null));
        Assert.Equal(ClubExamStatus.Planned, await StatusAsync());
    }

    // ------------------------------------------------------------ Absagen, Löschen, Austragen

    [Fact]
    public async Task Absagen_MeldetDenMitgliedTeamsOhneErgebnis()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Kommend);
        await TeamEintragenAsync(w, tag, w.Bello);
        await w.Dienst.AddEntryAsync(w.Leitung, w.Verein.Id, tag, new(w.Pruefung.Id, null, null, "Gerd", "Rex", null));
        w.Meldungen.Created.Clear();

        var ergebnis = await w.Dienst.CancelAsync(w.Leitung, w.Verein.Id, tag);

        Assert.True(ergebnis.Succeeded);
        var meldung = Assert.Single(w.Meldungen.Created);
        Assert.Equal(w.Mitglied, meldung.UserId);
        Assert.Equal("Die Prüfung bei Hundesportverein Nord am 14.11.2026 wurde abgesagt. Die Meldung von Bello für die Prüfung A entfällt.", meldung.Message);
        Assert.Equal($"/dogs/{w.Bello.Id}", meldung.LinkPath);
        Assert.Equal(ClubExamStatus.Cancelled, (await w.Dienst.GetAsync(w.Leitung, w.Verein.Id, tag)).Value!.Status);
        Assert.False((await w.Dienst.CancelAsync(w.Leitung, w.Verein.Id, tag)).Succeeded);
    }

    [Fact]
    public async Task AbsagenUndLoeschen_NichtMehrMitErgebnissen()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Vergangen);
        var eintrag = await TeamEintragenAsync(w, tag, w.Bello);
        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Failed);

        Assert.False((await w.Dienst.CancelAsync(w.Leitung, w.Verein.Id, tag)).Succeeded);
        Assert.False((await w.Dienst.DeleteAsync(w.Leitung, w.Verein.Id, tag)).Succeeded);
        Assert.Equal(1, await w.Db.ClubExams.CountAsync());

        // Erst das Ergebnis zurücknehmen.
        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Open);
        Assert.True((await w.Dienst.DeleteAsync(w.Leitung, w.Verein.Id, tag)).Succeeded);
    }

    [Fact]
    public async Task Loeschen_NimmtTeamsMitUndLoeschtDieGastdaten()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Kommend);
        await TeamEintragenAsync(w, tag, w.Bello);
        await w.Dienst.AddEntryAsync(w.Leitung, w.Verein.Id, tag, new(w.Pruefung.Id, null, null, "Gerd Gast", "Rex", "Verein Süd"));
        w.Meldungen.Created.Clear();

        var ergebnis = await w.Dienst.DeleteAsync(w.Leitung, w.Verein.Id, tag);

        Assert.True(ergebnis.Succeeded);
        Assert.Empty(await w.Db.ClubExams.ToListAsync());
        Assert.Empty(await w.Db.ClubExamEntries.ToListAsync());
        // Auch hinter dem Filter stehen keine Namen Dritter mehr.
        var zeilen = await w.Db.ClubExamEntries.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(2, zeilen.Count);
        Assert.All(zeilen, z => Assert.Null(z.GuestHandlerName ?? z.GuestDogName ?? z.GuestInfo));
        Assert.Equal(w.Mitglied, Assert.Single(w.Meldungen.Created).UserId);
        Assert.Contains("abgesagt", w.Meldungen.Created[0].Message);
    }

    [Fact]
    public async Task Austragen_LoeschtGastdatenUndMeldetDemMitglied()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Kommend);
        var bello = await TeamEintragenAsync(w, tag, w.Bello);
        await w.Dienst.AddEntryAsync(w.Leitung, w.Verein.Id, tag, new(w.Pruefung.Id, null, null, "Gerd Gast", "Rex", null));
        var gast = (await w.Db.ClubExamEntries.SingleAsync(e => e.GuestHandlerName != null)).Id;
        w.Meldungen.Created.Clear();

        Assert.True((await w.Dienst.RemoveEntryAsync(w.Leitung, w.Verein.Id, tag, gast)).Succeeded);
        Assert.Empty(w.Meldungen.Created);
        Assert.Null((await w.Db.ClubExamEntries.IgnoreQueryFilters().SingleAsync(e => e.Id == gast)).GuestHandlerName);

        Assert.True((await w.Dienst.RemoveEntryAsync(w.Leitung, w.Verein.Id, tag, bello)).Succeeded);
        var meldung = Assert.Single(w.Meldungen.Created);
        Assert.Equal("Hundesportverein Nord hat die Meldung von Bello für die Prüfung A am 14.11.2026 zurückgenommen.", meldung.Message);
        Assert.Empty((await w.Dienst.GetAsync(w.Leitung, w.Verein.Id, tag)).Value!.Entries);
        Assert.True((await w.Dienst.RemoveEntryAsync(w.Leitung, w.Verein.Id, tag, bello)).IsNotFound);
    }

    [Fact]
    public async Task Austragen_NachErgebnisOderAbsage_SchicktKeineRuecknahme()
    {
        var w = await AufbauenAsync();
        var vorbei = await TagAnlegenAsync(w, Vergangen);
        var mitErgebnis = await TeamEintragenAsync(w, vorbei, w.Bello);
        await ErgebnisAsync(w, vorbei, mitErgebnis, ClubExamResult.Failed);
        var abgesagt = await TagAnlegenAsync(w, Kommend);
        var ohneErgebnis = await TeamEintragenAsync(w, abgesagt, w.Emma);
        await w.Dienst.CancelAsync(w.Leitung, w.Verein.Id, abgesagt);
        w.Meldungen.Created.Clear();

        Assert.True((await w.Dienst.RemoveEntryAsync(w.Leitung, w.Verein.Id, vorbei, mitErgebnis)).Succeeded);
        Assert.True((await w.Dienst.RemoveEntryAsync(w.Leitung, w.Verein.Id, abgesagt, ohneErgebnis)).Succeeded);

        Assert.Empty(w.Meldungen.Created);
    }

    [Fact]
    public async Task Austragen_EinesBestandenen_NimmtDieLeistungMit()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Vergangen);
        var eintrag = await TeamEintragenAsync(w, tag, w.Bello);
        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Passed);

        await w.Dienst.RemoveEntryAsync(w.Leitung, w.Verein.Id, tag, eintrag);

        Assert.Empty(await w.Db.Goals.ToListAsync());
    }

    // ------------------------------------------------------------ Benachrichtigungen

    [Fact]
    public async Task Eintragen_BenachrichtigtAlleBesitzerDesHundes()
    {
        var w = await AufbauenAsync();
        var mitbesitz = Guid.NewGuid();
        w.Nutzer.Register(mitbesitz, "mit@example.org", "Mia", "Mitbesitz");
        w.Db.DogOwners.Add(new DogOwner { DogId = w.Bello.Id, UserId = mitbesitz });
        // Eine nur eingeladene Person zählt (noch) nicht.
        w.Db.DogOwners.Add(new DogOwner { DogId = w.Bello.Id, UserId = Guid.NewGuid(), Status = DogOwnerStatus.Invited });
        await w.Db.SaveChangesAsync();
        var tag = await TagAnlegenAsync(w, Kommend);

        await TeamEintragenAsync(w, tag, w.Bello);

        Assert.Equal(2, w.Meldungen.Created.Count);
        Assert.All(w.Meldungen.Created, m =>
        {
            Assert.Equal("Hundesportverein Nord hat Bello für die Prüfung A am 14.11.2026 gemeldet. Die Prüfungsleitung sieht dafür die Prüfungsreife.", m.Message);
            Assert.Equal($"/dogs/{w.Bello.Id}", m.LinkPath);
        });
        Assert.Equal(new[] { w.Mitglied, mitbesitz }.Order(), w.Meldungen.Created.Select(m => m.UserId).Order());
    }

    [Theory]
    [InlineData(ClubExamResult.Passed, 272, "Ergebnis der Prüfung A vom 10.10.2026 für Bello eingetragen: bestanden, Punkte: 272.")]
    [InlineData(ClubExamResult.Passed, null, "Ergebnis der Prüfung A vom 10.10.2026 für Bello eingetragen: bestanden.")]
    [InlineData(ClubExamResult.Failed, 120, "Ergebnis der Prüfung A vom 10.10.2026 für Bello eingetragen: nicht bestanden, Punkte: 120.")]
    [InlineData(ClubExamResult.Failed, null, "Ergebnis der Prüfung A vom 10.10.2026 für Bello eingetragen: nicht bestanden.")]
    [InlineData(ClubExamResult.NotStarted, null, "Ergebnis der Prüfung A vom 10.10.2026 für Bello eingetragen: nicht angetreten.")]
    public async Task Ergebnis_BenachrichtigtDieFamilie(ClubExamResult art, int? punkte, string text)
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Vergangen);
        var eintrag = await TeamEintragenAsync(w, tag, w.Bello);
        w.Meldungen.Created.Clear();

        await ErgebnisAsync(w, tag, eintrag, art, punkte);

        var meldung = Assert.Single(w.Meldungen.Created);
        Assert.Equal(text, meldung.Message);
        Assert.Equal(($"/dogs/{w.Bello.Id}", w.Mitglied), (meldung.LinkPath, meldung.UserId));

        // Dasselbe Ergebnis noch einmal speichern: keine zweite Nachricht. Zurücksetzen: keine Nachricht.
        await ErgebnisAsync(w, tag, eintrag, art, punkte);
        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Open);
        Assert.Single(w.Meldungen.Created);
    }

    // ------------------------------------------------------------ Anonymisierung, Auskunft, Löschung

    [Fact]
    public async Task Austritt_LoestDieMitgliedTeamsVonDerPerson_DasErgebnisBleibt()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Vergangen);
        var eintrag = await TeamEintragenAsync(w, tag, w.Bello);
        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Passed, 250);
        var vereine = new ClubService(w.Db, w.Nutzer, w.Meldungen, new TrainerRoleService(w.Db, w.Nutzer));

        var ergebnis = await vereine.LeaveClubAsync(w.Mitglied, w.Verein.Id);

        Assert.True(ergebnis.Succeeded);
        var e = await w.Db.ClubExamEntries.AsNoTracking().SingleAsync();
        Assert.Equal((null, null, null, false), (e.MemberUserId, e.DogId, e.GoalId, e.GoalCreated));
        Assert.Equal((ClubExamResult.Passed, 250), (e.Result, e.Score));
        var dto = (await w.Dienst.GetAsync(w.Leitung, w.Verein.Id, tag)).Value!.Entries.Single();
        Assert.Equal(ClubExamTeamKind.FormerMember, dto.Kind);
        Assert.Null(dto.HandlerName);
        Assert.Null(dto.DogName);
        Assert.Equal(1, (await w.Dienst.GetListAsync(w.Leitung, w.Verein.Id)).Value!.Single().PassedCount);
        // Die Leistung am Hund gehört der Familie und bleibt.
        Assert.Equal(GoalStatus.Achieved, (await w.Db.Goals.AsNoTracking().SingleAsync()).Status);
        // Eine Korrektur der Prüfungsleitung fasst den Hund der Familie nicht mehr an.
        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Failed);
        Assert.Equal(GoalStatus.Achieved, (await w.Db.Goals.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Austritt_EinesAnderenMitglieds_LaesstDieTeamsUnberuehrt()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Kommend);
        await TeamEintragenAsync(w, tag, w.Bello);
        var anderer = Guid.NewGuid();
        w.Nutzer.Register(anderer, "a@example.org", "Anna", "Andere");
        w.Db.ClubMemberships.Add(new ClubMembership { ClubId = w.Verein.Id, UserId = anderer, Status = ClubMembershipStatus.Approved });
        await w.Db.SaveChangesAsync();
        var vereine = new ClubService(w.Db, w.Nutzer, w.Meldungen, new TrainerRoleService(w.Db, w.Nutzer));

        await vereine.LeaveClubAsync(anderer, w.Verein.Id);

        Assert.Equal(ClubExamTeamKind.Member, (await w.Dienst.GetAsync(w.Leitung, w.Verein.Id, tag)).Value!.Entries.Single().Kind);
    }

    [Fact]
    public async Task LetzterRollenentzugOhneMitgliedschaft_LoestDasTeam_MitMitgliedschaftNicht()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Kommend);
        var nurRolle = Guid.NewGuid();
        w.Nutzer.Register(nurRolle, "rolle@example.org", "Rolf", "Rolle");
        w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = w.Verein.Id, UserId = nurRolle, Role = ClubRole.Training });
        var rex = new Dog { Name = "Rex" };
        w.Db.Dogs.Add(rex);
        w.Db.DogOwners.Add(new DogOwner { DogId = rex.Id, UserId = nurRolle });
        await w.Db.SaveChangesAsync();
        Assert.True((await w.Dienst.AddEntryAsync(w.Leitung, w.Verein.Id, tag, new(w.Pruefung.Id, nurRolle, rex.Id, null, null, null))).Succeeded);
        var vereine = new ClubService(w.Db, w.Nutzer, w.Meldungen, new TrainerRoleService(w.Db, w.Nutzer));

        // Mit der Mitgliedschaft gehört die Person weiter zum Verein: das Team bleibt.
        w.Db.ClubMemberships.Add(new ClubMembership { ClubId = w.Verein.Id, UserId = nurRolle, Status = ClubMembershipStatus.Approved });
        await w.Db.SaveChangesAsync();
        Assert.True((await vereine.RemoveTrainerAsync(w.Leitung, false, w.Verein.Id, nurRolle)).Succeeded);
        Assert.Equal(nurRolle, (await w.Db.ClubExamEntries.AsNoTracking().SingleAsync()).MemberUserId);

        // Ohne beides verlässt sie den Verein: das Team wird gelöst.
        var mitgliedschaft = await w.Db.ClubMemberships.SingleAsync(m => m.UserId == nurRolle);
        mitgliedschaft.DeletedAt = Jetzt;
        w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = w.Verein.Id, UserId = nurRolle, Role = ClubRole.Training });
        await w.Db.SaveChangesAsync();
        Assert.True((await vereine.RemoveTrainerAsync(w.Leitung, false, w.Verein.Id, nurRolle)).Succeeded);
        var e = await w.Db.ClubExamEntries.AsNoTracking().SingleAsync();
        Assert.Equal((null, null), (e.MemberUserId, e.DogId));
    }

    [Fact]
    public async Task AufraeumenVerwaisterDaten_LoestDieTeamsEinesGeloeschtenKontos()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Kommend);
        // Ein Konto, das es nicht mehr gibt (nirgends registriert), aber noch Mitglied mit Team ist.
        var weg = Guid.NewGuid();
        w.Db.ClubMemberships.Add(new ClubMembership { ClubId = w.Verein.Id, UserId = weg, Status = ClubMembershipStatus.Approved });
        var rex = new Dog { Name = "Rex" };
        w.Db.Dogs.Add(rex);
        w.Db.ClubExamEntries.Add(new ClubExamEntry { ExamId = tag, RegulationId = w.Pruefung.Id, StartOrder = 1, MemberUserId = weg, DogId = rex.Id });
        await TeamEintragenAsync(w, tag, w.Bello);
        await w.Db.SaveChangesAsync();

        await new CommunityOrphanCleanup(w.Db, w.Nutzer, w.Meldungen).CleanupAsync();

        var zeilen = await w.Db.ClubExamEntries.AsNoTracking().ToListAsync();
        Assert.Equal(2, zeilen.Count);
        Assert.Single(zeilen, z => z.MemberUserId == w.Mitglied && z.DogId == w.Bello.Id);
        Assert.Single(zeilen, z => z.MemberUserId is null && z.DogId is null);
    }

    [Fact]
    public async Task HundGeloescht_LoestDasTeamVonPersonUndHund()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Kommend);
        await TeamEintragenAsync(w, tag, w.Bello);
        var hunde = new Dogity.Application.Dogs.DogService(w.Db, w.Nutzer, w.Meldungen);

        Assert.True((await hunde.DeleteAsync(w.Mitglied, w.Bello.Id)).Succeeded);

        var e = await w.Db.ClubExamEntries.AsNoTracking().SingleAsync();
        Assert.Equal((null, null), (e.MemberUserId, e.DogId));
        Assert.Equal(ClubExamTeamKind.FormerMember, (await w.Dienst.GetAsync(w.Leitung, w.Verein.Id, tag)).Value!.Entries.Single().Kind);
    }

    [Fact]
    public async Task KontoLoeschen_LoestDasTeam_UndBehaeltDasErgebnisFuerDenVerein()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Vergangen);
        var eintrag = await TeamEintragenAsync(w, tag, w.Bello);
        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Passed, 250, "Notiz");
        var konto = new Dogity.Application.Account.AccountDataService(w.Db, w.Nutzer, w.Meldungen);

        var ergebnis = await konto.PurgeAsync(w.Mitglied);

        Assert.True(ergebnis.Succeeded);
        var e = await w.Db.ClubExamEntries.IgnoreQueryFilters().SingleAsync();
        Assert.Equal((null, null, null), (e.MemberUserId, e.DogId, e.GoalId));
        Assert.Equal((ClubExamResult.Passed, 250), (e.Result, e.Score));
        Assert.Empty(await w.Db.Goals.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task KontoLoeschen_DerPruefungsleitung_BehaeltTagUndTeams()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Vergangen);
        var eintrag = await TeamEintragenAsync(w, tag, w.Bello);
        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Failed);
        var konto = new Dogity.Application.Account.AccountDataService(w.Db, w.Nutzer, w.Meldungen);

        Assert.True((await konto.PurgeAsync(w.Leitung)).Succeeded);

        Assert.Null((await w.Db.ClubExams.IgnoreQueryFilters().SingleAsync()).CreatedByUserId);
        Assert.Null((await w.Db.ClubExamEntries.IgnoreQueryFilters().SingleAsync()).ResultByUserId);
        Assert.Equal(w.Mitglied, (await w.Db.ClubExamEntries.IgnoreQueryFilters().SingleAsync()).MemberUserId);
    }

    [Fact]
    public async Task Export_EnthaeltDieEigenenMeldungen_NichtDieGaeste()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Vergangen);
        var eintrag = await TeamEintragenAsync(w, tag, w.Bello);
        await ErgebnisAsync(w, tag, eintrag, ClubExamResult.Passed, 250, "gut");
        await w.Dienst.AddEntryAsync(w.Leitung, w.Verein.Id, tag, new(w.Pruefung.Id, null, null, "Gerd Gast", "Rex", null));
        var konto = new Dogity.Application.Account.AccountDataService(w.Db, w.Nutzer, w.Meldungen);

        var export = (await konto.ExportAsync(w.Mitglied)).Value!;

        var meldung = Assert.Single(export.Pruefungsmeldungen!);
        Assert.Equal(("Hundesportverein Nord", Vergangen, "Prüfung A", w.Bello.Id, "bestanden", 250, "gut"),
            (meldung.Verein, meldung.Datum, meldung.Ordnung, meldung.HundId, meldung.Ergebnis, meldung.Punkte, meldung.Bemerkung));
        Assert.DoesNotContain("Gerd", System.Text.Json.JsonSerializer.Serialize(export));
        var leitung = (await konto.ExportAsync(w.Leitung)).Value!;
        Assert.Empty(leitung.Pruefungsmeldungen!);
    }

    // ------------------------------------------------------------ Familie

    [Fact]
    public async Task Hundeseite_ZeigtAnstehendeMeldungen_NurDerFamilie()
    {
        var w = await AufbauenAsync();
        var kommend = await TagAnlegenAsync(w, Kommend);
        var vorbei = await TagAnlegenAsync(w, Vergangen);
        var abgesagt = await TagAnlegenAsync(w, Kommend.AddDays(7));
        await TeamEintragenAsync(w, kommend, w.Bello);
        await TeamEintragenAsync(w, vorbei, w.Bello);
        await TeamEintragenAsync(w, abgesagt, w.Bello);
        await w.Dienst.CancelAsync(w.Leitung, w.Verein.Id, abgesagt);
        var trainer = Guid.NewGuid();
        w.Db.TrainerAssignments.Add(new TrainerAssignment { DogId = w.Bello.Id, TrainerId = trainer, MemberId = w.Mitglied });
        await w.Db.SaveChangesAsync();

        var liste = (await w.Dienst.GetForDogAsync(w.Mitglied, w.Bello.Id)).Value!;

        var meldung = Assert.Single(liste);
        Assert.Equal((Kommend, "Hundesportverein Nord", w.Pruefung.Id), (meldung.Date, meldung.ClubName, meldung.Regulation.Id));
        Assert.True((await w.Dienst.GetForDogAsync(trainer, w.Bello.Id)).IsNotFound);
        Assert.True((await w.Dienst.GetForDogAsync(Guid.NewGuid(), w.Bello.Id)).IsNotFound);
    }

    // ------------------------------------------------------------ Vereinsliste und Übersicht

    [Theory]
    [InlineData(ClubRole.Training, true, false)]
    [InlineData(ClubRole.Vorstand, true, false)]
    [InlineData(ClubRole.Ausbildungswart, true, true)]
    [InlineData(ClubRole.Verwaltung, true, true)]
    [InlineData(ClubRole.Mitgliederverwaltung, false, false)]
    public async Task Vereinsliste_ZeigtDenNaechstenTagNurTrainingVorstandUndLeitung(ClubRole rollen, bool sieht, bool verwaltet)
    {
        var w = await AufbauenAsync();
        await TagAnlegenAsync(w, Kommend.AddDays(10), w.OhnePunkte.Id);
        var naechster = await TagAnlegenAsync(w, Kommend, w.Pruefung.Id);
        await TagAnlegenAsync(w, Vergangen);
        var abgesagt = await TagAnlegenAsync(w, Kommend.AddDays(-5));
        await w.Dienst.CancelAsync(w.Leitung, w.Verein.Id, abgesagt);
        var person = Guid.NewGuid();
        w.Nutzer.Register(person, "p@example.org", "Pia", "Person");
        w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = w.Verein.Id, UserId = person, Role = rollen });
        await w.Db.SaveChangesAsync();
        var vereine = new ClubService(w.Db, w.Nutzer, w.Meldungen, new TrainerRoleService(w.Db, w.Nutzer));

        var club = (await vereine.GetMyClubRolesAsync(person)).Value!.Single();

        Assert.Equal(verwaltet, club.CanManageExams);
        if (!sieht)
        {
            Assert.Null(club.NextExam);
            return;
        }
        var hinweis = club.NextExam!;
        Assert.Equal((Kommend, "Vereinsgelände"), (hinweis.Date, hinweis.Location));
        Assert.Equal([w.Pruefung.Id], hinweis.Regulations.Select(r => r.Id));
        Assert.NotEqual(Guid.Empty, naechster);
    }

    [Fact]
    public async Task Uebersicht_ZaehltPruefungenTeamsUndBestandene_OhneNamen()
    {
        var w = await AufbauenAsync();
        var tag = await TagAnlegenAsync(w, Vergangen);
        var bello = await TeamEintragenAsync(w, tag, w.Bello);
        var emma = await TeamEintragenAsync(w, tag, w.Emma);
        await ErgebnisAsync(w, tag, bello, ClubExamResult.Passed);
        await ErgebnisAsync(w, tag, emma, ClubExamResult.Failed);
        var abgesagt = await TagAnlegenAsync(w, Vergangen.AddDays(-1));
        await TeamEintragenAsync(w, abgesagt, w.Bello);
        await w.Dienst.CancelAsync(w.Leitung, w.Verein.Id, abgesagt);
        var vorstand = Guid.NewGuid();
        w.Nutzer.Register(vorstand, "v@example.org", "Vera", "Vorstand");
        w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = w.Verein.Id, UserId = vorstand, Role = ClubRole.Vorstand });
        await w.Db.SaveChangesAsync();
        var uebersicht = new ClubOverviewService(w.Db, w.Nutzer, new FesteUhr());

        var monate = (await uebersicht.GetOverviewAsync(vorstand, false, w.Verein.Id)).Value!.Months;

        var oktober = monate.Single(m => (m.Year, m.Month) == (2026, 10));
        Assert.Equal((1, 2, 1), (oktober.Exams, oktober.ExamTeams, oktober.ExamsPassed));
        Assert.All(monate.Where(m => m != oktober), m => Assert.Equal(0, m.Exams));
    }
}
