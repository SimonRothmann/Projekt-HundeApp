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
        //
        // Die Abfragen laufen bewusst NACHEINANDER, aber in möglichst wenigen
        // Schritten: Ein DbContext verträgt keine parallelen Abfragen. Vorher
        // waren es zwölf Roundtrips für eine Antwort von rund 270 Byte - und
        // das bei jedem Aufbau des Dashboards. Zusammengefasst bleiben fünf.
        var hunde = await db.DogOwners
            .Where(o => o.UserId == userId)
            .OrderBy(o => o.CreatedAt)
            .Select(o => new { o.DogId, o.Dog!.Name })
            .ToListAsync(ct);

        var ersterHund = hunde.FirstOrDefault();
        var hundeIds = hunde.Select(h => h.DogId).ToList();

        // Ziel und Training in EINER Abfrage: zwei Existenzprüfungen über
        // verschiedene Tabellen, die sich zu einer einzigen Zeile verrechnen
        // lassen. Ohne Hunde ist beides ohnehin falsch - dann entfällt die
        // Abfrage ganz.
        var hatZiel = false;
        var hatTraining = false;
        if (hundeIds.Count > 0)
        {
            var stand = await db.Dogs
                .Where(d => hundeIds.Contains(d.Id))
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Ziel = db.Goals.Any(z => hundeIds.Contains(z.DogId) && z.Status == GoalStatus.Active),
                    Training = db.TrainingSessions.Any(t => hundeIds.Contains(t.DogId)),
                })
                .FirstOrDefaultAsync(ct);

            hatZiel = stand?.Ziel ?? false;
            hatTraining = stand?.Training ?? false;
        }

        // Vereins- und Gruppenzugehörigkeit in je EINER Abfrage.
        //
        // Trainer:innen zählen mit. Beim Zuweisen entsteht KEINE
        // Mitgliedschaftszeile - wer nur auf ClubMemberships/GroupMembers
        // schaut, fordert eine Vereinstrainerin auf, dem Verein beizutreten,
        // den sie leitet. Genau das ist passiert.
        var verein = await db.ClubMemberships
            .Where(m => m.UserId == userId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Mitglied = g.Any(m => m.Status == ClubMembershipStatus.Approved),
                Angefragt = g.Any(m => m.Status == ClubMembershipStatus.Pending),
            })
            .FirstOrDefaultAsync(ct);

        var hatVerein = (verein?.Mitglied ?? false)
            || await db.ClubTrainers.AnyAsync(t => t.UserId == userId, ct);

        var gruppe = await db.GroupMembers
            .Where(m => m.UserId == userId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Mitglied = g.Any(m => m.Status == GroupMemberStatus.Active),
                Angefragt = g.Any(m => m.Status == GroupMemberStatus.Pending),
            })
            .FirstOrDefaultAsync(ct);

        var hatGruppe = (gruppe?.Mitglied ?? false)
            || await db.Groups.AnyAsync(g => g.TrainerId == userId, ct)
            || await db.GroupTrainers.AnyAsync(t => t.UserId == userId, ct);

        // Eine offene Anfrage ist kein offener Schritt: Der Nutzer hat getan,
        // was er tun konnte, und wartet auf die Freigabe durch den Verein.
        var vereinAngefragt = !hatVerein && (verein?.Angefragt ?? false);
        var gruppeAngefragt = !hatGruppe && (gruppe?.Angefragt ?? false);

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
