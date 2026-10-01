using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Notifications;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

public class GroupRegistrationFormService(IApplicationDbContext db, INotificationService notifications) : IGroupRegistrationFormService
{
    // Eine Meldung für jeden Grund, warum ein Code nicht zieht: unbekannt,
    // abgeschaltet, ersetzt, Gruppe gelöscht, falsche Form.
    private const string CodeUngueltig = "Diese Anmeldung ist geschlossen. Frag im Verein nach.";

    public async Task<Result<GroupRegistrationFormDto>> GetFormAsync(string code, CancellationToken ct = default)
    {
        var info = await FindeGruppeAsync(code, ct);
        return info is null
            ? Result<GroupRegistrationFormDto>.NotFound(CodeUngueltig)
            : Result<GroupRegistrationFormDto>.Success(new GroupRegistrationFormDto(info.ClubName, info.GroupName));
    }

    public async Task<Result> SubmitAsync(string code, PublicRegistrationRequest request, CancellationToken ct = default)
    {
        var info = await FindeGruppeAsync(code, ct);
        if (info is null) return Result.NotFound(CodeUngueltig);

        // Köder-Feld: Menschen sehen es nicht. Ein Roboter, der alle Felder
        // füllt, bekommt die Erfolgsantwort - und es wird nichts gespeichert.
        if (!string.IsNullOrWhiteSpace(request.Website)) return Result.Success();

        if (!request.Consent)
            return Result.Failure("Bitte stimme zu, dass der Verein deine Angaben zur Organisation der Gruppe speichert.");

        var heute = Vereinszeit.Heute();
        var (eingabe, fehler) = GroupRegistrationRules.Pruefen(
            request.FirstName, request.LastName, request.DogName, request.DogBreed,
            request.DogBirthDate, request.Phone, notiz: null, heute, referenztag: heute);
        if (eingabe is null) return Result.Failure(fehler!);

        var vorhandene = await db.GroupRegistrations
            .Where(r => r.GroupId == info.GroupId)
            .Select(r => new { r.Phone, r.DogName, r.RegisteredAt })
            .AsNoTracking()
            .ToListAsync(ct);

        if (vorhandene.Count >= GroupRegistrationRules.MaxProGruppe)
            return Result.Failure("Die Anmeldung ist gerade nicht möglich. Bitte melde dich im Verein.");

        var jetzt = DateTimeOffset.UtcNow;
        var neu = GroupRegistrationRules.SchluesselVon(eingabe.Phone, eingabe.DogName, jetzt);
        var schluessel = vorhandene.Select(v => GroupRegistrationRules.SchluesselVon(v.Phone, v.DogName, v.RegisteredAt)).ToList();
        // Doppelt abgeschickt (Doppeltipp, zweites Gerät, Seite neu geladen): Es
        // bleibt bei einer Anmeldung, die Antwort ist trotzdem ein Erfolg.
        if (GroupRegistrationRules.IstDoppelt(schluessel, neu, jetzt - GroupRegistrationRules.DoppeltFrist))
            return Result.Success();

        db.GroupRegistrations.Add(new GroupRegistration
        {
            GroupId = info.GroupId,
            FirstName = eingabe.FirstName,
            LastName = eingabe.LastName,
            DogName = eingabe.DogName,
            DogBreed = eingabe.DogBreed,
            DogBirthDate = eingabe.DogBirthDate,
            Phone = eingabe.Phone,
            Source = GroupRegistrationSource.Form,
            RegisteredAt = jetzt,
        });
        await db.SaveChangesAsync(ct);

        // Die Trainer:innen der Gruppe erfahren es in der App - die Anmeldung
        // kommt von einem Menschen ohne Konto, sonst bemerkte sie niemand.
        var text = $"Neue Anmeldung für {info.GroupName}: {eingabe.DogName} ({eingabe.DogBreed}).";
        foreach (var id in await TrainerIdsAsync(info.GroupId, info.LeadId, ct))
            await notifications.CreateAsync(id, text, $"/trainer/{info.GroupId}", ct);

        return Result.Success();
    }

    private record GruppeInfo(Guid GroupId, string GroupName, string? ClubName, Guid LeadId);

    private async Task<GruppeInfo?> FindeGruppeAsync(string code, CancellationToken ct)
    {
        // Vor der Abfrage: Was nie ein Code war, braucht keinen Datenbankzugriff.
        if (!ClubInviteCode.IstGueltigeForm(code)) return null;
        return await db.Groups
            .Where(g => g.RegistrationCode == code)
            // Ein gelöschter Verein blendet der globale Filter aus -> ClubName null.
            .Select(g => new GruppeInfo(g.Id, g.Name, g.Club != null ? g.Club.Name : null, g.TrainerId))
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    private async Task<List<Guid>> TrainerIdsAsync(Guid groupId, Guid leadId, CancellationToken ct)
    {
        // Der globale Filter blendet offene Einladungen schon aus.
        var weitere = await db.GroupTrainers.Where(t => t.GroupId == groupId).Select(t => t.UserId).ToListAsync(ct);
        return weitere.Append(leadId).Distinct().ToList();
    }
}
