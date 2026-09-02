using Dogity.Application.Onboarding;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;

namespace Dogity.Application.Tests.Onboarding;

/// <summary>
/// Testet den geführten Erststart - vor allem die Gabelung: Nach dem Hund
/// führen zwei Wege ans Ziel (selbst loslegen oder über den Verein), und EINER
/// genügt. Beide zu verlangen wäre falsch, sie sind Alternativen.
/// </summary>
public class OnboardingServiceTests
{
    private static (OnboardingService Dienst, Guid NutzerId, Aufbau Aufbau) Erstelle()
    {
        var db = InMemoryDbContext.Create();
        return (new OnboardingService(db, new FakeUserLookupService()), Guid.NewGuid(), new Aufbau(db));
    }

    [Fact]
    public async Task OhneHund_StehtNurDerHundAn()
    {
        var (dienst, nutzerId, _) = Erstelle();

        var stand = (await dienst.GetStatusAsync(nutzerId)).Value!;

        Assert.False(stand.HasDog);
        Assert.Null(stand.FirstDogId);
        Assert.False(stand.IsComplete);
    }

    [Fact]
    public async Task MitHund_TraegtDerErsteHundDieVerweise()
    {
        var (dienst, nutzerId, aufbau) = Erstelle();
        var ersterHund = aufbau.Hund(nutzerId, "Bella");
        aufbau.Hund(nutzerId, "Zweithund");
        await aufbau.Speichern();

        var stand = (await dienst.GetStatusAsync(nutzerId)).Value!;

        Assert.True(stand.HasDog);
        Assert.Equal(ersterHund, stand.FirstDogId);
        Assert.Equal("Bella", stand.FirstDogName);
        // Ein Hund allein reicht nicht - es fehlt noch einer der beiden Wege.
        Assert.False(stand.IsComplete);
    }

    [Fact]
    public async Task ErstesTraining_SchliesstDenErststartAb()
    {
        var (dienst, nutzerId, aufbau) = Erstelle();
        var hund = aufbau.Hund(nutzerId, "Bella");
        aufbau.Training(nutzerId, hund);
        await aufbau.Speichern();

        var stand = (await dienst.GetStatusAsync(nutzerId)).Value!;

        Assert.True(stand.HasTraining);
        Assert.True(stand.IsComplete);
    }

    [Fact]
    public async Task Trainingsgruppe_SchliesstDenErststartEbenfallsAb()
    {
        var (dienst, nutzerId, aufbau) = Erstelle();
        aufbau.Hund(nutzerId, "Bella");
        aufbau.Gruppenmitglied(nutzerId, GroupMemberStatus.Active);
        await aufbau.Speichern();

        var stand = (await dienst.GetStatusAsync(nutzerId)).Value!;

        // Der Vereinsweg ist gleichwertig - wer über die Gruppe kommt, braucht
        // kein selbst eingetragenes Training, um "angekommen" zu sein.
        Assert.True(stand.HasGroupMembership);
        Assert.True(stand.IsComplete);
        Assert.False(stand.HasTraining);
    }

    [Fact]
    public async Task OffeneAnfrage_ZaehltNichtAlsMitgliedschaft_AberAlsWartend()
    {
        var (dienst, nutzerId, aufbau) = Erstelle();
        aufbau.Hund(nutzerId, "Bella");
        aufbau.Vereinsmitglied(nutzerId, ClubMembershipStatus.Pending);
        aufbau.Gruppenmitglied(nutzerId, GroupMemberStatus.Pending);
        await aufbau.Speichern();

        var stand = (await dienst.GetStatusAsync(nutzerId)).Value!;

        Assert.False(stand.HasClubMembership);
        Assert.True(stand.HasPendingClubRequest);
        Assert.False(stand.HasGroupMembership);
        Assert.True(stand.HasPendingGroupRequest);
        // Warten ist kein Abschluss: Der Verein muss erst freigeben.
        Assert.False(stand.IsComplete);
    }

    [Fact]
    public async Task FreigegebeneMitgliedschaft_GiltNichtMehrAlsWartend()
    {
        var (dienst, nutzerId, aufbau) = Erstelle();
        aufbau.Hund(nutzerId, "Bella");
        aufbau.Vereinsmitglied(nutzerId, ClubMembershipStatus.Approved);
        await aufbau.Speichern();

        var stand = (await dienst.GetStatusAsync(nutzerId)).Value!;

        Assert.True(stand.HasClubMembership);
        Assert.False(stand.HasPendingClubRequest);
    }

    [Fact]
    public async Task AktivesZiel_WirdErkannt()
    {
        var (dienst, nutzerId, aufbau) = Erstelle();
        var hund = aufbau.Hund(nutzerId, "Bella");
        aufbau.Ziel(hund);
        await aufbau.Speichern();

        var stand = (await dienst.GetStatusAsync(nutzerId)).Value!;

        Assert.True(stand.HasGoal);
        // Ein Ziel allein schließt nicht ab - das erste Training fehlt noch.
        Assert.False(stand.IsComplete);
    }

    [Fact]
    public async Task Wegklicken_BleibtWeggeklickt()
    {
        var (dienst, nutzerId, aufbau) = Erstelle();
        aufbau.Hund(nutzerId, "Bella");
        await aufbau.Speichern();

        Assert.False((await dienst.GetStatusAsync(nutzerId)).Value!.IsDismissed);
        await dienst.DismissAsync(nutzerId);

        Assert.True((await dienst.GetStatusAsync(nutzerId)).Value!.IsDismissed);
    }

    [Fact]
    public async Task FremdeHunde_ZaehlenNicht()
    {
        var (dienst, nutzerId, aufbau) = Erstelle();
        aufbau.Hund(Guid.NewGuid(), "Fremder");
        await aufbau.Speichern();

        Assert.False((await dienst.GetStatusAsync(nutzerId)).Value!.HasDog);
    }
}
