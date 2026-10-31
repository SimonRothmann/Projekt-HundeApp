using Dogity.Application.Abstractions;
using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Einladung statt Direktaufnahme: Eine Vereinstrainer:in kann niemanden ohne
/// dessen Zustimmung in den Verein aufnehmen. Eingeladen heißt nicht Mitglied
/// - erst die Zusage der Person (oder ihr eigener Antrag) macht daraus eine
/// Mitgliedschaft. Die Trennung zwischen Vereinen prüft VereinsgrenzenTests.
/// </summary>
public class ClubEinladungTests
{
    private const string MailPerson = "person@example.org";

    private sealed record Aufbau(
        ApplicationDbContext Db,
        ClubService Dienst,
        ClubInviteService Einladungslink,
        FakeNotificationService Meldungen,
        Club Verein,
        Club Anderer,
        Guid Verwaltung,
        Guid Mitgliederverwaltung,
        Guid FremdeTrainerin,
        Guid Person,
        Guid Dritte);

    private static async Task<Aufbau> AufsetzenAsync()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = new FakeUserLookupService();
        var meldungen = new FakeNotificationService();
        var dienst = new ClubService(db, nutzer, meldungen, new TrainerRoleService(db, nutzer));
        var verein = new Club { Name = "Hundesportverein Nord" };
        var anderer = new Club { Name = "Hundesportverein Süd" };
        var verwaltung = Guid.NewGuid();
        var mitgliederverwaltung = Guid.NewGuid();
        var fremde = Guid.NewGuid();
        var person = Guid.NewGuid();
        var dritte = Guid.NewGuid();
        nutzer.Register(verwaltung, "verwaltung@example.org", "Vera", "Walter");
        nutzer.Register(mitgliederverwaltung, "mitgliederverwaltung@example.org", "Mara", "Mitglieder");
        nutzer.Register(fremde, "fremde@example.org", "Frieda", "Fremd");
        nutzer.Register(person, MailPerson, "Paula", "Person");
        nutzer.Register(dritte, "dritte@example.org", "Dora", "Dritte");
        db.Clubs.AddRange(verein, anderer);
        db.ClubTrainers.AddRange(
            new ClubTrainer { ClubId = verein.Id, UserId = verwaltung, Role = ClubRole.Verwaltung },
            new ClubTrainer { ClubId = verein.Id, UserId = mitgliederverwaltung, Role = ClubRole.Mitgliederverwaltung },
            new ClubTrainer { ClubId = anderer.Id, UserId = fremde, Role = ClubRole.Verwaltung });
        await db.SaveChangesAsync();
        return new Aufbau(db, dienst, new ClubInviteService(db, dienst), meldungen, verein, anderer, verwaltung, mitgliederverwaltung, fremde, person, dritte);
    }

    private static Task<ClubMembership> ZeileAsync(Aufbau a, Guid nutzer) =>
        a.Db.ClubMemberships.IgnoreQueryFilters().Where(m => m.ClubId == a.Verein.Id && m.UserId == nutzer).SingleAsync();

    private static Task<Application.Common.Result<AddClubMemberResultDto>> EinladenAsync(Aufbau a, Guid von, string mail = MailPerson) =>
        a.Dienst.AddMemberAsync(von, isAdmin: false, a.Verein.Id, new AssignClubMemberRequest(mail));

    // ------------------------------------------------------------- Einladen

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Einladen_BeideRollenLegenEineEinladungAnStattEinMitglied(bool verwaltung)
    {
        var a = await AufsetzenAsync();

        var ergebnis = await EinladenAsync(a, verwaltung ? a.Verwaltung : a.Mitgliederverwaltung);

        Assert.True(ergebnis.Succeeded);
        // Die Oberfläche meldet daran "eingeladen" statt "aufgenommen".
        Assert.False(ergebnis.Value!.Joined);
        var zeile = await ZeileAsync(a, a.Person);
        Assert.Equal(ClubMembershipStatus.Invited, zeile.Status);
        Assert.Equal(ClubMembershipSource.Invitation, zeile.Source);
        Assert.Equal(MailPerson, zeile.InvitedEmail);
        Assert.Null(zeile.DecidedAt);
        Assert.False(await a.Db.ZaehltImErststartZumVereinAsync(a.Person));
    }

    [Fact]
    public async Task Einladen_BenachrichtigtDieEingeladenePersonMitNamenDerEinladenden()
    {
        var a = await AufsetzenAsync();

        await EinladenAsync(a, a.Verwaltung);

        var hinweis = Assert.Single(a.Meldungen.Created);
        Assert.Equal(a.Person, hinweis.UserId);
        Assert.Equal("Vera Walter lädt dich in den Verein \"Hundesportverein Nord\" ein.", hinweis.Message);
        Assert.Equal("/clubs", hinweis.LinkPath);
    }

    [Fact]
    public async Task Einladen_UnbekannteAdresseBleibtEinFehler()
    {
        var a = await AufsetzenAsync();

        var ergebnis = await EinladenAsync(a, a.Verwaltung, "niemand@example.org");

        Assert.False(ergebnis.Succeeded);
        Assert.Equal("Kein Benutzer mit dieser E-Mail-Adresse gefunden.", Assert.Single(ergebnis.Errors));
        Assert.Empty(a.Db.ClubMemberships);
    }

    [Fact]
    public async Task Einladen_DoppeltIstEinFehler_UndLegtKeineZweiteZeileAn()
    {
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);

        var zweites = await EinladenAsync(a, a.Mitgliederverwaltung);

        Assert.False(zweites.Succeeded);
        Assert.Equal("Diese Person ist bereits eingeladen.", Assert.Single(zweites.Errors));
        Assert.Equal(1, await a.Db.ClubMemberships.CountAsync(m => m.UserId == a.Person));
        Assert.Single(a.Meldungen.Created);
    }

    [Fact]
    public async Task Einladen_BereitsMitgliedIstEinFehler()
    {
        var a = await AufsetzenAsync();
        a.Db.ClubMemberships.Add(new ClubMembership { ClubId = a.Verein.Id, UserId = a.Person, Status = ClubMembershipStatus.Approved });
        await a.Db.SaveChangesAsync();

        var ergebnis = await EinladenAsync(a, a.Verwaltung);

        Assert.False(ergebnis.Succeeded);
        Assert.Equal("Dieser Benutzer ist bereits Mitglied dieses Vereins.", Assert.Single(ergebnis.Errors));
    }

    [Fact]
    public async Task Einladen_OffeneAnfrageDerPersonWirdDirektFreigegeben()
    {
        var a = await AufsetzenAsync();
        await a.Dienst.RequestJoinAsync(a.Person, a.Verein.Id);

        var ergebnis = await EinladenAsync(a, a.Mitgliederverwaltung);

        Assert.True(ergebnis.Succeeded);
        Assert.True(ergebnis.Value!.Joined);
        var zeile = await ZeileAsync(a, a.Person);
        Assert.Equal(ClubMembershipStatus.Approved, zeile.Status);
        Assert.NotNull(zeile.DecidedAt);
        Assert.Equal(a.Mitgliederverwaltung, zeile.DecidedByUserId);
        Assert.Null(zeile.InvitedEmail);
    }

    [Fact]
    public async Task Einladen_KurzNachEinemNeinIstGesperrt_DamitKeinVereinMitEinladungenZuschuettet()
    {
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);
        await a.Dienst.RespondToInvitationAsync(a.Person, a.Verein.Id, accept: false);

        var erneut = await EinladenAsync(a, a.Verwaltung);

        Assert.False(erneut.Succeeded);
        Assert.Equal(0, await a.Db.ClubMemberships.CountAsync(m => m.UserId == a.Person && m.Status == ClubMembershipStatus.Invited));
        // Die Sperre gilt nur dem Verein, nicht der Person: Selbst beitreten geht.
        Assert.True((await a.Dienst.RequestJoinAsync(a.Person, a.Verein.Id)).Succeeded);
    }

    [Fact]
    public async Task Einladen_NachAblaufDerSperreIstEineNeueEinladungMoeglich()
    {
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);
        await a.Dienst.RespondToInvitationAsync(a.Person, a.Verein.Id, accept: false);
        var nein = await ZeileAsync(a, a.Person);
        nein.DecidedAt = DateTimeOffset.UtcNow - ClubService.EinladungsSperreNachNein - TimeSpan.FromDays(1);
        await a.Db.SaveChangesAsync();

        var erneut = await EinladenAsync(a, a.Verwaltung);

        Assert.True(erneut.Succeeded);
        Assert.Equal(1, await a.Db.ClubMemberships.CountAsync(m => m.UserId == a.Person && m.Status == ClubMembershipStatus.Invited));
    }

    [Fact]
    public async Task Einladen_EineAbgelehnteAnfrageSperrtKeineEinladung()
    {
        var a = await AufsetzenAsync();
        await a.Dienst.RequestJoinAsync(a.Person, a.Verein.Id);
        var anfrage = await ZeileAsync(a, a.Person);
        await a.Dienst.DecideJoinRequestAsync(a.Verwaltung, a.Verein.Id, anfrage.Id, approve: false);

        // Das Nein kam hier vom Verein, nicht von der Person - einladen darf er.
        Assert.True((await EinladenAsync(a, a.Verwaltung)).Succeeded);
    }

    [Fact]
    public async Task Einladen_BetreiberNimmtWeiterhinDirektAuf()
    {
        var a = await AufsetzenAsync();

        var ergebnis = await a.Dienst.AddMemberAsync(Guid.NewGuid(), isAdmin: true, a.Verein.Id, new AssignClubMemberRequest(MailPerson));

        Assert.True(ergebnis.Succeeded);
        Assert.True(ergebnis.Value!.Joined);
        var zeile = await ZeileAsync(a, a.Person);
        Assert.Equal(ClubMembershipStatus.Approved, zeile.Status);
        Assert.NotNull(zeile.DecidedAt);
        Assert.Null(zeile.InvitedEmail);
        Assert.Equal("Du wurdest zum Verein \"Hundesportverein Nord\" hinzugefügt.", Assert.Single(a.Meldungen.Created).Message);
    }

    [Fact]
    public async Task Einladen_BetreiberMeldetEinBestehendesMitgliedWieBisher()
    {
        var a = await AufsetzenAsync();
        var admin = Guid.NewGuid();
        await a.Dienst.AddMemberAsync(admin, isAdmin: true, a.Verein.Id, new AssignClubMemberRequest(MailPerson));

        var nochmal = await a.Dienst.AddMemberAsync(admin, isAdmin: true, a.Verein.Id, new AssignClubMemberRequest(MailPerson));

        Assert.False(nochmal.Succeeded);
        Assert.Equal("Dieser Benutzer ist bereits Mitglied dieses Vereins.", Assert.Single(nochmal.Errors));
    }

    [Fact]
    public async Task Einladen_NurTrainerinnenDesVereinsDuerfenEinladen()
    {
        var a = await AufsetzenAsync();

        // Eine Trainerin eines anderen Vereins und eine gewöhnliche Person.
        Assert.True((await EinladenAsync(a, a.FremdeTrainerin)).IsNotFound);
        Assert.True((await EinladenAsync(a, a.Dritte)).IsNotFound);

        Assert.Empty(a.Db.ClubMemberships);
    }

    // ------------------------------------------------------ Sichtbarkeit

    [Fact]
    public async Task Einladungen_ZeigenNurDieEingegebeneAdresse_KeinenNamen()
    {
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);

        var liste = (await a.Dienst.GetInvitationsAsync(a.Mitgliederverwaltung, a.Verein.Id)).Value!;

        var einladung = Assert.Single(liste);
        Assert.Equal(MailPerson, einladung.InvitedEmail);
        Assert.Equal(["MembershipId", "InvitedEmail", "RequestedAt"], typeof(ClubInvitationDto).GetProperties().Select(p => p.Name));
        Assert.DoesNotContain("Paula", System.Text.Json.JsonSerializer.Serialize(einladung));
    }

    [Fact]
    public async Task Einladungen_ErscheinenWederAlsMitgliederNochAlsAnfragen()
    {
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);

        Assert.Empty((await a.Dienst.GetMembersAsync(a.Verwaltung, a.Verein.Id)).Value!);
        Assert.Empty((await a.Dienst.GetJoinRequestsAsync(a.Verwaltung, a.Verein.Id)).Value!);
        Assert.Empty((await a.Dienst.GetDetailAsync(a.Verein.Id)).Value!.Members);
    }

    [Fact]
    public async Task Einladungen_SindNurFuerTrainerinnenDesVereinsEinsehbar()
    {
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);

        Assert.True((await a.Dienst.GetInvitationsAsync(a.FremdeTrainerin, a.Verein.Id)).IsNotFound);
        Assert.True((await a.Dienst.GetInvitationsAsync(a.Person, a.Verein.Id)).IsNotFound);
    }

    [Fact]
    public async Task EigeneMitgliedschaften_ZeigenDieEinladungMitStatusInvited()
    {
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);

        var meine = (await a.Dienst.GetMyMembershipsAsync(a.Person)).Value!;

        var eintrag = Assert.Single(meine);
        Assert.Equal(ClubMembershipStatus.Invited, eintrag.Status);
        Assert.Equal("Hundesportverein Nord", eintrag.ClubName);
    }

    [Fact]
    public async Task OffeneEinladungen_FuerDieStartseite_NurDieEigenenOffenen()
    {
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);
        // Dieselbe Person hat im anderen Verein schon zugesagt - das ist keine offene Einladung.
        a.Db.ClubMemberships.Add(new ClubMembership { ClubId = a.Anderer.Id, UserId = a.Person, Status = ClubMembershipStatus.Approved });
        a.Db.ClubMemberships.Add(new ClubMembership { ClubId = a.Verein.Id, UserId = a.Dritte, Status = ClubMembershipStatus.Pending });
        await a.Db.SaveChangesAsync();

        var offen = await a.Dienst.GetMyOpenInvitationsAsync(a.Person);

        Assert.Equal((a.Verein.Id, "Hundesportverein Nord"), Assert.Single(offen));
        Assert.Empty(await a.Dienst.GetMyOpenInvitationsAsync(a.Dritte));
        Assert.Empty(await a.Dienst.GetMyOpenInvitationsAsync(a.Verwaltung));
    }

    // ---------------------------------------------------------------- Antwort

    [Fact]
    public async Task Annehmen_MachtDiePersonZumMitglied_UndLoeschtDieAdresse()
    {
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);

        var ergebnis = await a.Dienst.RespondToInvitationAsync(a.Person, a.Verein.Id, accept: true);

        Assert.True(ergebnis.Succeeded);
        var zeile = await ZeileAsync(a, a.Person);
        Assert.Equal(ClubMembershipStatus.Approved, zeile.Status);
        Assert.NotNull(zeile.DecidedAt);
        Assert.Null(zeile.InvitedEmail);
        Assert.Equal(a.Person, Assert.Single((await a.Dienst.GetMembersAsync(a.Verwaltung, a.Verein.Id)).Value!).UserId);
        Assert.True(await a.Db.ZaehltImErststartZumVereinAsync(a.Person));
        Assert.Empty((await a.Dienst.GetInvitationsAsync(a.Verwaltung, a.Verein.Id)).Value!);
    }

    [Fact]
    public async Task Annehmen_ErledigtAuchEineVersehentlichDoppeltAngelegteEinladung()
    {
        // Zwei fast gleichzeitige Einladungen können zwei Zeilen anlegen (kein
        // eindeutiger Index). Die Antwort darf keine als Einladung zurücklassen.
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);
        a.Db.ClubMemberships.Add(new ClubMembership
        {
            ClubId = a.Verein.Id, UserId = a.Person, Status = ClubMembershipStatus.Invited,
            Source = ClubMembershipSource.Invitation, InvitedEmail = MailPerson, RequestedAt = DateTimeOffset.UtcNow.AddSeconds(1),
        });
        await a.Db.SaveChangesAsync();

        Assert.True((await a.Dienst.RespondToInvitationAsync(a.Person, a.Verein.Id, accept: true)).Succeeded);

        Assert.Empty(await a.Dienst.GetMyOpenInvitationsAsync(a.Person));
        Assert.Empty((await a.Dienst.GetInvitationsAsync(a.Verwaltung, a.Verein.Id)).Value!);
        Assert.Single(await a.Db.ClubMemberships.Where(m => m.UserId == a.Person && m.Status == ClubMembershipStatus.Approved).ToListAsync());
        Assert.DoesNotContain(await a.Db.ClubMemberships.IgnoreQueryFilters().ToListAsync(), m => m.UserId == a.Person && m.InvitedEmail != null);
    }

    [Fact]
    public async Task Ablehnen_LaesstDiePersonKeinMitgliedWerden_UndLoeschtDieAdresse()
    {
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);

        var ergebnis = await a.Dienst.RespondToInvitationAsync(a.Person, a.Verein.Id, accept: false);

        Assert.True(ergebnis.Succeeded);
        var zeile = await ZeileAsync(a, a.Person);
        Assert.Equal(ClubMembershipStatus.Rejected, zeile.Status);
        Assert.Null(zeile.InvitedEmail);
        Assert.Empty((await a.Dienst.GetMembersAsync(a.Verwaltung, a.Verein.Id)).Value!);
        Assert.Empty((await a.Dienst.GetInvitationsAsync(a.Verwaltung, a.Verein.Id)).Value!);
        Assert.False(await a.Db.ZaehltImErststartZumVereinAsync(a.Person));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Antworten_EineFremdePersonKannDieEinladungEinesAnderenNichtBeantworten(bool annehmen)
    {
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);

        // Weder eine Dritte noch die Vereinsleitung selbst noch die Trainerin eines anderen Vereins.
        foreach (var fremd in new[] { a.Dritte, a.Verwaltung, a.FremdeTrainerin })
            Assert.True((await a.Dienst.RespondToInvitationAsync(fremd, a.Verein.Id, annehmen)).IsNotFound);

        Assert.Equal(ClubMembershipStatus.Invited, (await ZeileAsync(a, a.Person)).Status);
        Assert.False(await a.Db.ClubMemberships.AnyAsync(m => m.UserId != a.Person));
    }

    [Fact]
    public async Task Antworten_EineEinladungInEinenAnderenVereinGiltNichtFuerDiesen()
    {
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);

        Assert.True((await a.Dienst.RespondToInvitationAsync(a.Person, a.Anderer.Id, accept: true)).IsNotFound);
        Assert.False(await a.Db.ClubMemberships.AnyAsync(m => m.ClubId == a.Anderer.Id));
    }

    [Fact]
    public async Task Antworten_OhneEinladungOderEinMalZuViel_Scheitert()
    {
        var a = await AufsetzenAsync();

        Assert.True((await a.Dienst.RespondToInvitationAsync(a.Person, a.Verein.Id, accept: true)).IsNotFound);

        await EinladenAsync(a, a.Verwaltung);
        Assert.True((await a.Dienst.RespondToInvitationAsync(a.Person, a.Verein.Id, accept: true)).Succeeded);
        // Angenommen ist angenommen: Ein zweites "Ablehnen" macht niemanden wieder zum Nicht-Mitglied.
        Assert.True((await a.Dienst.RespondToInvitationAsync(a.Person, a.Verein.Id, accept: false)).IsNotFound);
        Assert.Equal(ClubMembershipStatus.Approved, (await ZeileAsync(a, a.Person)).Status);
    }

    [Fact]
    public async Task Antworten_EineAnfrageDerPersonIstKeineEinladung()
    {
        var a = await AufsetzenAsync();
        await a.Dienst.RequestJoinAsync(a.Person, a.Verein.Id);

        // Sich selbst per "Annehmen" freizugeben wäre die Umgehung der Freigabe.
        Assert.True((await a.Dienst.RespondToInvitationAsync(a.Person, a.Verein.Id, accept: true)).IsNotFound);
        Assert.Equal(ClubMembershipStatus.Pending, (await ZeileAsync(a, a.Person)).Status);
    }

    // ------------------------------------------------ Beide Seiten wollen es

    [Fact]
    public async Task Beitrittsanfrage_BeiOffenerEinladungWirdDirektMitgliedschaft()
    {
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);

        var ergebnis = await a.Dienst.RequestJoinAsync(a.Person, a.Verein.Id);

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(ClubMembershipStatus.Approved, ergebnis.Value!.Status);
        Assert.Equal(1, await a.Db.ClubMemberships.CountAsync(m => m.UserId == a.Person));
        var zeile = await ZeileAsync(a, a.Person);
        Assert.Equal(ClubMembershipStatus.Approved, zeile.Status);
        Assert.Null(zeile.InvitedEmail);
        Assert.Empty((await a.Dienst.GetJoinRequestsAsync(a.Verwaltung, a.Verein.Id)).Value!);
    }

    [Fact]
    public async Task Einladungslink_BeiOffenerEinladungWirdDirektMitgliedschaft()
    {
        var a = await AufsetzenAsync();
        var code = (await a.Einladungslink.RegenerateAsync(a.Verwaltung, a.Verein.Id)).Value!.Code;
        await EinladenAsync(a, a.Verwaltung);

        var ergebnis = await a.Einladungslink.JoinAsync(a.Person, code);

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(ClubMembershipStatus.Approved, ergebnis.Value!.Status);
        Assert.Equal(1, await a.Db.ClubMemberships.CountAsync(m => m.UserId == a.Person));
    }

    [Fact]
    public async Task Einladungslink_OhneEinladungBleibtEineAnfrage()
    {
        var a = await AufsetzenAsync();
        var code = (await a.Einladungslink.RegenerateAsync(a.Verwaltung, a.Verein.Id)).Value!.Code;

        var ergebnis = await a.Einladungslink.JoinAsync(a.Person, code);

        Assert.Equal(ClubMembershipStatus.Pending, ergebnis.Value!.Status);
    }

    // ----------------------------------------------------------- Zurückziehen

    [Fact]
    public async Task Zurueckziehen_EntferntDieEinladung_UndSieLaesstSichNichtMehrAnnehmen()
    {
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);
        var id = Assert.Single((await a.Dienst.GetInvitationsAsync(a.Mitgliederverwaltung, a.Verein.Id)).Value!).MembershipId;

        var ergebnis = await a.Dienst.WithdrawInvitationAsync(a.Mitgliederverwaltung, a.Verein.Id, id);

        Assert.True(ergebnis.Succeeded);
        Assert.Empty((await a.Dienst.GetInvitationsAsync(a.Verwaltung, a.Verein.Id)).Value!);
        Assert.Empty(await a.Dienst.GetMyOpenInvitationsAsync(a.Person));
        Assert.True((await a.Dienst.RespondToInvitationAsync(a.Person, a.Verein.Id, accept: true)).IsNotFound);
        // Die Adresse ist auch aus der weich gelöschten Zeile weg.
        Assert.Null((await ZeileAsync(a, a.Person)).InvitedEmail);
        // Danach kann der Verein wieder einladen.
        Assert.True((await EinladenAsync(a, a.Verwaltung)).Succeeded);
    }

    [Fact]
    public async Task Zurueckziehen_NurTrainerinnenDiesesVereins()
    {
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);
        var id = Assert.Single((await a.Dienst.GetInvitationsAsync(a.Verwaltung, a.Verein.Id)).Value!).MembershipId;

        Assert.True((await a.Dienst.WithdrawInvitationAsync(a.FremdeTrainerin, a.Verein.Id, id)).IsNotFound);
        Assert.True((await a.Dienst.WithdrawInvitationAsync(a.Person, a.Verein.Id, id)).IsNotFound);
        // Auch nicht über die eigene Vereins-Id der fremden Trainerin.
        Assert.True((await a.Dienst.WithdrawInvitationAsync(a.FremdeTrainerin, a.Anderer.Id, id)).IsNotFound);

        Assert.Equal(ClubMembershipStatus.Invited, (await ZeileAsync(a, a.Person)).Status);
    }

    [Theory]
    [InlineData(ClubMembershipStatus.Approved)]
    [InlineData(ClubMembershipStatus.Pending)]
    [InlineData(ClubMembershipStatus.Rejected)]
    public async Task Zurueckziehen_NurEinladungen_KeineMitgliedschaftenOderAnfragen(ClubMembershipStatus status)
    {
        var a = await AufsetzenAsync();
        var zeile = new ClubMembership { ClubId = a.Verein.Id, UserId = a.Person, Status = status };
        a.Db.ClubMemberships.Add(zeile);
        await a.Db.SaveChangesAsync();

        var ergebnis = await a.Dienst.WithdrawInvitationAsync(a.Verwaltung, a.Verein.Id, zeile.Id);

        Assert.True(ergebnis.IsNotFound);
        Assert.Equal(status, (await ZeileAsync(a, a.Person)).Status);
        Assert.Null((await ZeileAsync(a, a.Person)).DeletedAt);
    }

    // ------------------------------------------------------- Trainer berufen

    [Fact]
    public async Task TrainerBerufen_NurAusDenFreigegebenenMitgliedern()
    {
        var a = await AufsetzenAsync();

        var nichtMitglied = await a.Dienst.AssignTrainerAsync(a.Verwaltung, isAdmin: false, a.Verein.Id, new AssignClubTrainerRequest(MailPerson));

        Assert.False(nichtMitglied.Succeeded);
        Assert.Equal(
            "Nur Mitglieder dieses Vereins können Trainer:in werden. Lade die Person zuerst in den Verein ein.",
            Assert.Single(nichtMitglied.Errors));
        Assert.False(await a.Db.ClubTrainers.AnyAsync(t => t.UserId == a.Person));
    }

    [Theory]
    [InlineData(ClubMembershipStatus.Invited)]
    [InlineData(ClubMembershipStatus.Pending)]
    [InlineData(ClubMembershipStatus.Rejected)]
    public async Task TrainerBerufen_EineEinladungOderAnfrageGenuegtNicht(ClubMembershipStatus status)
    {
        var a = await AufsetzenAsync();
        a.Db.ClubMemberships.Add(new ClubMembership { ClubId = a.Verein.Id, UserId = a.Person, Status = status });
        await a.Db.SaveChangesAsync();

        var ergebnis = await a.Dienst.AssignTrainerAsync(a.Verwaltung, isAdmin: false, a.Verein.Id, new AssignClubTrainerRequest(MailPerson));

        Assert.False(ergebnis.Succeeded);
        Assert.False(await a.Db.ClubTrainers.AnyAsync(t => t.UserId == a.Person));
    }

    [Fact]
    public async Task TrainerBerufen_EinMitgliedDiesesVereinsGeht()
    {
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);
        await a.Dienst.RespondToInvitationAsync(a.Person, a.Verein.Id, accept: true);

        var ergebnis = await a.Dienst.AssignTrainerAsync(a.Verwaltung, isAdmin: false, a.Verein.Id, new AssignClubTrainerRequest(MailPerson, ClubRole.Training));

        Assert.True(ergebnis.Succeeded);
        Assert.True(await a.Db.ClubTrainers.AnyAsync(t => t.ClubId == a.Verein.Id && t.UserId == a.Person && t.Role == ClubRole.Training));
    }

    [Fact]
    public async Task TrainerBerufen_MitgliedschaftInEinemAnderenVereinGenuegtNicht()
    {
        var a = await AufsetzenAsync();
        a.Db.ClubMemberships.Add(new ClubMembership { ClubId = a.Anderer.Id, UserId = a.Person, Status = ClubMembershipStatus.Approved });
        await a.Db.SaveChangesAsync();

        var ergebnis = await a.Dienst.AssignTrainerAsync(a.Verwaltung, isAdmin: false, a.Verein.Id, new AssignClubTrainerRequest(MailPerson));

        Assert.False(ergebnis.Succeeded);
    }

    [Fact]
    public async Task TrainerBerufen_DerBetreiberDarfWeiterhinOhneMitgliedschaft()
    {
        var a = await AufsetzenAsync();

        var ergebnis = await a.Dienst.AssignTrainerAsync(Guid.NewGuid(), isAdmin: true, a.Verein.Id, new AssignClubTrainerRequest(MailPerson));

        Assert.True(ergebnis.Succeeded);
        Assert.True(await a.Db.ClubTrainers.AnyAsync(t => t.ClubId == a.Verein.Id && t.UserId == a.Person));
    }

    [Fact]
    public async Task TrainerBerufen_EineBereitsBerufeneMeldetDenAltenFehler()
    {
        var a = await AufsetzenAsync();

        var ergebnis = await a.Dienst.AssignTrainerAsync(a.Verwaltung, isAdmin: false, a.Verein.Id, new AssignClubTrainerRequest("verwaltung@example.org"));

        Assert.Equal("Dieser Benutzer ist bereits Trainer dieses Vereins.", Assert.Single(ergebnis.Errors));
    }

    // ---------------------------------------------------------- Zählungen u. a.

    [Fact]
    public async Task OffeneEinladungenZaehlenNichtAlsOffeneAnfragenDerTrainerinnen()
    {
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);

        var zahlen = await new TrainerOpenCountsService(a.Db).GetAsync(a.Verwaltung);

        Assert.Equal(0, zahlen.Value!.ClubJoinRequests);
    }

    [Fact]
    public async Task AustrittUndEntfernenKennenKeineEinladungen()
    {
        var a = await AufsetzenAsync();
        await EinladenAsync(a, a.Verwaltung);

        Assert.False((await a.Dienst.LeaveClubAsync(a.Person, a.Verein.Id)).Succeeded);
        Assert.True((await a.Dienst.RemoveMemberAsync(a.Verein.Id, a.Person)).IsNotFound);
        Assert.Equal(ClubMembershipStatus.Invited, (await ZeileAsync(a, a.Person)).Status);
    }
}
