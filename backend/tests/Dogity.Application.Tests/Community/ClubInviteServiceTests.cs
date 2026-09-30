using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Einladungslink der Vereine: Rechte zum Verwalten, die öffentliche Vorschau
/// (darf nichts verraten) und der Beitritt darüber (darf nichts freigeben).
/// </summary>
public class ClubInviteServiceTests
{
    private record Aufbau(
        ClubInviteService Dienst,
        ClubService Vereine,
        ApplicationDbContext Db,
        Club Verein,
        Guid Verwaltung,
        Guid Training);

    private static async Task<Aufbau> AufsetzenAsync()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var vereine = new ClubService(db, lookup, new FakeNotificationService(), new TrainerRoleService(db, lookup));
        var verein = new Club { Name = "Hundesportverein Nord" };
        var verwaltung = Guid.NewGuid();
        var training = Guid.NewGuid();
        db.Clubs.Add(verein);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = verein.Id, UserId = verwaltung, Role = ClubRole.Verwaltung });
        db.ClubTrainers.Add(new ClubTrainer { ClubId = verein.Id, UserId = training, Role = ClubRole.Training });
        await db.SaveChangesAsync();
        return new Aufbau(new ClubInviteService(db, vereine), vereine, db, verein, verwaltung, training);
    }

    [Fact]
    public void Code_HatDieErwarteteFormUndIstJedesMalAnders()
    {
        var a = ClubInviteCode.Erzeugen();
        var b = ClubInviteCode.Erzeugen();

        Assert.Equal(ClubInviteCode.Laenge, a.Length);
        Assert.True(ClubInviteCode.IstGueltigeForm(a));
        Assert.NotEqual(a, b);
        // URL-sicher: nichts, was im Link maskiert werden müsste.
        Assert.DoesNotContain('+', a);
        Assert.DoesNotContain('/', a);
        Assert.DoesNotContain('=', a);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("zu-kurz")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA+")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAA ä")]
    public void Code_FalscheFormWirdAbgewiesen(string? code) =>
        Assert.False(ClubInviteCode.IstGueltigeForm(code));

    [Fact]
    public async Task Get_OhneLink_LiefertNull()
    {
        var a = await AufsetzenAsync();

        var ergebnis = await a.Dienst.GetAsync(a.Verwaltung, a.Verein.Id);

        Assert.True(ergebnis.Succeeded);
        Assert.Null(ergebnis.Value);
    }

    [Fact]
    public async Task Verwalten_DarfWerBeitrittsanfragenEntscheidenDarf()
    {
        var a = await AufsetzenAsync();

        // Auch Trainer:innen mit der Rolle Training - wie bei den Anfragen.
        var erzeugt = await a.Dienst.RegenerateAsync(a.Training, a.Verein.Id);
        var gelesen = await a.Dienst.GetAsync(a.Training, a.Verein.Id);

        Assert.True(erzeugt.Succeeded);
        Assert.Equal(erzeugt.Value!.Code, gelesen.Value!.Code);
    }

    [Fact]
    public async Task Verwalten_FremdeUndMitgliederBekommenNichts()
    {
        var a = await AufsetzenAsync();
        var fremder = Guid.NewGuid();
        var mitglied = Guid.NewGuid();
        a.Db.ClubMemberships.Add(new ClubMembership { ClubId = a.Verein.Id, UserId = mitglied, Status = ClubMembershipStatus.Approved });
        var anderer = new Club { Name = "Anderer Verein" };
        var trainerAnderswo = Guid.NewGuid();
        a.Db.Clubs.Add(anderer);
        a.Db.ClubTrainers.Add(new ClubTrainer { ClubId = anderer.Id, UserId = trainerAnderswo, Role = ClubRole.Verwaltung });
        await a.Db.SaveChangesAsync();
        await a.Dienst.RegenerateAsync(a.Verwaltung, a.Verein.Id);

        foreach (var wer in new[] { fremder, mitglied, trainerAnderswo })
        {
            var lesen = await a.Dienst.GetAsync(wer, a.Verein.Id);
            var erzeugen = await a.Dienst.RegenerateAsync(wer, a.Verein.Id);
            var abschalten = await a.Dienst.DisableAsync(wer, a.Verein.Id);

            // Wie überall bei Vereinen: 404, damit niemand erfährt, ob es ihn gibt.
            Assert.True(lesen.IsNotFound);
            Assert.True(erzeugen.IsNotFound);
            Assert.True(abschalten.IsNotFound);
        }

        // Und der Code ist danach unverändert da.
        Assert.NotNull((await a.Dienst.GetAsync(a.Verwaltung, a.Verein.Id)).Value);
    }

    [Fact]
    public async Task NeuerCode_EntwertetDenAlten()
    {
        var a = await AufsetzenAsync();
        var alt = (await a.Dienst.RegenerateAsync(a.Verwaltung, a.Verein.Id)).Value!.Code;

        var neu = (await a.Dienst.RegenerateAsync(a.Verwaltung, a.Verein.Id)).Value!.Code;

        Assert.NotEqual(alt, neu);
        Assert.True((await a.Dienst.GetPreviewAsync(alt)).IsNotFound);
        Assert.True((await a.Dienst.GetPreviewAsync(neu)).Succeeded);
    }

    [Fact]
    public async Task Abschalten_MachtDenCodeUngueltigUndIstWiederholbar()
    {
        var a = await AufsetzenAsync();
        var code = (await a.Dienst.RegenerateAsync(a.Verwaltung, a.Verein.Id)).Value!.Code;

        Assert.True((await a.Dienst.DisableAsync(a.Verwaltung, a.Verein.Id)).Succeeded);
        Assert.True((await a.Dienst.DisableAsync(a.Verwaltung, a.Verein.Id)).Succeeded);

        Assert.True((await a.Dienst.GetPreviewAsync(code)).IsNotFound);
        Assert.Null((await a.Dienst.GetAsync(a.Verwaltung, a.Verein.Id)).Value);
    }

    [Fact]
    public async Task Vorschau_ZeigtNurDenVereinsnamen()
    {
        var a = await AufsetzenAsync();
        var code = (await a.Dienst.RegenerateAsync(a.Verwaltung, a.Verein.Id)).Value!.Code;

        var vorschau = await a.Dienst.GetPreviewAsync(code);

        Assert.Equal("Hundesportverein Nord", vorschau.Value!.ClubName);
        // Der Typ hat nur diese eine Eigenschaft - kein Weg, mehr preiszugeben.
        Assert.Single(typeof(ClubInvitePreviewDto).GetProperties());
    }

    [Fact]
    public async Task Vorschau_AlleGruendeFuerUngueltigAntwortenGleich()
    {
        var a = await AufsetzenAsync();
        var anderer = new Club { Name = "Weg", InviteCode = ClubInviteCode.Erzeugen() };
        a.Db.Clubs.Add(anderer);
        anderer.DeletedAt = DateTimeOffset.UtcNow;
        await a.Db.SaveChangesAsync();

        var unbekannt = await a.Dienst.GetPreviewAsync(ClubInviteCode.Erzeugen());
        var schlechteForm = await a.Dienst.GetPreviewAsync("x");
        var vereinGeloescht = await a.Dienst.GetPreviewAsync(anderer.InviteCode!);

        foreach (var e in new[] { unbekannt, schlechteForm, vereinGeloescht })
        {
            Assert.True(e.IsNotFound);
            Assert.Equal(unbekannt.Errors, e.Errors);
        }
    }

    [Fact]
    public async Task Beitritt_LegtEineOffeneAnfrageMitHerkunftEinladungslinkAn()
    {
        var a = await AufsetzenAsync();
        var code = (await a.Dienst.RegenerateAsync(a.Verwaltung, a.Verein.Id)).Value!.Code;
        var neu = Guid.NewGuid();

        var ergebnis = await a.Dienst.JoinAsync(neu, code);

        // Keine automatische Mitgliedschaft - der Verein gibt frei.
        Assert.True(ergebnis.Succeeded);
        Assert.Equal(ClubMembershipStatus.Pending, ergebnis.Value!.Status);
        var zeile = await a.Db.ClubMemberships.SingleAsync(m => m.UserId == neu);
        Assert.Equal(ClubMembershipSource.InviteLink, zeile.Source);
        Assert.Equal(ClubMembershipStatus.Pending, zeile.Status);
    }

    [Fact]
    public async Task Beitritt_AnfrageErscheintMitHerkunftInDerListeDerTrainer()
    {
        var a = await AufsetzenAsync();
        var code = (await a.Dienst.RegenerateAsync(a.Verwaltung, a.Verein.Id)).Value!.Code;
        await a.Dienst.JoinAsync(Guid.NewGuid(), code);
        await a.Vereine.RequestJoinAsync(Guid.NewGuid(), a.Verein.Id);

        var liste = (await a.Vereine.GetJoinRequestsAsync(a.Training, a.Verein.Id)).Value!;

        Assert.Equal(2, liste.Count);
        Assert.Single(liste, m => m.Source == ClubMembershipSource.InviteLink);
        Assert.Single(liste, m => m.Source == ClubMembershipSource.Directory);
    }

    [Fact]
    public async Task Beitritt_IstIdempotentBeiOffenerAnfrage()
    {
        var a = await AufsetzenAsync();
        var code = (await a.Dienst.RegenerateAsync(a.Verwaltung, a.Verein.Id)).Value!.Code;
        var neu = Guid.NewGuid();
        var erste = await a.Dienst.JoinAsync(neu, code);

        var zweite = await a.Dienst.JoinAsync(neu, code);

        Assert.True(zweite.Succeeded);
        Assert.Equal(erste.Value!.Id, zweite.Value!.Id);
        Assert.Equal(1, await a.Db.ClubMemberships.CountAsync(m => m.UserId == neu));
    }

    [Fact]
    public async Task Beitritt_IstIdempotentBeiBestehenderMitgliedschaftUndAendertDieHerkunftNicht()
    {
        var a = await AufsetzenAsync();
        var code = (await a.Dienst.RegenerateAsync(a.Verwaltung, a.Verein.Id)).Value!.Code;
        var mitglied = Guid.NewGuid();
        a.Db.ClubMemberships.Add(new ClubMembership
        {
            ClubId = a.Verein.Id, UserId = mitglied, Status = ClubMembershipStatus.Approved,
        });
        await a.Db.SaveChangesAsync();

        var ergebnis = await a.Dienst.JoinAsync(mitglied, code);

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(ClubMembershipStatus.Approved, ergebnis.Value!.Status);
        var zeile = await a.Db.ClubMemberships.SingleAsync(m => m.UserId == mitglied);
        Assert.Equal(ClubMembershipSource.Directory, zeile.Source);
    }

    [Fact]
    public async Task Beitritt_NachAblehnungIstEineNeueAnfrageMoeglich()
    {
        var a = await AufsetzenAsync();
        var code = (await a.Dienst.RegenerateAsync(a.Verwaltung, a.Verein.Id)).Value!.Code;
        var person = Guid.NewGuid();
        var erste = await a.Dienst.JoinAsync(person, code);
        await a.Vereine.DecideJoinRequestAsync(a.Verwaltung, a.Verein.Id, erste.Value!.Id, approve: false);

        var neu = await a.Dienst.JoinAsync(person, code);

        Assert.True(neu.Succeeded);
        Assert.Equal(ClubMembershipStatus.Pending, neu.Value!.Status);
        Assert.NotEqual(erste.Value.Id, neu.Value.Id);
        var zeile = await a.Db.ClubMemberships.SingleAsync(m => m.Id == neu.Value.Id);
        Assert.Equal(ClubMembershipSource.InviteLink, zeile.Source);
    }

    [Fact]
    public async Task Beitritt_NachAustrittIstEineNeueAnfrageMoeglich()
    {
        var a = await AufsetzenAsync();
        var code = (await a.Dienst.RegenerateAsync(a.Verwaltung, a.Verein.Id)).Value!.Code;
        var person = Guid.NewGuid();
        var erste = await a.Dienst.JoinAsync(person, code);
        await a.Vereine.DecideJoinRequestAsync(a.Verwaltung, a.Verein.Id, erste.Value!.Id, approve: true);
        await a.Vereine.LeaveClubAsync(person, a.Verein.Id);

        var neu = await a.Dienst.JoinAsync(person, code);

        Assert.True(neu.Succeeded);
        Assert.Equal(ClubMembershipStatus.Pending, neu.Value!.Status);
        var zeile = await a.Db.ClubMemberships.SingleAsync(m => m.Id == neu.Value.Id);
        Assert.Equal(ClubMembershipSource.InviteLink, zeile.Source);
    }

    [Fact]
    public async Task Anfrage_UeberDieListeHatDieHerkunftVerzeichnis()
    {
        var a = await AufsetzenAsync();
        var person = Guid.NewGuid();

        var ergebnis = await a.Vereine.RequestJoinAsync(person, a.Verein.Id);

        Assert.True(ergebnis.Succeeded);
        var zeile = await a.Db.ClubMemberships.SingleAsync(m => m.UserId == person);
        Assert.Equal(ClubMembershipSource.Directory, zeile.Source);
    }

    [Fact]
    public async Task Beitritt_TrainerDerZugleichMitgliedIstBekommtDenBestehendenStand()
    {
        var a = await AufsetzenAsync();
        var code = (await a.Dienst.RegenerateAsync(a.Verwaltung, a.Verein.Id)).Value!.Code;
        a.Db.ClubMemberships.Add(new ClubMembership
        {
            ClubId = a.Verein.Id, UserId = a.Training, Status = ClubMembershipStatus.Approved,
        });
        await a.Db.SaveChangesAsync();

        var ergebnis = await a.Dienst.JoinAsync(a.Training, code);

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(ClubMembershipStatus.Approved, ergebnis.Value!.Status);
        Assert.Equal(1, await a.Db.ClubMemberships.CountAsync(m => m.UserId == a.Training));
    }

    [Fact]
    public async Task Beitritt_MitUngueltigemCodeScheitertAlsNichtGefunden()
    {
        var a = await AufsetzenAsync();
        var code = (await a.Dienst.RegenerateAsync(a.Verwaltung, a.Verein.Id)).Value!.Code;
        await a.Dienst.DisableAsync(a.Verwaltung, a.Verein.Id);

        var ergebnis = await a.Dienst.JoinAsync(Guid.NewGuid(), code);

        Assert.True(ergebnis.IsNotFound);
        Assert.Empty(a.Db.ClubMemberships);
    }

    [Fact]
    public async Task Beitritt_TrainerDesVereinsLegtKeineAnfrageAn()
    {
        var a = await AufsetzenAsync();
        var code = (await a.Dienst.RegenerateAsync(a.Verwaltung, a.Verein.Id)).Value!.Code;

        var ergebnis = await a.Dienst.JoinAsync(a.Verwaltung, code);

        Assert.False(ergebnis.Succeeded);
        Assert.Empty(a.Db.ClubMemberships);
    }

    [Fact]
    public async Task Beitritt_GeloeschterVereinIstNichtGefunden()
    {
        var a = await AufsetzenAsync();
        var code = (await a.Dienst.RegenerateAsync(a.Verwaltung, a.Verein.Id)).Value!.Code;
        a.Verein.DeletedAt = DateTimeOffset.UtcNow;
        await a.Db.SaveChangesAsync();

        var ergebnis = await a.Dienst.JoinAsync(Guid.NewGuid(), code);

        Assert.True(ergebnis.IsNotFound);
    }
}
