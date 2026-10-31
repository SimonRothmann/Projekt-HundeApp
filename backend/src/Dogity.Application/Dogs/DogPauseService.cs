using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Community;
using Dogity.Domain.Dogs;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Dogs;

/// <inheritdoc />
public class DogPauseService(IApplicationDbContext db, TimeProvider timeProvider) : IDogPauseService
{
    // Grenzen gegen Tippfehler wie das Jahr 2206: weit genug für nachgetragene
    // Pausen und Voranmeldungen, eng genug, damit eine Datumsrechnung nie
    // an den Rand von DateOnly stößt.
    private const int MaxJahreZurueck = 5;
    private const int MaxJahreVoraus = 2;
    private const int MaxDauerJahre = 2;

    public async Task<Result<IReadOnlyList<DogPauseDto>>> GetAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        var istBesitzer = await db.IstBesitzerAsync(userId, dogId, ct);
        // Wer den Hund nur betreut, sieht nur laufende und kommende Pausen - ohne
        // Grund und Notiz. Den Verlauf vergangener Pausen braucht die Trainer:in
        // nicht (er ist Sache der Familie); der Plan trägt die für ihn nötigen
        // Zeiträume selbst (GoalDto.PlanPausen).
        if (!istBesitzer && !await db.HasDogAccessAsync(userId, dogId, ct))
            return Result<IReadOnlyList<DogPauseDto>>.NotFound("Hund nicht gefunden.");

        var heute = Heute();
        var pausen = await db.DogPauses
            .Where(p => p.DogId == dogId && (istBesitzer || p.EndDate == null || p.EndDate >= heute))
            .OrderByDescending(p => p.StartDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return Result<IReadOnlyList<DogPauseDto>>.Success(
            pausen.Select(p => DogPauseQueries.ToDto(p, zeigeDetails: istBesitzer)).ToList());
    }

    public async Task<Result<DogPauseDto>> CreateAsync(Guid userId, Guid dogId, SaveDogPauseRequest request, CancellationToken ct = default)
    {
        if (!await db.IstBesitzerAsync(userId, dogId, ct))
            return Result<DogPauseDto>.NotFound("Hund nicht gefunden.");

        var fehler = await PruefeAsync(dogId, request, ignoriereId: null, ct);
        if (fehler is not null) return Result<DogPauseDto>.Failure(fehler);

        var pause = new DogPause
        {
            DogId = dogId,
            Reason = request.Reason,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Note = Texte.NullIfBlank(request.Note),
            CreatedByUserId = userId,
        };
        db.DogPauses.Add(pause);
        await db.SaveChangesAsync(ct);

        return Result<DogPauseDto>.Success(DogPauseQueries.ToDto(pause, zeigeDetails: true));
    }

    public async Task<Result<DogPauseDto>> UpdateAsync(Guid userId, Guid dogId, Guid pauseId, SaveDogPauseRequest request, CancellationToken ct = default)
    {
        if (!await db.IstBesitzerAsync(userId, dogId, ct))
            return Result<DogPauseDto>.NotFound("Hund nicht gefunden.");

        var pause = await db.DogPauses.FirstOrDefaultAsync(p => p.Id == pauseId && p.DogId == dogId, ct);
        if (pause is null)
            return Result<DogPauseDto>.NotFound("Pause nicht gefunden.");

        var fehler = await PruefeAsync(dogId, request, ignoriereId: pauseId, ct);
        if (fehler is not null) return Result<DogPauseDto>.Failure(fehler);

        pause.Reason = request.Reason;
        pause.StartDate = request.StartDate;
        pause.EndDate = request.EndDate;
        pause.Note = Texte.NullIfBlank(request.Note);
        pause.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);

