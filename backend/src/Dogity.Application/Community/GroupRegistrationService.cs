using System.Data.Common;
using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

public class GroupRegistrationService(IApplicationDbContext db) : IGroupRegistrationService
{
    // Dieselbe Meldung für "gibt es nicht" und "darfst du nicht": dass die
    // Gruppe oder die Anmeldung existiert, geht Fremde nichts an.
    private const string GruppeNichtGefunden = "Gruppe nicht gefunden.";
    private const string AnmeldungNichtGefunden = "Anmeldung nicht gefunden.";

    /// <summary>SQLSTATE "unique_violation" (Postgres).</summary>
    private const string EindeutigerIndexVerletzt = "23505";

    /// <summary>Wie weit zurück die Tagesauswahl der Anwesenheit Termine anbietet.</summary>
    private const int TageZurueck = 28;
    private const int TageVoraus = 14;

    /// <summary>So weit zurück lässt sich Anwesenheit nachtragen - ein Tippfehler im Jahr soll keine Zeile von 0001 anlegen.</summary>
    private const int MaxNachtragJahre = 2;

    public async Task<Result<GroupRegistrationLinkDto?>> GetLinkAsync(Guid callerId, Guid groupId, CancellationToken ct = default)
    {
        var group = await db.GetManageableGroupAsync(callerId, groupId, ct);
        if (group is null) return Result<GroupRegistrationLinkDto?>.NotFound(GruppeNichtGefunden);

        return Result<GroupRegistrationLinkDto?>.Success(
            string.IsNullOrEmpty(group.RegistrationCode) ? null : new GroupRegistrationLinkDto(group.RegistrationCode));
    }

    public async Task<Result<GroupRegistrationLinkDto>> RegenerateLinkAsync(Guid callerId, Guid groupId, CancellationToken ct = default)
    {
        var group = await db.GetManageableGroupAsync(callerId, groupId, ct);
        if (group is null) return Result<GroupRegistrationLinkDto>.NotFound(GruppeNichtGefunden);

        // Dieselbe Code-Erzeugung wie beim Vereinslink - ein Code ist ein Code.
        group.RegistrationCode = ClubInviteCode.Erzeugen();
        group.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result<GroupRegistrationLinkDto>.Success(new GroupRegistrationLinkDto(group.RegistrationCode));
    }

