using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Die Zahl der Anmeldungen am Termin (für "Nächster Termin" auf der Trainer-
/// Übersicht): nur für Personen, die die Gruppe verwalten dürfen - dieselbe
/// Reichweite wie die Anmeldeliste -, für alle anderen 0.
/// </summary>
public class GroupTrainingSessionRegistrationCountTests
{
    private static GroupTrainingScheduleService Dienst(AnmeldeAufbau a) =>
        new(a.Db, new FakeUserLookupService(), new FakeNotificationService());

    private static async Task<GroupTrainingSession> TerminAnlegenAsync(AnmeldeAufbau a, Group gruppe)
    {
        var termin = new GroupTrainingSession
        {
            ClubId = a.Verein.Id,
            GroupId = gruppe.Id,
            StartsAt = DateTimeOffset.UtcNow.Date.AddDays(3).AddHours(12),
        };
        a.Db.GroupTrainingSessions.Add(termin);
        await a.Db.SaveChangesAsync();
        return termin;
    }

    private static async Task AnmeldungenAnlegenAsync(AnmeldeAufbau a, Group gruppe, int anzahl)
    {
        for (var i = 0; i < anzahl; i++)
            a.Db.GroupRegistrations.Add(new GroupRegistration { GroupId = gruppe.Id, FirstName = $"Person{i}", DogName = $"Hund{i}" });
        await a.Db.SaveChangesAsync();
    }

    private static DateOnly Heute => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Vereinstrainerin_Sieht_Die_Zahl_Im_Vereinskalender()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        await TerminAnlegenAsync(a, a.Gruppe);
        await AnmeldungenAnlegenAsync(a, a.Gruppe, 3);

        var liste = await Dienst(a).GetClubScheduleAsync(a.VereinsTrainerin, a.Verein.Id, Heute, null, null, null, false);

        Assert.Equal(3, Assert.Single(liste.Value!).RegistrationCount);
    }

    [Fact]
    public async Task Leitung_Und_Aktive_CoTrainerin_Sehen_Die_Zahl_Im_Terminfeed()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        await TerminAnlegenAsync(a, a.Gruppe);
        await AnmeldungenAnlegenAsync(a, a.Gruppe, 2);
        foreach (var nutzer in new[] { a.Leitung, a.CoTrainerin })
            a.Db.GroupMembers.Add(new GroupMember { GroupId = a.Gruppe.Id, UserId = nutzer, Status = GroupMemberStatus.Active });
        await a.Db.SaveChangesAsync();

        foreach (var nutzer in new[] { a.Leitung, a.CoTrainerin })
        {
            var liste = await Dienst(a).GetMemberScheduleAsync(nutzer, Heute);
            Assert.Equal(2, Assert.Single(liste.Value!).RegistrationCount);
        }
    }

    [Fact]
    public async Task Mitglied_Und_Eingeladene_CoTrainerin_Bekommen_Null()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        await TerminAnlegenAsync(a, a.Gruppe);
        await AnmeldungenAnlegenAsync(a, a.Gruppe, 4);
        var mitglied = Guid.NewGuid();
        var eingeladen = Guid.NewGuid();
        a.Db.GroupMembers.AddRange(
            new GroupMember { GroupId = a.Gruppe.Id, UserId = mitglied, Status = GroupMemberStatus.Active },
            new GroupMember { GroupId = a.Gruppe.Id, UserId = eingeladen, Status = GroupMemberStatus.Active });
        // Eine offene Einladung gibt keine Verwaltungsrechte (globaler Filter).
        a.Db.GroupTrainers.Add(new GroupTrainer { GroupId = a.Gruppe.Id, UserId = eingeladen, Status = GroupTrainerStatus.Invited });
        await a.Db.SaveChangesAsync();

        foreach (var nutzer in new[] { mitglied, eingeladen })
        {
            var liste = await Dienst(a).GetMemberScheduleAsync(nutzer, Heute);
            Assert.Equal(0, Assert.Single(liste.Value!).RegistrationCount);
        }
    }

    [Fact]
    public async Task Gezaehlt_Wird_Je_Gruppe_Und_Nur_Wo_Man_Verwaltet()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        // Zweite Gruppe, die die Leitung nicht verwaltet, in der sie aber Mitglied ist.
        var fremdeLeitung = Guid.NewGuid();
        var andere = new Group { ClubId = null, TrainerId = fremdeLeitung, Name = "Andere Gruppe" };
        a.Db.Groups.Add(andere);
        a.Db.GroupMembers.AddRange(
            new GroupMember { GroupId = a.Gruppe.Id, UserId = a.Leitung, Status = GroupMemberStatus.Active },
            new GroupMember { GroupId = andere.Id, UserId = a.Leitung, Status = GroupMemberStatus.Active });
        await a.Db.SaveChangesAsync();
        var eigener = await TerminAnlegenAsync(a, a.Gruppe);
        var fremder = await TerminAnlegenAsync(a, andere);
        await AnmeldungenAnlegenAsync(a, a.Gruppe, 2);
        await AnmeldungenAnlegenAsync(a, andere, 5);

        var liste = (await Dienst(a).GetMemberScheduleAsync(a.Leitung, Heute)).Value!;

        Assert.Equal(2, liste.Single(t => t.Id == eigener.Id).RegistrationCount);
        Assert.Equal(0, liste.Single(t => t.Id == fremder.Id).RegistrationCount);
    }

    [Fact]
    public async Task Gruppe_Ohne_Anmeldungen_Zeigt_Null()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        await TerminAnlegenAsync(a, a.Gruppe);

        var liste = await Dienst(a).GetClubScheduleAsync(a.VereinsTrainerin, a.Verein.Id, Heute, null, null, null, false);

        Assert.Equal(0, Assert.Single(liste.Value!).RegistrationCount);
    }
}
