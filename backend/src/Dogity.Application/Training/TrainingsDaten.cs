using Dogity.Application.Abstractions;
using Dogity.Application.Community;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Training;

/// <summary>
/// Was <see cref="Trainingsrhythmus"/> braucht, für mehrere Hunde und Personen in wenigen Abfragen geladen
/// (Merkzettel-Liste und Erinnerungsdienst). Gruppentermine gelten je Person: Sie trainiert in ihren Gruppen,
/// der Hund hat keine.
/// </summary>
public sealed class TrainingsDaten
{
    /// <summary>
    /// So weit voraus werden Gruppentermine geladen: bis zum Ende der Folgewoche plus die 14 Tage, in denen
    /// <see cref="Trainingsrhythmus"/> einen Gruppentermin noch gelten lässt.
    /// </summary>
    private const int GruppenLadeTage = 28;

    private readonly Dictionary<Guid, List<TagebuchTraining>> _tagebuch = [];
    private readonly Dictionary<Guid, GespeicherterRhythmus> _rhythmen = [];
    private readonly Dictionary<Guid, List<DateTimeOffset>> _gruppentermine = [];

    public IReadOnlyList<TagebuchTraining> Tagebuch(Guid hundId) =>
        _tagebuch.TryGetValue(hundId, out var liste) ? liste : [];

    public GespeicherterRhythmus? Rhythmus(Guid hundId) =>
        _rhythmen.TryGetValue(hundId, out var rhythmus) ? rhythmus : null;

    public IReadOnlyList<DateTimeOffset> Gruppentermine(Guid userId) =>
        _gruppentermine.TryGetValue(userId, out var liste) ? liste : [];

    public static async Task<TrainingsDaten> LadenAsync(
        IApplicationDbContext db, IReadOnlyCollection<Guid> hundeIds, IReadOnlyCollection<Guid> userIds, DateOnly heute,
        CancellationToken ct = default)
    {
        var daten = new TrainingsDaten();

        var seit = heute.AddDays(-7 * Trainingsrhythmus.TagebuchWochen);
        var trainings = await db.TrainingSessions
            .Where(s => hundeIds.Contains(s.DogId) && s.Date >= seit && s.Date <= heute)
            .Select(s => new { s.DogId, s.Date, s.StartTime })
            .AsNoTracking()
            .ToListAsync(ct);
        foreach (var t in trainings)
        {
            if (!daten._tagebuch.TryGetValue(t.DogId, out var liste)) daten._tagebuch[t.DogId] = liste = [];
            liste.Add(new TagebuchTraining(t.Date, t.StartTime));
        }

        var rhythmen = await db.DogTrainingRhythms
            .Where(r => hundeIds.Contains(r.DogId))
            .Select(r => new { r.DogId, r.Days, r.Hour })
            .AsNoTracking()
            .ToListAsync(ct);
        foreach (var r in rhythmen) daten._rhythmen[r.DogId] = new GespeicherterRhythmus(r.Days, r.Hour);

        // Wie die Terminerinnerung: geplante Termine aktiver Mitglieder, Termine gelöschter Gruppen zählen nicht.
        var von = Vereinszeit.TagesBeginn(heute);
        var bis = Vereinszeit.TagesBeginn(heute.AddDays(GruppenLadeTage));
        var mitglieder = await db.GroupMembers
            .Where(m => userIds.Contains(m.UserId) && m.Status == GroupMemberStatus.Active)
            .Select(m => new { m.GroupId, m.UserId })
            .AsNoTracking()
            .ToListAsync(ct);
        if (mitglieder.Count == 0) return daten;

        var gruppenIds = mitglieder.Select(m => m.GroupId).Distinct().ToList();
        var termine = await db.GroupTrainingSessions
            .Where(s => gruppenIds.Contains(s.GroupId) && db.Groups.Any(g => g.Id == s.GroupId)
                && s.Status == GroupTrainingSessionStatus.Planned && s.StartsAt >= von && s.StartsAt < bis)
            .Select(s => new { s.GroupId, s.StartsAt })
            .AsNoTracking()
            .ToListAsync(ct);
        foreach (var m in mitglieder)
        {
            var eigene = termine.Where(t => t.GroupId == m.GroupId).Select(t => t.StartsAt).ToList();
            if (eigene.Count == 0) continue;
            if (!daten._gruppentermine.TryGetValue(m.UserId, out var liste)) daten._gruppentermine[m.UserId] = liste = [];
            liste.AddRange(eigene);
        }
        return daten;
    }
}