    public async Task<Result> DisableLinkAsync(Guid callerId, Guid groupId, CancellationToken ct = default)
    {
        var group = await db.GetManageableGroupAsync(callerId, groupId, ct);
        if (group is null) return Result.NotFound(GruppeNichtGefunden);

        if (group.RegistrationCode is null) return Result.Success();

        group.RegistrationCode = null;
        group.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<GroupRegistrationDto>>> ListAsync(Guid callerId, Guid groupId, CancellationToken ct = default)
    {
        if (await db.GetManageableGroupAsync(callerId, groupId, ct) is null)
            return Result<IReadOnlyList<GroupRegistrationDto>>.NotFound(GruppeNichtGefunden);

        var zeilen = await db.GroupRegistrations
            .Where(r => r.GroupId == groupId)
            .OrderByDescending(r => r.RegisteredAt)
            .ThenByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        var ids = zeilen.Select(r => r.Id).ToList();
        var anwesenheiten = await db.GroupRegistrationAttendances
            .Where(a => ids.Contains(a.RegistrationId))
            .GroupBy(a => a.RegistrationId)
            .Select(g => new { Id = g.Key, Anzahl = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Anzahl, ct);

        return Result<IReadOnlyList<GroupRegistrationDto>>.Success(
            zeilen.Select(r => ToDto(r, anwesenheiten.GetValueOrDefault(r.Id))).ToList());
    }

    public async Task<Result<GroupRegistrationDto>> CreateAsync(Guid callerId, Guid groupId, GroupRegistrationRequest request, CancellationToken ct = default)
    {
        if (await db.GetManageableGroupAsync(callerId, groupId, ct) is null)
            return Result<GroupRegistrationDto>.NotFound(GruppeNichtGefunden);

        var heute = Vereinszeit.Heute();
        var (eingabe, fehler) = GroupRegistrationRules.Pruefen(
            request.FirstName, request.LastName, request.DogName, request.DogBreed,
            request.DogBirthDate, request.Phone, request.Notes, heute, referenztag: heute);
        if (eingabe is null) return Result<GroupRegistrationDto>.Failure(fehler!);

        if (await db.GroupRegistrations.CountAsync(r => r.GroupId == groupId, ct) >= GroupRegistrationRules.MaxProGruppe)
            return Result<GroupRegistrationDto>.Failure(
                $"Mehr als {GroupRegistrationRules.MaxProGruppe} Anmeldungen je Gruppe sind nicht möglich. Lösche alte Einträge.");

        var zeile = new GroupRegistration
        {
            GroupId = groupId,
            Source = GroupRegistrationSource.Manual,
            RegisteredAt = DateTimeOffset.UtcNow,
        };
        Uebernehmen(zeile, eingabe);
        db.GroupRegistrations.Add(zeile);
        await db.SaveChangesAsync(ct);
        return Result<GroupRegistrationDto>.Success(ToDto(zeile, 0));
    }

    public async Task<Result<GroupRegistrationDto>> UpdateAsync(Guid callerId, Guid groupId, Guid registrationId, GroupRegistrationRequest request, CancellationToken ct = default)
    {
        var zeile = await FindeAsync(callerId, groupId, registrationId, ct);
        if (zeile is null) return Result<GroupRegistrationDto>.NotFound(AnmeldungNichtGefunden);

        // Gegen den Tag der Anmeldung geprüft, nicht gegen heute: Sonst ließe
        // sich bei einer Anmeldung von vor zwei Jahren nicht einmal mehr die
        // Telefonnummer berichtigen.
        var (eingabe, fehler) = GroupRegistrationRules.Pruefen(
            request.FirstName, request.LastName, request.DogName, request.DogBreed,
            request.DogBirthDate, request.Phone, request.Notes,
            Vereinszeit.Heute(), referenztag: Vereinszeit.Tag(zeile.RegisteredAt));
        if (eingabe is null) return Result<GroupRegistrationDto>.Failure(fehler!);

        Uebernehmen(zeile, eingabe);
        zeile.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result<GroupRegistrationDto>.Success(ToDto(zeile, await ZaehleAnwesenheitAsync(zeile.Id, ct)));
    }

    public async Task<Result> DeleteAsync(Guid callerId, Guid groupId, Guid registrationId, CancellationToken ct = default)
    {
        var zeile = await FindeAsync(callerId, groupId, registrationId, ct);
        if (zeile is null) return Result.NotFound(AnmeldungNichtGefunden);

        await GroupRegistrationErasure.EntfernenAsync(db, db.GroupRegistrations.Where(r => r.Id == registrationId), ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<GroupRegistrationDto>> SetPaidAsync(Guid callerId, Guid groupId, Guid registrationId, bool paid, CancellationToken ct = default)
    {
        var zeile = await FindeAsync(callerId, groupId, registrationId, ct);
        if (zeile is null) return Result<GroupRegistrationDto>.NotFound(AnmeldungNichtGefunden);

        var jetzt = DateTimeOffset.UtcNow;
        // Schon im gewünschten Zustand (zweites Gerät, Doppeltipp): Wer zuerst
        // bezahlt gesetzt hat, bleibt stehen.
        if (paid && zeile.PaidAt is null)
        {
            zeile.PaidAt = jetzt;
            zeile.PaidByUserId = callerId;
            zeile.UpdatedAt = jetzt;
            await db.SaveChangesAsync(ct);
        }
        else if (!paid && zeile.PaidAt is not null)
        {
            zeile.PaidAt = null;
            zeile.PaidByUserId = null;
            zeile.UpdatedAt = jetzt;
            await db.SaveChangesAsync(ct);
        }

        return Result<GroupRegistrationDto>.Success(ToDto(zeile, await ZaehleAnwesenheitAsync(zeile.Id, ct)));
    }

    public async Task<Result<IReadOnlyList<AttendanceDayDto>>> GetAttendanceDaysAsync(Guid callerId, Guid groupId, CancellationToken ct = default)
    {
        if (await db.GetManageableGroupAsync(callerId, groupId, ct) is null)
            return Result<IReadOnlyList<AttendanceDayDto>>.NotFound(GruppeNichtGefunden);

        var heute = Vereinszeit.Heute();
        // Großzügig in UTC geholt, danach exakt nach Vereinszeit gefiltert.
        var von = AnfangVon(heute.AddDays(-TageZurueck).AddDays(-1));
        var bis = AnfangVon(heute.AddDays(TageVoraus).AddDays(2));
        var termine = await db.GroupTrainingSessions
            .Where(s => s.GroupId == groupId && s.Status == GroupTrainingSessionStatus.Planned
                && s.StartsAt >= von && s.StartsAt < bis)
            .OrderBy(s => s.StartsAt)
            .Select(s => new { s.Id, s.StartsAt })
            .AsNoTracking()
            .ToListAsync(ct);

        var tage = termine
            .Select(s => new AttendanceDayDto(Vereinszeit.Tag(s.StartsAt), s.Id, s.StartsAt))
            .Where(t => t.Date >= heute.AddDays(-TageZurueck) && t.Date <= heute.AddDays(TageVoraus))
            // Mehrere Termine am selben Tag: ein Chip, der früheste.
            .GroupBy(t => t.Date)
            .Select(g => g.First())
            .OrderBy(t => t.Date)
            .ToList();
        return Result<IReadOnlyList<AttendanceDayDto>>.Success(tage);
    }

    public async Task<Result<IReadOnlyList<Guid>>> GetAttendanceAsync(Guid callerId, Guid groupId, DateOnly date, CancellationToken ct = default)
    {
        if (await db.GetManageableGroupAsync(callerId, groupId, ct) is null)
            return Result<IReadOnlyList<Guid>>.NotFound(GruppeNichtGefunden);

        var ids = await db.GroupRegistrationAttendances
            .Where(a => a.Date == date && a.Registration!.GroupId == groupId)
            .Select(a => a.RegistrationId)
            .ToListAsync(ct);
        return Result<IReadOnlyList<Guid>>.Success(ids);
    }

    public async Task<Result> SetAttendanceAsync(Guid callerId, Guid groupId, Guid registrationId, DateOnly date, bool present, CancellationToken ct = default)
    {
        var zeile = await FindeAsync(callerId, groupId, registrationId, ct);
        if (zeile is null) return Result.NotFound(AnmeldungNichtGefunden);

        var heute = Vereinszeit.Heute();
        if (date > heute)
            return Result.Failure("Für einen Tag in der Zukunft lässt sich die Anwesenheit nicht eintragen.");
        if (date < heute.AddYears(-MaxNachtragJahre))
            return Result.Failure("Für diesen Tag lässt sich die Anwesenheit nicht mehr eintragen.");

        var jetzt = DateTimeOffset.UtcNow;
        // Auch entfernte Zeilen ansehen (Soft-Delete + eindeutiger Index), sonst
        // gäbe es beim erneuten Abhaken ein Duplikat bzw. einen 500er.
        var (haken, istAktiv) = await db.GroupRegistrationAttendances
            .FindIncludingRemovedAsync(a => a.RegistrationId == registrationId && a.Date == date, ct);

        if (!present)
        {
            if (haken is null || !istAktiv) return Result.Success();
            haken.DeletedAt = jetzt;
            haken.UpdatedAt = jetzt;
        }
        else if (haken is null)
        {
            var neu = new GroupRegistrationAttendance
            {
                RegistrationId = registrationId,
                Date = date,
                GroupTrainingSessionId = await FindeTerminAsync(groupId, date, ct),
                MarkedByUserId = callerId,
                MarkedAt = jetzt,
            };
            db.GroupRegistrationAttendances.Add(neu);
            zeile.UpdatedAt = jetzt;
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is DbException { SqlState: EindeutigerIndexVerletzt })
            {
                // Zwei Trainer:innen haken denselben Tag gleichzeitig ab: Die
                // andere Anfrage war schneller. Kein Fehler - es steht schon da.
                db.GroupRegistrationAttendances.Remove(neu);
            }
            return Result.Success();
        }
        else
        {
            haken.DeletedAt = null;
            haken.GroupTrainingSessionId = await FindeTerminAsync(groupId, date, ct);
            haken.MarkedByUserId = callerId;
            haken.MarkedAt = jetzt;
            haken.UpdatedAt = jetzt;
        }

        // Zuletzt Aktives an der Anmeldung festhalten: Daran bemisst sich die
        // Aufbewahrungsfrist (GroupRegistrationRetention).
        zeile.UpdatedAt = jetzt;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<ImportRegistrationsResultDto>> ImportAsync(Guid callerId, Guid groupId, ImportRegistrationsRequest request, CancellationToken ct = default)
    {
        if (await db.GetManageableGroupAsync(callerId, groupId, ct) is null)
            return Result<ImportRegistrationsResultDto>.NotFound(GruppeNichtGefunden);

        var zeilen = request.Rows;
        if (zeilen is null || zeilen.Count == 0)
            return Result<ImportRegistrationsResultDto>.Failure("Die Datei enthält keine Zeilen zum Importieren.");
        if (zeilen.Count > GroupRegistrationRules.MaxImportZeilen)
            return Result<ImportRegistrationsResultDto>.Failure(
                $"Ein Import nimmt höchstens {GroupRegistrationRules.MaxImportZeilen} Zeilen auf. Teile die Datei auf.");

        var vorhandene = await db.GroupRegistrations
            .Where(r => r.GroupId == groupId)
            .Select(r => new { r.Phone, r.DogName, r.RegisteredAt })
            .AsNoTracking()
            .ToListAsync(ct);
        var schluessel = vorhandene.Select(v => GroupRegistrationRules.SchluesselVon(v.Phone, v.DogName, v.RegisteredAt)).ToList();
        var platz = GroupRegistrationRules.MaxProGruppe - vorhandene.Count;

        var heute = Vereinszeit.Heute();
        var jetzt = DateTimeOffset.UtcNow;
        var angelegt = 0;
        var uebersprungen = 0;
        var fehler = new List<ImportRegistrationErrorDto>();

        foreach (var z in zeilen)
        {
            // Ein Zeitstempel aus der Zukunft oder ohne Wert wird zum Moment des Imports.
            var angemeldet = z.RegisteredAt is { } t && t <= jetzt ? t : jetzt;

            var (eingabe, meldung) = GroupRegistrationRules.Pruefen(
                z.FirstName, z.LastName, z.DogName, z.DogBreed, z.DogBirthDate, z.Phone, notiz: null,
                heute, referenztag: Vereinszeit.Tag(angemeldet));
            if (eingabe is null)
            {
                fehler.Add(new ImportRegistrationErrorDto(z.Zeile, meldung!));
                continue;
            }

            var neu = GroupRegistrationRules.SchluesselVon(eingabe.Phone, eingabe.DogName, angemeldet);
            // Keine Zeitgrenze wie beim Formular: Eine Liste enthält ohnehin nur
            // Altes, und dieselbe Person soll nicht zweimal erscheinen.
            if (GroupRegistrationRules.IstDoppelt(schluessel, neu, nurSeit: null))
            {
                uebersprungen++;
                continue;
            }

            if (platz <= 0)
            {
                fehler.Add(new ImportRegistrationErrorDto(z.Zeile,
                    $"Mehr als {GroupRegistrationRules.MaxProGruppe} Anmeldungen je Gruppe sind nicht möglich."));
                continue;
            }

            var zeile = new GroupRegistration
            {
                GroupId = groupId,
                Source = GroupRegistrationSource.Import,
                RegisteredAt = angemeldet,
            };
            Uebernehmen(zeile, eingabe);
            db.GroupRegistrations.Add(zeile);
            schluessel.Add(neu);
            platz--;
            angelegt++;
        }

        if (angelegt > 0) await db.SaveChangesAsync(ct);
        return Result<ImportRegistrationsResultDto>.Success(new ImportRegistrationsResultDto(angelegt, uebersprungen, fehler));
    }

    /// <summary>Die Anmeldung, wenn der/die Aufrufer:in die Gruppe verwalten darf und sie zu ihr gehört - sonst null.</summary>
    private async Task<GroupRegistration?> FindeAsync(Guid callerId, Guid groupId, Guid registrationId, CancellationToken ct)
    {
        if (await db.GetManageableGroupAsync(callerId, groupId, ct) is null) return null;
        return await db.GroupRegistrations.FirstOrDefaultAsync(r => r.Id == registrationId && r.GroupId == groupId, ct);
    }

    /// <summary>Der Termin der Gruppe an diesem Tag (Vereinszeit), falls es einen gibt - der früheste, ohne abgesagte.</summary>
    private async Task<Guid?> FindeTerminAsync(Guid groupId, DateOnly date, CancellationToken ct)
    {
        var von = AnfangVon(date.AddDays(-1));
        var bis = AnfangVon(date.AddDays(2));
        var termine = await db.GroupTrainingSessions
            .Where(s => s.GroupId == groupId && s.Status == GroupTrainingSessionStatus.Planned
                && s.StartsAt >= von && s.StartsAt < bis)
            .OrderBy(s => s.StartsAt)
            .Select(s => new { s.Id, s.StartsAt })
            .AsNoTracking()
            .ToListAsync(ct);
        return termine.FirstOrDefault(s => Vereinszeit.Tag(s.StartsAt) == date)?.Id;
    }

    private static DateTimeOffset AnfangVon(DateOnly tag) => new(tag.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private Task<int> ZaehleAnwesenheitAsync(Guid registrationId, CancellationToken ct) =>
        db.GroupRegistrationAttendances.CountAsync(a => a.RegistrationId == registrationId, ct);

    private static void Uebernehmen(GroupRegistration zeile, GroupRegistrationInput eingabe)
    {
        zeile.FirstName = eingabe.FirstName;
        zeile.LastName = eingabe.LastName;
        zeile.DogName = eingabe.DogName;
        zeile.DogBreed = eingabe.DogBreed;
        zeile.DogBirthDate = eingabe.DogBirthDate;
        zeile.Phone = eingabe.Phone;
        zeile.Notes = eingabe.Notes;
    }

    private static GroupRegistrationDto ToDto(GroupRegistration r, int anwesenheiten) =>
        new(r.Id, r.FirstName, r.LastName, r.DogName, r.DogBreed, r.DogBirthDate, r.Phone, r.Source,
            r.RegisteredAt, r.PaidAt, r.Notes, anwesenheiten);
}
