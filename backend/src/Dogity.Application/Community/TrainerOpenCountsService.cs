using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

/// <summary>
/// Was bei einer Trainer:in offen ist, als reine Zahlen - für "Zu erledigen"
/// auf der Trainer-Übersicht.
///
/// Die Übersicht lud dafür vorher die Anfragen jeder Gruppe und jedes Vereins
/// einzeln und die komplette Bewertungsliste samt Übungen, nur um zu wissen, ob
/// sich ein Kasten zeigt. Hier sind es drei Zählabfragen, und die Listen
/// laden erst auf den Seiten, auf denen man sie bearbeitet.
/// </summary>
public class TrainerOpenCountsService(IApplicationDbContext db) : ITrainerOpenCountsService
{
    public async Task<Result<TrainerOpenCountsDto>> GetAsync(Guid userId, CancellationToken ct = default)
    {
        // Dieselbe Reichweite wie GroupService.GetMyGroupsAsync und
        // GetManageableGroupAsync: eigene Gruppen, mit-betreute und alle
        // Gruppen der Vereine, in denen man Trainer:in ist. Nur dort darf man
        // Anfragen entscheiden.
        var clubIds = await db.ClubTrainers
            .Where(t => t.UserId == userId)
            .Select(t => t.ClubId)
            .ToListAsync(ct);

        var gruppenAnfragen = await db.GroupMembers
            .Where(m => m.Status == GroupMemberStatus.Pending
                && (m.Group!.TrainerId == userId
                    || m.Group.Trainers.Any(t => t.UserId == userId)
                    || (m.Group.ClubId != null && clubIds.Contains(m.Group.ClubId.Value))))
            .GroupBy(m => m.GroupId)
            .Select(g => new GroupOpenCountDto(g.Key, g.Count()))
            .AsNoTracking()
            .ToListAsync(ct);

        var vereinsAnfragen = clubIds.Count == 0
            ? 0
            : await db.ClubMemberships.CountAsync(m => clubIds.Contains(m.ClubId) && m.Status == ClubMembershipStatus.Pending, ct);

        var betreuteHunde = await db.TrainerAssignments
            .Where(t => t.TrainerId == userId)
            .Select(t => t.DogId)
            .ToListAsync(ct);
        var zuBewerten = betreuteHunde.Count == 0
            ? 0
            : await db.TrainingSessionsToRate(betreuteHunde).CountAsync(ct);

        return Result<TrainerOpenCountsDto>.Success(
            new TrainerOpenCountsDto(gruppenAnfragen.Sum(g => g.JoinRequests), vereinsAnfragen, zuBewerten, gruppenAnfragen));
    }
}
