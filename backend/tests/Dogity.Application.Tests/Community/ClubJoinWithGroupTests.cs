using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Vereinsbeitritt mit einer Freigabe: Die Trainer:in nimmt die Anfrage an
/// und lädt in dieselbe Hand in eine Gruppe ein.
///
/// Der Grundsatz dahinter: Gruppen nehmen niemanden ohne dessen Zustimmung auf.
/// Die Vereinsanfrage ist keine Zustimmung zur Gruppe - also entsteht dort eine
/// EINLADUNG, und Mitglied wird erst, wer sie annimmt.
/// </summary>
public class ClubJoinWithGroupTests
{
    private sealed record Umgebung(
        ApplicationDbContext Db,
        FakeNotificationService Notifications,
        ClubService Vereine,
        GroupService Gruppen,
        Club Verein,
        Group Gruppe,
        Guid Trainer,
        Guid Anfragender);

    private static async Task<Umgebung> AufbauenAsync()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var notifications = new FakeNotificationService();
        var vereine = new ClubService(db, lookup, notifications, new TrainerRoleService(db, lookup));
        var gruppen = new GroupService(db, lookup, new TrainerRoleService(db, lookup), notifications);

        var trainer = Guid.NewGuid();
        var anfragender = Guid.NewGuid();
        lookup.Register(anfragender, "neu@example.com", "Nina", "Neu");