        return Result<DogPauseDto>.Success(DogPauseQueries.ToDto(pause, zeigeDetails: true));
    }

    public async Task<Result> EndAsync(Guid userId, Guid dogId, Guid pauseId, CancellationToken ct = default)
    {
        if (!await db.IstBesitzerAsync(userId, dogId, ct))
            return Result.NotFound("Hund nicht gefunden.");

        var pause = await db.DogPauses.FirstOrDefaultAsync(p => p.Id == pauseId && p.DogId == dogId, ct);
        if (pause is null)
            return Result.NotFound("Pause nicht gefunden.");

        var heute = Heute();
        if (pause.EndDate is { } ende && ende < heute)
            return Result.Failure("Diese Pause ist schon zu Ende.");

        if (pause.StartDate >= heute)
        {
            // Sie hat noch nie gewirkt: Ein Eintrag "gestern bis vorgestern"
            // im Verlauf wäre falsch, ein Ende vor dem Beginn ungültig.
            pause.DeletedAt = timeProvider.GetUtcNow();
        }
        else
        {
            // Der letzte Pausentag ist gestern - ab heute ist der Hund wieder dabei.
            pause.EndDate = heute.AddDays(-1);
            pause.UpdatedAt = timeProvider.GetUtcNow();
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid dogId, Guid pauseId, CancellationToken ct = default)
    {
        if (!await db.IstBesitzerAsync(userId, dogId, ct))
            return Result.NotFound("Hund nicht gefunden.");

        var pause = await db.DogPauses.FirstOrDefaultAsync(p => p.Id == pauseId && p.DogId == dogId, ct);
        if (pause is null)
            return Result.NotFound("Pause nicht gefunden.");

        pause.DeletedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private DateOnly Heute() => Vereinszeit.Tag(timeProvider.GetUtcNow());

    /// <summary>Die Fehlermeldung zur Eingabe, sonst null.</summary>
    private async Task<string?> PruefeAsync(Guid dogId, SaveDogPauseRequest request, Guid? ignoriereId, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Reason))
            return "Bitte einen Grund für die Pause wählen.";

        if (request.Reason == DogPauseReason.InHeat)
        {
            var geschlecht = await db.Dogs.Where(d => d.Id == dogId).Select(d => (DogGender?)d.Gender).FirstOrDefaultAsync(ct);
            if (geschlecht != DogGender.Female)
                return "Läufigkeit gibt es nur bei Hündinnen.";
        }

        var heute = Heute();
        if (request.StartDate < heute.AddYears(-MaxJahreZurueck) || request.StartDate > heute.AddYears(MaxJahreVoraus))
            return "Der Beginn der Pause liegt zu weit entfernt.";

        if (request.EndDate is { } ende)
        {
            if (ende < request.StartDate)
                return "Das Ende der Pause darf nicht vor ihrem Beginn liegen.";
            if (ende > request.StartDate.AddYears(MaxDauerJahre))
                return "Die Pause ist zu lang - für eine unbestimmte Dauer das Ende offen lassen.";
        }

        // Überschneidung (beide Grenzen einschließlich; ein offenes Ende reicht
        // unbegrenzt): Mit zwei gleichzeitigen Pausen wüsste niemand, welcher
        // Grund gilt, und die Planrechnung zählte Tage doppelt. Eine neue Pause
        // beendet eine alte nicht von selbst - das soll die Person bewusst tun.
        // Die Prüfung ist nur anwendungsseitig: Zwei gleichzeitige Anfragen
        // (etwa von zwei Mitbesitzer:innen) könnten trotzdem beide durchkommen.
        // Das ist bewusst akzeptiert - die Planrechnung vereinigt die Zeiträume
        // (Trainingspausen.PausentageZwischen), und ActivePausesAsync nimmt die
        // jüngste; ein Exclusion-Constraint wäre für diesen Fall zu viel.
        var neuesEnde = request.EndDate;
        var ueberschneidet = await db.DogPauses.AnyAsync(p =>
            p.DogId == dogId
            && p.Id != ignoriereId
            && p.StartDate <= (neuesEnde ?? DateOnly.MaxValue)
            && (p.EndDate == null || p.EndDate >= request.StartDate), ct);
        if (ueberschneidet)
            return "In diesem Zeitraum gibt es schon eine Pause - beende oder ändere sie zuerst.";

        return Textlaengen.ZuLang(request.Note, Textlaengen.PausenNotiz, "Die Notiz");
    }
}
