using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Kalendertage zählen in der Vereinszeit, nicht in UTC: Ein Termin um 00:30 Uhr
/// am Samstag liegt in UTC noch am Freitag und gehört trotzdem auf den Samstag -
/// in den Terminlisten, bei der Anwesenheit und in den Tagen, die zum Abhaken
/// angeboten werden.
/// </summary>
public class TerminlistenVereinszeitTests
{
    /// <summary>Ein Tag in der jüngsten Vergangenheit: Anwesenheit lässt sich nur für vergangene Tage nachtragen und wird für die letzten Wochen angeboten.</summary>
    private static readonly DateOnly Tag = Vereinszeit.Heute().AddDays(-3);

    private static async Task<(AnmeldeAufbau Aufbau, GroupTrainingScheduleService Termine, Guid TerminId)> AufbauenAsync()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var termine = new GroupTrainingScheduleService(a.Db, new FakeUserLookupService(), a.Meldungen);
        // 00:30 Uhr Ortszeit - in UTC der Vortag.
        var termin = new GroupTrainingSession
        {
            ClubId = a.Verein.Id, GroupId = a.Gruppe.Id, Category = GroupTrainingCategory.Puppy,
            StartsAt = Vereinszeit.TagesBeginn(Tag).AddMinutes(30), DurationMinutes = 60,
        };
        a.Db.GroupTrainingSessions.Add(termin);
        await a.Db.SaveChangesAsync();
        return (a, termine, termin.Id);
    }

    private static async Task<List<Guid>> VereinslisteAsync(AnmeldeAufbau a, GroupTrainingScheduleService termine, DateOnly von, DateOnly? bis) =>
        (await termine.GetClubScheduleAsync(a.VereinsTrainerin, a.Verein.Id, von, bis, null, null, false)).Value!.Select(s => s.Id).ToList();

    [Fact]
    public async Task Vereinsliste_EinTerminKurzNachMitternachtGehoertAufDenOrtsTag()
    {
        var (a, termine, id) = await AufbauenAsync();

        Assert.Equal([id], await VereinslisteAsync(a, termine, Tag, Tag));
        Assert.Equal([id], await VereinslisteAsync(a, termine, Tag, null));
        // Ab dem Tag danach ist er nicht mehr dabei ...
        Assert.Empty(await VereinslisteAsync(a, termine, Tag.AddDays(1), null));
        // ... und bis zum Tag davor noch nicht.
        Assert.Empty(await VereinslisteAsync(a, termine, Tag.AddDays(-5), Tag.AddDays(-1)));
    }

    [Fact]
    public async Task Mitgliederliste_EinTerminKurzNachMitternachtGehoertAufDenOrtsTag()
    {
        var (a, termine, id) = await AufbauenAsync();
        var mitglied = Guid.NewGuid();
        a.Db.GroupMembers.Add(new GroupMember { GroupId = a.Gruppe.Id, UserId = mitglied, Status = GroupMemberStatus.Active });
        await a.Db.SaveChangesAsync();

        Assert.Equal([id], (await termine.GetMemberScheduleAsync(mitglied, Tag)).Value!.Select(s => s.Id));
        Assert.Empty((await termine.GetMemberScheduleAsync(mitglied, Tag.AddDays(1))).Value!);
    }

    [Fact]
    public async Task Anwesenheit_DerTerminKurzNachMitternachtWirdAmOrtsTagGefunden()
    {
        var (a, _, id) = await AufbauenAsync();
        var anmeldung = await a.AnmeldungAsync("Bella", "0160 111111", DateTimeOffset.UtcNow);

        Assert.True((await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, anmeldung.Id, Tag, true)).Succeeded);

        Assert.Equal(id, (await a.Db.GroupRegistrationAttendances.SingleAsync()).GroupTrainingSessionId);
        var tage = (await a.Dienst.GetAttendanceDaysAsync(a.Leitung, a.Gruppe.Id)).Value!;
        Assert.Equal(Tag, Assert.Single(tage).Date);
    }

    [Fact]
    public async Task Anwesenheit_AmTagDavorGehoertDerTerminNichtDazu()
    {
        var (a, _, _) = await AufbauenAsync();
        var anmeldung = await a.AnmeldungAsync("Bella", "0160 111111", DateTimeOffset.UtcNow);

        await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, anmeldung.Id, Tag.AddDays(-1), true);

        Assert.Null((await a.Db.GroupRegistrationAttendances.SingleAsync()).GroupTrainingSessionId);
    }
}
