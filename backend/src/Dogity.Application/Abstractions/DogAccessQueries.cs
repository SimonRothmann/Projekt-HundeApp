using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Abstractions;

/// <summary>
/// Zentrale Zugriffsregel für Hunde - die einzige Stelle, an der sie steht.
/// Zwei Stufen:
/// <list type="bullet">
/// <item><b>Besitzer:in</b> (<see cref="EigeneHundeIds"/>, <see cref="IstBesitzerAsync"/>):
/// aktive Zeile in <see cref="Domain.Dogs.DogOwner"/> (offene Einladungen blendet
/// der globale Filter aus), auch Mitbesitzer:innen.</item>
/// <item><b>Zugriff</b> (<see cref="ZugaenglicheHundeIds"/>, <see cref="HasDogAccessAsync"/>):
/// Besitzer:in ODER betreuende Trainer:in über eine
/// <see cref="Domain.Community.TrainerAssignment"/> (siehe DATABASE.md
/// "trainer_assignments") - damit Trainer:innen individuelle Trainingspläne
/// für betreute Hunde anlegen können.</item>
/// </list>
/// Beide gelten nur für Hunde, die es noch gibt: Zu einem weich gelöschten Hund
/// hat niemand Zugriff (Query-Filter von Dog, deshalb läuft die Abfrage über
/// <c>db.Dogs</c>) - es gibt dort keine leere Liste, sondern "nicht gefunden".
/// Die <c>...Ids</c>-Fassungen sind <see cref="IQueryable{T}"/> und lassen sich in
/// andere EF-Abfragen einbetten
/// (<c>.Where(s =&gt; db.ZugaenglicheHundeIds(userId).Contains(s.DogId))</c>);
/// die async-Fassungen sind nur die Kurzform für einen einzelnen Hund.
/// </summary>
public static class DogAccessQueries
{
    /// <summary>Die Hunde, bei denen <paramref name="userId"/> Besitzer:in oder Mitbesitzer:in ist.</summary>
    public static IQueryable<Guid> EigeneHundeIds(this IApplicationDbContext db, Guid userId) =>
        db.Dogs
            .Where(d => db.DogOwners.Any(o => o.DogId == d.Id && o.UserId == userId))
            .Select(d => d.Id);

    /// <summary>Die Hunde, auf die <paramref name="userId"/> Zugriff hat: eigene (Besitz) und betreute.</summary>
    public static IQueryable<Guid> ZugaenglicheHundeIds(this IApplicationDbContext db, Guid userId) =>
        db.EigeneHundeIds(userId).Union(
            db.Dogs
                .Where(d => db.TrainerAssignments.Any(t => t.DogId == d.Id && t.TrainerId == userId))
                .Select(d => d.Id));

    /// <summary>
    /// Die Besitzer:innen (und Mitbesitzer:innen) eines Hundes, dessen Zugriff
    /// schon geprüft ist - die Umkehrung von <see cref="EigeneHundeIds"/>, für
    /// Abfragen, die je Zeile nach der Person fragen.
    /// </summary>
    public static IQueryable<Guid> BesitzerIds(this IApplicationDbContext db, Guid dogId) =>
        db.DogOwners.Where(o => o.DogId == dogId).Select(o => o.UserId);

    public static Task<bool> HasDogAccessAsync(this IApplicationDbContext db, Guid userId, Guid dogId, CancellationToken ct = default) =>
        db.ZugaenglicheHundeIds(userId).AnyAsync(id => id == dogId, ct);

    public static Task<bool> IstBesitzerAsync(this IApplicationDbContext db, Guid userId, Guid dogId, CancellationToken ct = default) =>
        db.EigeneHundeIds(userId).AnyAsync(id => id == dogId, ct);
}
