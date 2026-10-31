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
        // Gruppenanfragen entscheidet, wer Trainer:in der Gruppe ist (eine
        // Definition: GruppenIdsDerTrainerin, dieselbe wie bei
        // GetManageableGroupAsync). Vereinsanfragen entscheidet, wer
        // Mitglieder verwaltet - ein eigener Kreis.
        var mitgliederClubIds = await db.ClubTrainers
            .Where(t => t.UserId == userId)
            .MitRolle(ClubRollen.Mitglieder)
            .Select(t => t.ClubId)
            .ToListAsync(ct);

        var gruppenAnfragen = await db.GroupMembers
            .Where(m => m.Status == GroupMemberStatus.Pending
                && db.GruppenIdsDerTrainerin(userId).Contains(m.GroupId))
            .GroupBy(m => m.GroupId)
            .Select(g => new GroupOpenCountDto(g.Key, g.Count()))
            .AsNoTracking()
            .ToListAsync(ct);

        var vereinsAnfragen = mitgliederClubIds.Count == 0
            ? 0
            : await db.ClubMemberships.CountAsync(m => mitgliederClubIds.Contains(m.ClubId) && m.Status == ClubMembershipStatus.Pending, ct);

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