        var verein = new Club { Name = "Testverein" };
        db.Clubs.Add(verein);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = verein.Id, UserId = trainer, Role = ClubRole.Training });
        var gruppe = new Group { ClubId = verein.Id, TrainerId = trainer, Name = "Dienstagsgruppe" };
        db.Groups.Add(gruppe);
        await db.SaveChangesAsync();
        return new Umgebung(db, notifications, vereine, gruppen, verein, gruppe, trainer, anfragender);
    }

    private static async Task<Guid> AnfrageAsync(Umgebung u, Guid? nutzer = null) =>
        (await u.Vereine.RequestJoinAsync(nutzer ?? u.Anfragender, u.Verein.Id)).Value!.Id;

    [Fact]
    public async Task MitGruppe_NimmtVereinAn_LaedtEin_UndMeldetGenauEinmal()
    {
        var u = await AufbauenAsync();
        var anfrage = await AnfrageAsync(u);

        var ergebnis = await u.Vereine.DecideJoinRequestAsync(u.Trainer, u.Verein.Id, anfrage, approve: true, u.Gruppe.Id);

        Assert.True(ergebnis.Succeeded);
        var verein = await u.Db.ClubMemberships.SingleAsync(m => m.Id == anfrage);
        Assert.Equal(ClubMembershipStatus.Approved, verein.Status);

        // Eingeladen, nicht aufgenommen.
        var mitglied = await u.Db.GroupMembers.SingleAsync(m => m.GroupId == u.Gruppe.Id && m.UserId == u.Anfragender);
        Assert.Equal(GroupMemberStatus.Invited, mitglied.Status);
        Assert.Equal("neu@example.com", mitglied.InvitedEmail);

        // Eine Meldung statt zwei - sie nennt beides und führt zur Annahme.
        var meldung = Assert.Single(u.Notifications.Created, n => n.UserId == u.Anfragender);
        Assert.Contains("Testverein", meldung.Message);
        Assert.Contains("Dienstagsgruppe", meldung.Message);
        Assert.Contains("eingeladen", meldung.Message);
        Assert.Equal("/dashboard", meldung.LinkPath);
    }

    [Fact]
    public async Task MitGruppe_EingeladeneIstNochKeinAktivesMitglied()
    {
        var u = await AufbauenAsync();
        var anfrage = await AnfrageAsync(u);
        await u.Vereine.DecideJoinRequestAsync(u.Trainer, u.Verein.Id, anfrage, approve: true, u.Gruppe.Id);

        var detail = await u.Gruppen.GetDetailAsync(u.Trainer, u.Gruppe.Id);

        Assert.DoesNotContain(detail.Value!.Members, m => m.UserId == u.Anfragender);
    }

    [Fact]
    public async Task FremdeGruppe_Scheitert_UndDieMitgliedschaftBleibtOffen()
    {
        var u = await AufbauenAsync();
        // Gruppe eines anderen Vereins, bei dem die Trainer:in ebenfalls Trainer:in ist.
        var anderer = new Club { Name = "Anderer Verein" };
        u.Db.Clubs.Add(anderer);
        u.Db.ClubTrainers.Add(new ClubTrainer { ClubId = anderer.Id, UserId = u.Trainer, Role = ClubRole.Training });
        var fremde = new Group { ClubId = anderer.Id, TrainerId = u.Trainer, Name = "Fremdgruppe" };
        u.Db.Groups.Add(fremde);
        await u.Db.SaveChangesAsync();
        var anfrage = await AnfrageAsync(u);

        var ergebnis = await u.Vereine.DecideJoinRequestAsync(u.Trainer, u.Verein.Id, anfrage, approve: true, fremde.Id);

        Assert.False(ergebnis.Succeeded);
        Assert.Equal(ClubMembershipStatus.Pending, (await u.Db.ClubMemberships.SingleAsync(m => m.Id == anfrage)).Status);
        Assert.Empty(u.Db.GroupMembers);
        Assert.DoesNotContain(u.Notifications.Created, n => n.UserId == u.Anfragender);
    }

    [Fact]
    public async Task NichtVerwaltbareGruppe_Scheitert_UndDieMitgliedschaftBleibtOffen()
    {
        var u = await AufbauenAsync();
        // Gruppe eines Vereins, mit dem die Trainer:in nichts zu tun hat.
        var anderer = new Club { Name = "Anderer Verein" };
        u.Db.Clubs.Add(anderer);
        var fremde = new Group { ClubId = anderer.Id, TrainerId = Guid.NewGuid(), Name = "Fremdgruppe" };
        u.Db.Groups.Add(fremde);
        await u.Db.SaveChangesAsync();
        var anfrage = await AnfrageAsync(u);

        var ergebnis = await u.Vereine.DecideJoinRequestAsync(u.Trainer, u.Verein.Id, anfrage, approve: true, fremde.Id);

        Assert.False(ergebnis.Succeeded);
        Assert.Equal(ClubMembershipStatus.Pending, (await u.Db.ClubMemberships.SingleAsync(m => m.Id == anfrage)).Status);
        Assert.Empty(u.Db.GroupMembers);
    }

    [Fact]
    public async Task OhneGruppe_VerhaeltSichWieBisher()
    {
        var u = await AufbauenAsync();
        var anfrage = await AnfrageAsync(u);

        var ergebnis = await u.Vereine.DecideJoinRequestAsync(u.Trainer, u.Verein.Id, anfrage, approve: true);

        Assert.True(ergebnis.Succeeded);
        Assert.Empty(u.Db.GroupMembers);
        var meldung = Assert.Single(u.Notifications.Created, n => n.UserId == u.Anfragender);
        Assert.Equal("Dein Beitritt zu \"Testverein\" wurde angenommen.", meldung.Message);
        Assert.Equal("/clubs", meldung.LinkPath);
    }

    [Fact]
    public async Task Ablehnen_IgnoriertDieGruppe()
    {
        var u = await AufbauenAsync();
        var anfrage = await AnfrageAsync(u);

        var ergebnis = await u.Vereine.DecideJoinRequestAsync(u.Trainer, u.Verein.Id, anfrage, approve: false, u.Gruppe.Id);

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(ClubMembershipStatus.Rejected, (await u.Db.ClubMemberships.SingleAsync(m => m.Id == anfrage)).Status);
        Assert.Empty(u.Db.GroupMembers);
        Assert.Contains("abgelehnt", Assert.Single(u.Notifications.Created, n => n.UserId == u.Anfragender).Message);
    }

    [Fact]
    public async Task HatteSelbstSchonDieGruppeAngefragt_IstGleichAktiv()
    {
        var u = await AufbauenAsync();
        var anfrage = await AnfrageAsync(u);
        u.Db.GroupMembers.Add(new GroupMember { GroupId = u.Gruppe.Id, UserId = u.Anfragender, Status = GroupMemberStatus.Pending });
        await u.Db.SaveChangesAsync();

        var ergebnis = await u.Vereine.DecideJoinRequestAsync(u.Trainer, u.Verein.Id, anfrage, approve: true, u.Gruppe.Id);

        Assert.True(ergebnis.Succeeded);
        var mitglied = await u.Db.GroupMembers.SingleAsync(m => m.GroupId == u.Gruppe.Id && m.UserId == u.Anfragender);
        Assert.Equal(GroupMemberStatus.Active, mitglied.Status);
        Assert.Contains("Mitglied", Assert.Single(u.Notifications.Created, n => n.UserId == u.Anfragender).Message);
    }

    [Fact]
    public async Task FrueherEntfernteZeile_WirdWiederEingeladen()
    {
        var u = await AufbauenAsync();
        var anfrage = await AnfrageAsync(u);
        u.Db.GroupMembers.Add(new GroupMember
        {
            GroupId = u.Gruppe.Id, UserId = u.Anfragender, Status = GroupMemberStatus.Active, DeletedAt = DateTimeOffset.UtcNow,
        });
        await u.Db.SaveChangesAsync();

        var ergebnis = await u.Vereine.DecideJoinRequestAsync(u.Trainer, u.Verein.Id, anfrage, approve: true, u.Gruppe.Id);

        Assert.True(ergebnis.Succeeded);
        var mitglied = await u.Db.GroupMembers.SingleAsync(m => m.GroupId == u.Gruppe.Id && m.UserId == u.Anfragender);
        Assert.Equal(GroupMemberStatus.Invited, mitglied.Status);
    }

    [Fact]
    public async Task NichtTrainer_KannWederAnnehmenNochEinladen()
    {
        var u = await AufbauenAsync();
        var anfrage = await AnfrageAsync(u);

        var ergebnis = await u.Vereine.DecideJoinRequestAsync(Guid.NewGuid(), u.Verein.Id, anfrage, approve: true, u.Gruppe.Id);

        Assert.False(ergebnis.Succeeded);
        Assert.Equal(ClubMembershipStatus.Pending, (await u.Db.ClubMemberships.SingleAsync(m => m.Id == anfrage)).Status);
        Assert.Empty(u.Db.GroupMembers);
    }

    [Fact]
    public async Task Vereinsliste_KennzeichnetDieEigenenVereineAlsTrainerIn()
    {
        var u = await AufbauenAsync();
        var anderer = new Club { Name = "Anderer Verein" };
        u.Db.Clubs.Add(anderer);
        await u.Db.SaveChangesAsync();

        var liste = (await u.Vereine.GetBrowsableClubsAsync(u.Trainer)).Value!;

        Assert.True(liste.Single(c => c.Id == u.Verein.Id).IsTrainer);
        Assert.False(liste.Single(c => c.Id == anderer.Id).IsTrainer);
        // Wer nicht Trainer:in ist, bekommt es nirgends gekennzeichnet.
        Assert.All((await u.Vereine.GetBrowsableClubsAsync(u.Anfragender)).Value!, c => Assert.False(c.IsTrainer));
    }
}
