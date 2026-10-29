using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Community;
using Dogity.Domain.Dogs;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Dogs;

/// <inheritdoc />
public class DogHealthService(IApplicationDbContext db, TimeProvider timeProvider) : IDogHealthService
{
    // Grenzen gegen Tippfehler wie das Jahr 2206: weit genug für einen alten Hund und lange
    // Fristen, eng genug, damit eine Datumsrechnung nie an den Rand von DateOnly stößt.
    private const int MaxJahreZurueck = 25;
    private const int MaxJahreVoraus = 2;
    private const int MaxFristJahre = 10;

    public async Task<Result<IReadOnlyList<DogHealthRecordDto>>> GetAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        if (!await db.IstBesitzerAsync(userId, dogId, ct))
            return Result<IReadOnlyList<DogHealthRecordDto>>.NotFound("Hund nicht gefunden.");

        var heute = Heute();
        var eintraege = await db.DogHealthRecords
            .Where(r => r.DogId == dogId)
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        var aktuell = Gesundheitsstand.Aktuelle(eintraege).Select(r => r.Id).ToHashSet();
        return Result<IReadOnlyList<DogHealthRecordDto>>.Success(eintraege
            .Select(r => DogHealthQueries.ToDto(r, r.Kind == DogHealthKind.VetAppointment ? r.Date >= heute : aktuell.Contains(r.Id)))
            .ToList());
    }

    public async Task<Result<DogHealthRecordDto>> CreateAsync(Guid userId, Guid dogId, SaveDogHealthRecordRequest request, CancellationToken ct = default)
    {
        if (!await db.IstBesitzerAsync(userId, dogId, ct))
            return Result<DogHealthRecordDto>.NotFound("Hund nicht gefunden.");

        var fehler = Pruefe(request);
        if (fehler is not null) return Result<DogHealthRecordDto>.Failure(fehler);

        var eintrag = new DogHealthRecord { DogId = dogId, CreatedByUserId = userId };
        Uebernehme(eintrag, request);
        db.DogHealthRecords.Add(eintrag);
        await db.SaveChangesAsync(ct);

        return Result<DogHealthRecordDto>.Success(await ToDtoAsync(eintrag, ct));
    }

    public async Task<Result<DogHealthRecordDto>> UpdateAsync(Guid userId, Guid dogId, Guid recordId, SaveDogHealthRecordRequest request, CancellationToken ct = default)
    {
        if (!await db.IstBesitzerAsync(userId, dogId, ct))
            return Result<DogHealthRecordDto>.NotFound("Hund nicht gefunden.");

        var eintrag = await db.DogHealthRecords.FirstOrDefaultAsync(r => r.Id == recordId && r.DogId == dogId, ct);
        if (eintrag is null)
            return Result<DogHealthRecordDto>.NotFound("Eintrag nicht gefunden.");

        var fehler = Pruefe(request);
        if (fehler is not null) return Result<DogHealthRecordDto>.Failure(fehler);

        Uebernehme(eintrag, request);
        eintrag.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);

        return Result<DogHealthRecordDto>.Success(await ToDtoAsync(eintrag, ct));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid dogId, Guid recordId, CancellationToken ct = default)
    {
        if (!await db.IstBesitzerAsync(userId, dogId, ct))
            return Result.NotFound("Hund nicht gefunden.");

        var eintrag = await db.DogHealthRecords.FirstOrDefaultAsync(r => r.Id == recordId && r.DogId == dogId, ct);
        if (eintrag is null)
            return Result.NotFound("Eintrag nicht gefunden.");

        eintrag.DeletedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private DateOnly Heute() => Vereinszeit.Tag(timeProvider.GetUtcNow());

    private static void Uebernehme(DogHealthRecord eintrag, SaveDogHealthRecordRequest request)
    {
        eintrag.Kind = request.Kind;
        eintrag.Title = request.Title.Trim();
        eintrag.Date = request.Date;
        eintrag.Time = request.Time;
        eintrag.DueDate = request.DueDate;
        eintrag.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
    }

    /// <summary>Der Eintrag mit der Kennzeichnung "aktuell", die erst der Blick auf die übrigen Einträge des Hundes ergibt.</summary>
    private async Task<DogHealthRecordDto> ToDtoAsync(DogHealthRecord eintrag, CancellationToken ct)
    {
        if (eintrag.Kind == DogHealthKind.VetAppointment)
            return DogHealthQueries.ToDto(eintrag, eintrag.Date >= Heute());

        var geschwister = await db.DogHealthRecords
            .Where(r => r.DogId == eintrag.DogId && r.Kind == eintrag.Kind)
            .AsNoTracking()
            .ToListAsync(ct);
        return DogHealthQueries.ToDto(eintrag, Gesundheitsstand.Aktuelle(geschwister).Any(r => r.Id == eintrag.Id));
    }

    /// <summary>Die Fehlermeldung zur Eingabe, sonst null.</summary>
    private string? Pruefe(SaveDogHealthRecordRequest request)
    {
        if (!Enum.IsDefined(request.Kind))
            return "Bitte eine Art für den Eintrag wählen.";

        if (string.IsNullOrWhiteSpace(request.Title))
            return "Bitte einen Titel angeben.";
        var zuLang = Textlaengen.ZuLang(request.Title, Textlaengen.GesundheitTitel, "Der Titel")
            ?? Textlaengen.ZuLang(request.Note, Textlaengen.GesundheitNotiz, "Die Notiz");
        if (zuLang is not null) return zuLang;

        var heute = Heute();
        if (request.Date < heute.AddYears(-MaxJahreZurueck) || request.Date > heute.AddYears(MaxJahreVoraus))
            return "Das Datum liegt zu weit entfernt.";

        var istTermin = request.Kind == DogHealthKind.VetAppointment;
        if (!istTermin && request.Time is not null)
            return "Eine Uhrzeit gibt es nur beim Tierarzttermin.";
        if (istTermin && request.DueDate is not null)
            return "Ein Tierarzttermin hat kein Ablaufdatum.";

        if (request.DueDate is { } faellig)
        {
            if (faellig < request.Date)
                return "Das Ablaufdatum darf nicht vor dem Datum des Eintrags liegen.";
            if (faellig > request.Date.AddYears(MaxFristJahre))
                return "Das Ablaufdatum liegt zu weit entfernt.";
        }

        return null;
    }
}
