using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Community;
using Dogity.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Onboarding;

/// <inheritdoc />
public class OnboardingService(IApplicationDbContext db, IUserLookupService users) : IOnboardingService
{
    public async Task<Result<OnboardingStatusDto>> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        // Der erste Hund trägt die Verweise der beiden folgenden Schritte.
        // Ältester zuerst, damit der Erststart nicht auf einen Hund zeigt, den
        // jemand später nebenbei angelegt hat.
        var ersterHund = await db.DogOwners
            .Where(o => o.UserId == userId)
            .OrderBy(o => o.CreatedAt)
            .Select(o => new { o.DogId, o.Dog!.Name })
            .FirstOrDefaultAsync(ct);

        var hundeIds = await db.DogOwners
            .Where(o => o.UserId == userId)
            .Select(o => o.DogId)
            .ToListAsync(ct);

        var hatZiel = hundeIds.Count > 0
            && await db.Goals.AnyAsync(g => hundeIds.Contains(g.DogId) && g.Status == GoalStatus.Active, ct);

        var hatTraining = hundeIds.Count > 0
            && await db.TrainingSessions.AnyAsync(s => hundeIds.Contains(s.DogId), ct);

        var vereine = await db.ClubMemberships
            .Where(m => m.UserId == userId)
            .Select(m => m.Status)
            .ToListAsync(ct);

        var gruppen = await db.GroupMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.Status)
            .ToListAsync(ct);

        // Trainer:innen zählen mit. Beim Zuweisen entsteht KEINE
        // Mitgliedschaftszeile - wer nur auf ClubMemberships/GroupMembers
        // schaut, fordert eine Vereinstrainerin auf, dem Verein beizutreten,
        // den sie leitet. Genau das ist passiert.
        var hatVerein = await db.BelongsToAnyClubAsync(userId, ct);
        var hatGruppe = await db.BelongsToAnyGroupAsync(userId, ct);

        // Eine offene Anfrage ist kein offener Schritt: Der Nutzer hat getan,
        // was er tun konnte, und wartet auf die Freigabe durch den Verein.
        var vereinAngefragt = !hatVerein && vereine.Contains(ClubMembershipStatus.Pending);
        var gruppeAngefragt = !hatGruppe && gruppen.Contains(GroupMemberStatus.Pending);

        var weggeklickt = await users.IsOnboardingDismissedAsync(userId, ct);

        // Angekommen ist, wer einen Hund hat und EINEN der beiden Wege gegangen
        // ist - beide zu verlangen wäre falsch, sie sind Alternativen.
        var fertig = ersterHund is not null && (hatTraining || hatGruppe);

        return Result<OnboardingStatusDto>.Success(new OnboardingStatusDto(
            ersterHund is not null,
            ersterHund?.DogId,
            ersterHund?.Name,
            hatZiel,
            hatTraining,
            hatVerein,
            vereinAngefragt,
            hatGruppe,
            gruppeAngefragt,
            weggeklickt,
            fertig));
    }

    public async Task<Result> DismissAsync(Guid userId, CancellationToken ct = default)
    {
        await users.DismissOnboardingAsync(userId, ct);
        return Result.Success();
    }
}
