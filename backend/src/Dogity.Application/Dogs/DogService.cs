using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Community;
using Dogity.Application.Notifications;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Dogs;

/// <summary>
/// Use Cases für die Hundeverwaltung (siehe FEATURE_MODULE.md "Core").
/// Zugriff ist immer auf Hunde beschränkt, die dem aufrufenden Benutzer
/// über <see cref="DogOwner"/> zugeordnet sind.
/// </summary>
public class DogService(
    IApplicationDbContext db,
    IUserLookupService userLookup,
    INotificationService notifications,
    TimeProvider? timeProvider = null) : IDogService
{
    private readonly TimeProvider zeit = timeProvider ?? TimeProvider.System;

    private DateOnly Heute() => Vereinszeit.Tag(zeit.GetUtcNow());

    public async Task<Result<IReadOnlyList<DogDto>>> GetMyDogsAsync(Guid userId, CancellationToken ct = default)
    {
        var dogs = await db.DogOwners
            .Where(o => o.UserId == userId)
            .Select(o => o.Dog!)
            .ToListAsync(ct);

        // Nur die Ids der Hunde MIT Bild holen - die Bilddaten selbst bleiben
        // in der Datenbank, die Liste zeigt ohnehin nur, ob eines existiert.
        var dogIds = dogs.Select(d => d.Id).ToList();
        var withImage = await db.DogImages
            .Where(i => dogIds.Contains(i.DogId))
            .Select(i => i.DogId)
            .ToListAsync(ct);

        // Meine Hunde: Ich bin Besitzer:in (oder Mitbesitzer:in) und sehe die
        // Pause mit Grund und Notiz.
        var pausen = await db.ActivePausesAsync(dogIds, Heute(), ct);
        var gesundheit = await db.HealthSummariesAsync(dogIds, Heute(), ct);
        return Result<IReadOnlyList<DogDto>>.Success(
            dogs.Select(d => ToDto(d, withImage.Contains(d.Id), pausen.GetValueOrDefault(d.Id), zeigeDetails: true, gesundheit.GetValueOrDefault(d.Id))).ToList());
    }

    /// <summary>
    /// Die Hunde, die ich als Trainer:in betreue (TrainerAssignment) - der
    /// direkte Weg von der Trainerübersicht zum Hund. Vorher führte der
    /// einzige Weg dorthin über Gruppe -> Mitglied aufklappen -> Hund, also
    /// vier Schritte für etwas, das man mehrmals pro Trainingsabend braucht.
    /// </summary>
    public async Task<Result<IReadOnlyList<SupervisedDogDto>>> GetSupervisedDogsAsync(Guid trainerId, CancellationToken ct = default)
    {
        var rows = await db.TrainerAssignments
            .Where(t => t.TrainerId == trainerId)
            .Select(t => new
            {
                t.DogId,
                t.MemberId,
                Dog = db.Dogs.FirstOrDefault(d => d.Id == t.DogId),
                HasImage = db.DogImages.Any(i => i.DogId == t.DogId),
                ActiveGoalCount = db.Goals.Count(g => g.DogId == t.DogId && g.Status == GoalStatus.Active),
            })
            .AsNoTracking()
            .ToListAsync(ct);

        // Archivierte Hunde und Zuordnungen zu inzwischen gelöschten Hunden
        // gehören nicht in die Arbeitsliste eines Trainingsabends.
        var visible = rows.Where(r => r.Dog is not null && r.Dog.ArchivedAt is null).ToList();

        var handlers = await userLookup.FindByIdsAsync(visible.Select(r => r.MemberId).Distinct().ToList(), ct);
        string HandlerName(Guid id) =>
            handlers.TryGetValue(id, out var info) ? $"{info.FirstName} {info.LastName}".Trim() : "(unbekannt)";

        // Als Trainer:in nur der Zeitraum: Grund und Notiz gehören der Familie.
        var pausen = await db.ActivePausesAsync(visible.Select(r => r.Dog!.Id).Distinct().ToList(), Heute(), ct);
        var dtos = visible
            .Select(r => new SupervisedDogDto(
                r.Dog!.Id, r.Dog.Name, r.Dog.Breed, r.HasImage, HandlerName(r.MemberId), r.ActiveGoalCount,
                pausen.TryGetValue(r.Dog.Id, out var pause) ? DogPauseQueries.ToDto(pause, zeigeDetails: false) : null))
            .OrderBy(d => d.Name)
            .ToList();

        return Result<IReadOnlyList<SupervisedDogDto>>.Success(dtos);
    }

    public async Task<Result<DogDto>> GetByIdAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        if (!await db.HasDogAccessAsync(userId, dogId, ct))
            return Result<DogDto>.NotFound("Hund nicht gefunden.");

        var dog = await db.Dogs.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dogId, ct);
        if (dog is null) return Result<DogDto>.NotFound("Hund nicht gefunden.");

        return Result<DogDto>.Success(await ToDtoAsync(dog, userId, ct));
    }

    public async Task<Result<DogDto>> CreateAsync(Guid userId, CreateDogRequest request, CancellationToken ct = default)
    {
        var validationError = Validate(request.Name, request.Breed);
        if (validationError is not null)
            return Result<DogDto>.Failure(validationError);

        var dog = new Dog
        {
            Name = request.Name.Trim(),
            Breed = request.Breed,
            Birthday = request.Birthday,
            Gender = request.Gender,
            ImageUrl = request.ImageUrl,
            Notes = request.Notes
        };
        dog.Owners.Add(new DogOwner { DogId = dog.Id, UserId = userId, Role = DogOwnerRole.Owner });

        db.Dogs.Add(dog);
        await db.SaveChangesAsync(ct);

        return Result<DogDto>.Success(await ToDtoAsync(dog, userId, ct));
    }

    public async Task<Result<DogDto>> UpdateAsync(Guid userId, Guid dogId, UpdateDogRequest request, CancellationToken ct = default)
    {
        var validationError = Validate(request.Name, request.Breed);
        if (validationError is not null)
            return Result<DogDto>.Failure(validationError);

        var dog = await GetOwnedDogAsync(userId, dogId, ct);
        if (dog is null)
            return Result<DogDto>.NotFound("Hund nicht gefunden.");

        dog.Name = request.Name.Trim();
        dog.Breed = request.Breed;
        dog.Birthday = request.Birthday;
        dog.Gender = request.Gender;
        dog.ImageUrl = request.ImageUrl;
        dog.Notes = request.Notes;
        dog.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result<DogDto>.Success(await ToDtoAsync(dog, userId, ct));
    }

    public async Task<Result> SetArchivedAsync(Guid userId, Guid dogId, bool archived, CancellationToken ct = default)
    {
        var dog = await GetOwnedDogAsync(userId, dogId, ct);
        if (dog is null)
            return Result.NotFound("Hund nicht gefunden.");

        // Archivieren blendet den Hund nur aus (kein Soft-Delete) - die Historie
        // bleibt vollständig erhalten und die Aktion ist jederzeit reversibel.
        dog.ArchivedAt = archived ? DateTimeOffset.UtcNow : null;
        dog.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        var dog = await GetOwnedDogAsync(userId, dogId, ct);
        if (dog is null)
            return Result.NotFound("Hund nicht gefunden.");

        dog.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<DogOwnerDto>>> GetOwnersAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        if (!await db.HasDogAccessAsync(userId, dogId, ct))
            return Result<IReadOnlyList<DogOwnerDto>>.NotFound("Hund nicht gefunden.");

        // Offene Einladungen sehen nur die Besitzer:innen - sie verwalten sie.
        // Eine betreuende Trainer:in geht nichts an, wen die Familie gerade
        // einlädt.
        var istBesitzer = await db.DogOwners.AnyAsync(o => o.DogId == dogId && o.UserId == userId, ct);
        var ownerRows = await db.DogOwners
            .IgnoreQueryFilters()
            .Where(o => o.DogId == dogId && o.DeletedAt == null)
            .Where(o => o.Status == DogOwnerStatus.Active || (istBesitzer && o.Status == DogOwnerStatus.Invited))
            .Select(o => new { o.UserId, o.Role, o.Status, o.InvitedEmail, o.CreatedAt, o.UpdatedAt })
            .AsNoTracking()
            .ToListAsync(ct);

        var lookup = await userLookup.FindByIdsAsync(ownerRows.Select(o => o.UserId).ToList(), ct);
        var dtos = ownerRows
            .Select(o =>
            {
                var info = lookup.GetValueOrDefault(o.UserId);
                // Vor der Zusage nur die Adresse, die die einladende Person
                // selbst eingegeben hat - der Name gehört der eingeladenen
                // Person, bis sie annimmt (siehe DogOwnerStatus.Invited).
                return o.Status == DogOwnerStatus.Invited
                    ? new DogOwnerDto(o.UserId, o.InvitedEmail ?? info?.Email ?? "(unbekannt)", "", "", o.Role, o.UpdatedAt ?? o.CreatedAt, IsInvited: true)
                    : info is not null
                        ? new DogOwnerDto(o.UserId, info.Email, info.FirstName, info.LastName, o.Role, o.CreatedAt)
                        : new DogOwnerDto(o.UserId, "(unbekannt)", "", "", o.Role, o.CreatedAt);
            })
            .ToList();

        return Result<IReadOnlyList<DogOwnerDto>>.Success(dtos);
    }

    /// <summary>
    /// Lädt eine Person ein, den Hund mitzuverwalten. Mitbesitzer:in wird sie
    /// erst, wenn sie annimmt (<see cref="RespondToInvitationAsync"/>) - bis
    /// dahin sieht sie nichts vom Hund außer seinem Namen, und die einladende
    /// Person nichts von ihr außer der eingegebenen Adresse.
    /// </summary>
    public async Task<Result> AddOwnerAsync(Guid userId, Guid dogId, AddDogOwnerRequest request, CancellationToken ct = default)
    {
        if (!await db.DogOwners.AnyAsync(o => o.DogId == dogId && o.UserId == userId && o.Role == DogOwnerRole.Owner, ct))
            return Result.NotFound("Hund nicht gefunden oder keine Berechtigung.");

        var target = await userLookup.FindByEmailAsync(request.Email, ct);
        if (target is null)
            return Result.Failure("Kein Benutzer mit dieser E-Mail-Adresse gefunden.");

        if (target.UserId == userId)
            return Result.Failure("Du bist bereits Besitzer dieses Hundes.");

        // Auch entfernte Zeilen ansehen (Soft-Delete + eindeutiger Index):
        // Ein entfernter Mitbesitzer ließ sich sonst nie wieder hinzufügen.
        var (existing, isActive) = await db.DogOwners
            .FindIncludingRemovedAsync(o => o.DogId == dogId && o.UserId == target.UserId, ct);
        if (isActive)
            return Result.Failure(existing!.Status == DogOwnerStatus.Invited
                ? "Diese Person ist bereits eingeladen."
                : "Dieser Benutzer ist bereits Mitbesitzer dieses Hundes.");

        // Eine Einladung, keine Aufnahme - auch für jemanden, der früher schon
        // einmal Mitbesitzer:in war. Wer gegangen ist oder entfernt wurde, hat
        // damit nicht für immer zugestimmt.
        if (existing is not null)
        {
            existing.DeletedAt = null;
            existing.Role = DogOwnerRole.Owner;
            existing.Status = DogOwnerStatus.Invited;
            existing.InvitedByUserId = userId;
            existing.InvitedEmail = request.Email.Trim();
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            db.DogOwners.Add(new DogOwner
            {
                DogId = dogId,
                UserId = target.UserId,
                Role = DogOwnerRole.Owner,
                Status = DogOwnerStatus.Invited,
                InvitedByUserId = userId,
                InvitedEmail = request.Email.Trim(),
            });
        }

        await db.SaveChangesAsync(ct);

        var hundName = await db.Dogs.Where(d => d.Id == dogId).Select(d => d.Name).FirstOrDefaultAsync(ct);
        var name = await DisplayNameAsync(userId, ct);
        // Ohne Namen oder ohne Hund je ein eigener ganzer Satz (kein übersetzbarer Satzteil als Wert).
        var einladung = name is null
            ? hundName is null
                ? Meldung.Von("Jemand lädt dich ein, einen Hund gemeinsam zu verwalten.")
                : Meldung.Von("Jemand lädt dich ein, {hund} gemeinsam zu verwalten.", ("hund", hundName))
            : hundName is null
                ? Meldung.Von("{name} lädt dich ein, einen Hund gemeinsam zu verwalten.", ("name", name))
                : Meldung.Von("{name} lädt dich ein, {hund} gemeinsam zu verwalten.", ("name", name), ("hund", hundName));
        await notifications.CreateAsync(
            target.UserId,
            einladung,
            "/dogs",
            ct,
            wichtig: true);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<DogInvitationDto>>> GetMyInvitationsAsync(Guid userId, CancellationToken ct = default)
    {
        // IgnoreQueryFilters nimmt auch den Filter des Hundes weg - deshalb
        // "nicht gelöscht" hier ausdrücklich für beide.
        var rows = await db.DogOwners
            .IgnoreQueryFilters()
            .Where(o => o.UserId == userId && o.DeletedAt == null && o.Status == DogOwnerStatus.Invited)
            .Where(o => o.Dog!.DeletedAt == null)
            .Select(o => new { o.DogId, DogName = o.Dog!.Name, o.InvitedByUserId, o.CreatedAt, o.UpdatedAt })
            .AsNoTracking()
            .ToListAsync(ct);

        var einladende = await userLookup.FindByIdsAsync(
            rows.Where(r => r.InvitedByUserId is not null).Select(r => r.InvitedByUserId!.Value).Distinct().ToList(), ct);
        var dtos = rows
            .Select(r => new DogInvitationDto(
                r.DogId,
                r.DogName,
                r.InvitedByUserId is { } von && einladende.TryGetValue(von, out var info) ? $"{info.FirstName} {info.LastName}".Trim() : null,
                r.UpdatedAt ?? r.CreatedAt))
            .OrderByDescending(d => d.InvitedAt)
            .ToList();

        return Result<IReadOnlyList<DogInvitationDto>>.Success(dtos);
    }

    public async Task<Result> RespondToInvitationAsync(Guid userId, Guid dogId, bool accept, CancellationToken ct = default)
    {
        var row = await db.DogOwners
            .IgnoreQueryFilters()
            .Where(o => o.Dog!.DeletedAt == null)
            .FirstOrDefaultAsync(o => o.DogId == dogId && o.UserId == userId && o.DeletedAt == null && o.Status == DogOwnerStatus.Invited, ct);
        if (row is null)
            return Result.NotFound("Einladung nicht gefunden.");

        if (accept)
        {
            row.Status = DogOwnerStatus.Active;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            row.DeletedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// Entfernt eine Mitbesitzer:in, zieht eine offene Einladung zurück oder
    /// beendet - mit der eigenen Id - den eigenen Mitbesitz.
    /// </summary>
    public async Task<Result> RemoveOwnerAsync(Guid userId, Guid dogId, Guid targetUserId, CancellationToken ct = default)
    {
        if (!await db.DogOwners.AnyAsync(o => o.DogId == dogId && o.UserId == userId && o.Role == DogOwnerRole.Owner, ct))
            return Result.NotFound("Hund nicht gefunden oder keine Berechtigung.");

        var ownerRow = await db.DogOwners
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.DogId == dogId && o.UserId == targetUserId && o.DeletedAt == null, ct);
        if (ownerRow is null)
            return Result.NotFound("Besitzer nicht gefunden.");

        var warAktiv = ownerRow.Status == DogOwnerStatus.Active;
        if (warAktiv)
        {
            var totalOwners = await db.DogOwners.CountAsync(o => o.DogId == dogId && o.Role == DogOwnerRole.Owner, ct);
            if (totalOwners <= 1)
                return Result.Failure("Der letzte Besitzer kann nicht entfernt werden.");
        }

        var jetzt = DateTimeOffset.UtcNow;
        ownerRow.DeletedAt = jetzt;

        // Betreuungen, die über diese Person liefen, enden mit ihrem
        // Mitbesitz - wie beim Verlassen einer Gruppe (EndSupervisionsViaGroupAsync).
        // Sonst behielte die Trainer:in der gegangenen Person Tagebuch, Ziele
        // und Fährten der verbliebenen Besitzer:innen im Blick, und die hätten
        // keinen Weg, das zu beenden: Sie stehen zu dieser Trainer:in in keiner
        // Beziehung.
        if (warAktiv)
        {
            var betreuungen = await db.TrainerAssignments
                .Where(a => a.DogId == dogId && a.MemberId == targetUserId)
                .ToListAsync(ct);
            foreach (var betreuung in betreuungen)
                betreuung.DeletedAt = jetzt;
        }

        await db.SaveChangesAsync(ct);

        // Wer von anderen entfernt wird, soll es erfahren - vorher war ein Hund
        // samt Tagebuch einfach aus der eigenen Liste verschwunden. Nicht beim
        // eigenen Austritt und nicht bei einer zurückgezogenen Einladung.
        if (warAktiv && targetUserId != userId)
        {
            var hundName = await db.Dogs.Where(d => d.Id == dogId).Select(d => d.Name).FirstOrDefaultAsync(ct);
            var name = await DisplayNameAsync(userId, ct);
            var entfernt = name is null
                ? hundName is null
                    ? Meldung.Von("Eine Besitzer:in hat dich als Mitbesitzer:in von einem Hund entfernt.")
                    : Meldung.Von("Eine Besitzer:in hat dich als Mitbesitzer:in von {hund} entfernt.", ("hund", hundName))
                : hundName is null
                    ? Meldung.Von("{name} hat dich als Mitbesitzer:in von einem Hund entfernt.", ("name", name))
                    : Meldung.Von("{name} hat dich als Mitbesitzer:in von {hund} entfernt.", ("name", name), ("hund", hundName));
            await notifications.CreateAsync(
                targetUserId,
                entfernt,
                null,
                ct);
        }
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<DogBetreuungDto>>> GetSupervisionsAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        // Wer den Hund nur betreut, bekommt dieselbe Antwort wie jede fremde
        // Person: Die Liste gehört der Familie (siehe GetOwnersAsync).
        if (!await db.DogOwners.AnyAsync(o => o.DogId == dogId && o.UserId == userId, ct))
            return Result<IReadOnlyList<DogBetreuungDto>>.NotFound("Hund nicht gefunden.");

        var betreuungen = await db.TrainerAssignments
            .Where(a => a.DogId == dogId)
            .OrderBy(a => a.StartDate)
            .Select(a => new { a.TrainerId, a.MemberId, a.StartDate })
            .AsNoTracking()
            .ToListAsync(ct);

        var lookup = await userLookup.FindByIdsAsync(betreuungen.Select(b => b.TrainerId).Distinct().ToList(), ct);
        var dtos = new List<DogBetreuungDto>();
        foreach (var b in betreuungen)
        {
            var name = lookup.TryGetValue(b.TrainerId, out var info) ? $"{info.FirstName} {info.LastName}".Trim() : "";
            // Dieselbe Beziehung wie beim Beenden durch die Gruppenverwaltung
            // (GroupService.GehoertZurGruppeAsync): das betreute Mitglied ist
            // aktiv in der Gruppe, die betreuende Person dort Trainer:in. Ohne
            // Treffer (Gruppe verlassen, Betreuung blieb) bleibt der Name offen.
            var gruppe = await db.Groups
                .Where(g => g.Members.Any(m => m.UserId == b.MemberId && m.Status == GroupMemberStatus.Active))
                .Where(g => g.TrainerId == b.TrainerId
                    || g.Trainers.Any(t => t.UserId == b.TrainerId)
                    || (g.ClubId != null && db.ClubTrainers.Any(t => t.ClubId == g.ClubId && t.UserId == b.TrainerId)))
                .OrderBy(g => g.Name)
                .Select(g => g.Name)
                .FirstOrDefaultAsync(ct);
            dtos.Add(new DogBetreuungDto(b.TrainerId, name.Length > 0 ? name : "(unbekannt)", b.StartDate, gruppe));
        }

        return Result<IReadOnlyList<DogBetreuungDto>>.Success(dtos);
    }

    public async Task<Result> EndSupervisionAsync(Guid userId, Guid dogId, Guid trainerId, CancellationToken ct = default)
    {
        if (!await db.DogOwners.AnyAsync(o => o.DogId == dogId && o.UserId == userId, ct))
            return Result.NotFound("Hund nicht gefunden.");

        // Eine schon beendete Betreuung fällt am Query-Filter heraus: wie bei
        // RemoveTrainerFromDogAsync dann "nicht gefunden" - und die Trainer:in
        // wird nicht ein zweites Mal benachrichtigt.
        var betreuung = await db.TrainerAssignments.FirstOrDefaultAsync(a => a.DogId == dogId && a.TrainerId == trainerId, ct);
        if (betreuung is null)
            return Result.NotFound("Betreuung nicht gefunden.");

        betreuung.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        // Die Trainer:in soll nicht rätseln, warum der Hund aus ihrer Liste
        // verschwunden ist (vgl. Entfernen als Mitbesitzer:in).
        var hundName = await db.Dogs.Where(d => d.Id == dogId).Select(d => d.Name).FirstOrDefaultAsync(ct);
        var name = await DisplayNameAsync(userId, ct);
        var beendet = name is null
            ? hundName is null
                ? Meldung.Von("Eine Besitzer:in hat die Betreuung von einem Hund beendet.")
                : Meldung.Von("Eine Besitzer:in hat die Betreuung von {hund} beendet.", ("hund", hundName))
            : hundName is null
                ? Meldung.Von("{name} hat die Betreuung von einem Hund beendet.", ("name", name))
                : Meldung.Von("{name} hat die Betreuung von {hund} beendet.", ("name", name), ("hund", hundName));
        await notifications.CreateAsync(
            trainerId,
            beendet,
            "/trainer",
            ct);
        return Result.Success();
    }

    private async Task<string?> DisplayNameAsync(Guid userId, CancellationToken ct) =>
        (await userLookup.FindByIdsAsync([userId], ct)).TryGetValue(userId, out var info)
            ? $"{info.FirstName} {info.LastName}".Trim()
            : null;

    public async Task<Result<DogImageDto>> GetImageAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        if (!await db.HasDogAccessAsync(userId, dogId, ct))
            return Result<DogImageDto>.NotFound("Hund nicht gefunden.");

        var image = await db.DogImages.AsNoTracking().FirstOrDefaultAsync(i => i.DogId == dogId, ct);
        return image is null
            ? Result<DogImageDto>.Failure("Kein Bild hinterlegt.")
            : Result<DogImageDto>.Success(new DogImageDto(
                $"data:{image.ContentType};base64,{Convert.ToBase64String(image.Data)}"));
    }

    /// <summary>
    /// Kennzeichen des aktuell hinterlegten Bildes - ohne die Bilddaten zu
    /// lesen. Grundlage für den bedingten Abruf (ETag): Ein Profilbild wiegt
    /// als Data-URI rund 64 KB, und es hing bisher an JEDEM Aufbau einer
    /// Liste, in der der Hund vorkommt - die Antwort trägt weder
    /// Cache-Control noch ETag, der Browser kann sie also nicht behalten, und
    /// die Trainerübersicht mit einem Dutzend Hunden lud jedes Mal ein
    /// Megabyte neu. Mit dem Kennzeichen beantwortet der Server den
    /// Zweitabruf mit 304 und ohne Rumpf.
    ///
    /// Bewusst aus Id + Zeitstempel statt aus einer Prüfsumme über die Daten:
    /// die Prüfsumme müsste die Bytes lesen, genau das soll entfallen. Beim
    /// Austausch des Bildes ändert sich UpdatedAt, beim Ersetzen der Zeile
    /// die Id - beides deckt den Wechsel ab.
    /// </summary>
    public async Task<Result<string>> GetImageETagAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        if (!await db.HasDogAccessAsync(userId, dogId, ct))
            return Result<string>.NotFound("Hund nicht gefunden.");

        var stempel = await db.DogImages
            .AsNoTracking()
            .Where(i => i.DogId == dogId)
            .Select(i => new { i.Id, i.UpdatedAt, i.CreatedAt })
            .FirstOrDefaultAsync(ct);

        if (stempel is null) return Result<string>.Failure("Kein Bild hinterlegt.");

        var zeit = stempel.UpdatedAt ?? stempel.CreatedAt;
        return Result<string>.Success($"\"{stempel.Id:N}-{zeit.UtcTicks}\"");
    }

    public async Task<Result> SetImageAsync(Guid userId, Guid dogId, string dataUrl, CancellationToken ct = default)
    {
        // Bild ändern darf nur, wer den Hund besitzt - ein zugewiesener Trainer
        // sieht ihn zwar, verwaltet ihn aber nicht (wie bei UpdateAsync).
        var dog = await GetOwnedDogAsync(userId, dogId, ct);
        if (dog is null) return Result.NotFound("Hund nicht gefunden.");

        if (!TryParseDataUrl(dataUrl, out var contentType, out var bytes, out var error))
            return Result.Failure(error!);

        var existing = await db.DogImages.FirstOrDefaultAsync(i => i.DogId == dogId, ct);
        if (existing is null)
        {
            db.DogImages.Add(new DogImage { DogId = dogId, Data = bytes!, ContentType = contentType! });
        }
        else
        {
            existing.Data = bytes!;
            existing.ContentType = contentType!;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteImageAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        var dog = await GetOwnedDogAsync(userId, dogId, ct);
        if (dog is null) return Result.NotFound("Hund nicht gefunden.");

        var image = await db.DogImages.FirstOrDefaultAsync(i => i.DogId == dogId, ct);
        // Kein Bild da? Dann ist das Ziel bereits erreicht - kein Fehler.
        if (image is null) return Result.Success();

        // Echtes Löschen statt Soft-Delete: ein entferntes Bild soll auch
        // wirklich verschwinden, und es hängt keine Historie daran.
        db.DogImages.Remove(image);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Obergrenze der entpackten Bilddaten. Der Client rechnet vorher herunter - das hier ist die Notbremse.</summary>
    private const int MaxImageBytes = 2 * 1024 * 1024;

    private static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png", "image/webp"];

    /// <summary>
    /// Zerlegt "data:image/jpeg;base64,...." in MIME-Typ und Daten.
    ///
    /// Der MIME-Typ wird gegen eine feste Liste geprüft und nicht einfach
    /// übernommen: er landet später unverändert im Content-Type der Antwort,
    /// und ein frei wählbarer Typ (etwa text/html) machte aus dem Bildabruf
    /// eine Seite, die der Browser ausführt.
    /// </summary>
    private static bool TryParseDataUrl(string? dataUrl, out string? contentType, out byte[]? bytes, out string? error)
    {
        contentType = null;
        bytes = null;

        if (string.IsNullOrWhiteSpace(dataUrl))
        {
            error = "Kein Bild übermittelt.";
            return false;
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            dataUrl, @"^data:(?<type>[a-z]+/[a-z0-9.+-]+);base64,(?<data>.+)$",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            error = "Bildformat nicht erkannt.";
            return false;
        }

        var type = match.Groups["type"].Value.ToLowerInvariant();
        if (!AllowedImageTypes.Contains(type))
        {
            error = "Nur JPEG, PNG und WebP sind möglich.";
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(match.Groups["data"].Value);
        }
        catch (FormatException)
        {
            error = "Bilddaten sind beschädigt.";
            return false;
        }

        if (bytes.Length == 0)
        {
            error = "Bilddaten sind leer.";
            return false;
        }

        if (bytes.Length > MaxImageBytes)
        {
            var megabyte = MaxImageBytes / 1024 / 1024;
            error = $"Bild ist zu groß (max. {megabyte} MB).";
            return false;
        }

        contentType = type;
        error = null;
        return true;
    }

    private async Task<Dog?> GetOwnedDogAsync(Guid userId, Guid dogId, CancellationToken ct) =>
        await db.Dogs
            .Where(d => d.Id == dogId)
            .Where(d => d.Owners.Any(o => o.UserId == userId))
            .FirstOrDefaultAsync(ct);

    private static string? Validate(string name, string? breed) =>
        string.IsNullOrWhiteSpace(name)
            ? "Name ist erforderlich."
            : Textlaengen.ZuLang(name, Textlaengen.Hundename, "Der Name")
                ?? Textlaengen.ZuLang(breed, Textlaengen.Hunderasse, "Die Rasse");

    private Task<bool> HasImageAsync(Guid dogId, CancellationToken ct) =>
        db.DogImages.AnyAsync(i => i.DogId == dogId, ct);

    /// <summary>
    /// Der Hund für genau diese Person: Eine Besitzer:in sieht die Pause mit
    /// Grund und Notiz und den Gesundheitsstand, eine betreuende Trainer:in nur den
    /// Zeitraum der Pause und nichts zur Gesundheit.
    /// </summary>
    private async Task<DogDto> ToDtoAsync(Dog dog, Guid userId, CancellationToken ct)
    {
        var istBesitzer = await db.DogOwners.AnyAsync(o => o.DogId == dog.Id && o.UserId == userId, ct);
        var pausen = await db.ActivePausesAsync([dog.Id], Heute(), ct);
        var gesundheit = istBesitzer ? (await db.HealthSummariesAsync([dog.Id], Heute(), ct)).GetValueOrDefault(dog.Id) : null;
        return ToDto(dog, await HasImageAsync(dog.Id, ct), pausen.GetValueOrDefault(dog.Id), istBesitzer, gesundheit);
    }

    private static DogDto ToDto(Dog d, bool hasImage, DogPause? pause, bool zeigeDetails, DogHealthSummaryDto? gesundheit) =>
        new(d.Id, d.Name, d.Breed, d.Birthday, d.Gender, d.ImageUrl, d.Notes, d.ArchivedAt, hasImage,
            pause is null ? null : DogPauseQueries.ToDto(pause, zeigeDetails), gesundheit);
}
