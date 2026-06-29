using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Abstractions;

/// <summary>
/// Zentrale Zugriffsprüfung für Hunde: Zugriff hat, wer Besitzer ist
/// (<see cref="Domain.Dogs.DogOwner"/>) ODER als Trainer über eine
/// <see cref="Domain.Community.TrainerAssignment"/> betreut
/// (siehe DATABASE.md "trainer_assignments"). Wird von Dog-, Training-
/// und Goal-Services gemeinsam genutzt, damit Trainer individuelle
/// Trainingspläne für betreute Hunde anlegen können.
/// </summary>
public static class DogAccessQueries
{
    public static async Task<bool> HasDogAccessAsync(this IApplicationDbContext db, Guid userId, Guid dogId, CancellationToken ct = default)
    {
        var isOwner = await db.DogOwners.AnyAsync(o => o.DogId == dogId && o.UserId == userId, ct);
        if (isOwner) return true;

        return await db.TrainerAssignments.AnyAsync(t => t.DogId == dogId && t.TrainerId == userId, ct);
    }
}
