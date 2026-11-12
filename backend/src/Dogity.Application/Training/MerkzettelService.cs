using System.Data.Common;
using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Community;
using Dogity.Application.Reminders;
using Dogity.Domain.Dogs;
using Dogity.Domain.Training;
using Microsoft.EntityFrameworkCore;
using static Dogity.Application.Common.SqlStates;

namespace Dogity.Application.Training;

/// <inheritdoc />
public class MerkzettelService(IApplicationDbContext db, TimeProvider timeProvider) : IMerkzettelService
{
    // Ein Datum weit in der Vergangenheit ist ein Tippfehler; ein wenig Spielraum bleibt für einen Eintrag,
    // der offline gemerkt und erst später synchronisiert wurde.
    private const int MaxTageZurueck = 7;
    private const int MaxTageVoraus = 366;

    public async Task<Result<IReadOnlyList<TrainingNoteDto>>> GetAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        if (!await db.IstBesitzerAsync(userId, dogId, ct))
            return Result<IReadOnlyList<TrainingNoteDto>>.NotFound("Hund nicht gefunden.");

        var eintraege = await db.TrainingNotes
            .Where(n => n.DogId == dogId && n.DoneAt == null)
            .OrderBy(n => n.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        return Result<IReadOnlyList<TrainingNoteDto>>.Success(await ToDtosAsync(userId, dogId, eintraege, ct));
    }

    public async Task<Result<TrainingNoteDto>> CreateAsync(Guid userId, Guid dogId, SaveTrainingNoteRequest request, CancellationToken ct = default)
    {
        if (!await db.IstBesitzerAsync(userId, dogId, ct))
            return Result<TrainingNoteDto>.NotFound("Hund nicht gefunden.");

        // Idempotenz für die Offline-Warteschlange: Wurde dieselbe vom Gerät vergebene Id schon
        // synchronisiert (erneuter Versuch nach verlorener Antwort), nicht doppelt anlegen.
        if (request.Id is { } id && await VorhandenerEintragAsync(userId, dogId, id, ct) is { } vorhanden)
            return vorhanden;

        var fehler = await PruefeAsync(request, ct);
        if (fehler is not null) return Result<TrainingNoteDto>.Failure(fehler);

        // CreatedAt von der Uhr des Dienstes: Wann gemerkt wurde, entscheidet über das Ziel "Nächstes Training".
        var eintrag = new TrainingNote { DogId = dogId, UserId = userId, CreatedAt = timeProvider.GetUtcNow() };
        if (request.Id is { } neueId) eintrag.Id = neueId;
        Uebernehme(eintrag, request);
        db.TrainingNotes.Add(eintrag);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (request.Id is not null
            && ex.InnerException is DbException { SqlState: EindeutigerIndexVerletzt })
        {
            // Dieselbe Geräte-Id zweimal gleichzeitig (Wiederholung, während die erste Antwort noch läuft):
            // Die andere war schneller, ihr Eintrag ist der eine gültige.
            if (await VorhandenerEintragAsync(userId, dogId, request.Id.Value, ct) is not { } schnellere) throw;
            return schnellere;
        }

        return Result<TrainingNoteDto>.Success(await ToDtoAsync(userId, eintrag, ct));
    }

    /// <summary>
    /// Der schon unter dieser Geräte-Id angelegte Eintrag, null wenn es keinen gibt. Auch ein inzwischen
    /// gelöschter zählt (sonst kollidierte der Schlüssel); gehört er zu einem anderen Hund, ist es ein 404.
    /// </summary>
    private async Task<Result<TrainingNoteDto>?> VorhandenerEintragAsync(Guid userId, Guid dogId, Guid id, CancellationToken ct)
    {
        var vorhanden = await db.TrainingNotes.IgnoreQueryFilters()
            .AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct);
        if (vorhanden is null) return null;
        return vorhanden.DogId == dogId
            ? Result<TrainingNoteDto>.Success((await ToDtosAsync(userId, dogId, [vorhanden], ct))[0])
            : Result<TrainingNoteDto>.NotFound("Hund nicht gefunden.");
    }

    public async Task<Result<TrainingNoteDto>> UpdateAsync(Guid userId, Guid dogId, Guid noteId, SaveTrainingNoteRequest request, CancellationToken ct = default)
    {
        if (!await db.IstBesitzerAsync(userId, dogId, ct))
            return Result<TrainingNoteDto>.NotFound("Hund nicht gefunden.");

        var eintrag = await db.TrainingNotes.FirstOrDefaultAsync(n => n.Id == noteId && n.DogId == dogId, ct);
        if (eintrag is null)
            return Result<TrainingNoteDto>.NotFound("Eintrag nicht gefunden.");

        var fehler = await PruefeAsync(request, ct);
        if (fehler is not null) return Result<TrainingNoteDto>.Failure(fehler);

        Uebernehme(eintrag, request);
        eintrag.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);

        return Result<TrainingNoteDto>.Success(await ToDtoAsync(userId, eintrag, ct));
    }

    public async Task<Result<TrainingNoteDto>> SetDoneAsync(Guid userId, Guid dogId, Guid noteId, bool done, CancellationToken ct = default)
    {
        if (!await db.IstBesitzerAsync(userId, dogId, ct))
            return Result<TrainingNoteDto>.NotFound("Hund nicht gefunden.");

        var eintrag = await db.TrainingNotes.FirstOrDefaultAsync(n => n.Id == noteId && n.DogId == dogId, ct);
        if (eintrag is null)
            return Result<TrainingNoteDto>.NotFound("Eintrag nicht gefunden.");

        eintrag.DoneAt = done ? timeProvider.GetUtcNow() : null;
        eintrag.DoneInTrainingId = null;
        eintrag.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);

        return Result<TrainingNoteDto>.Success(await ToDtoAsync(userId, eintrag, ct));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid dogId, Guid noteId, CancellationToken ct = default)
    {
        if (!await db.IstBesitzerAsync(userId, dogId, ct))
            return Result.NotFound("Hund nicht gefunden.");

        var eintrag = await db.TrainingNotes.FirstOrDefaultAsync(n => n.Id == noteId && n.DogId == dogId, ct);
        if (eintrag is null)
            return Result.NotFound("Eintrag nicht gefunden.");

        eintrag.DeletedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<NextTrainingDto>> GetNextTrainingAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        if (!await db.IstBesitzerAsync(userId, dogId, ct))
            return Result<NextTrainingDto>.NotFound("Hund nicht gefunden.");

        return Result<NextTrainingDto>.Success(await NaechstesTrainingAsync(userId, dogId, ct));
    }

    public async Task<Result<NextTrainingDto>> SetRhythmAsync(Guid userId, Guid dogId, SaveTrainingRhythmRequest request, CancellationToken ct = default)
    {
        if (!await db.IstBesitzerAsync(userId, dogId, ct))
            return Result<NextTrainingDto>.NotFound("Hund nicht gefunden.");

        if (!Erinnerungsregeln.GueltigeTage(request.Days))
            return Result<NextTrainingDto>.Failure("Bitte mindestens einen Wochentag wählen.");
        if (request.Hour is { } stunde && stunde is < 0 or > 23)
            return Result<NextTrainingDto>.Failure("Die Uhrzeit ist ungültig.");

        var rhythmus = await db.DogTrainingRhythms.FirstOrDefaultAsync(r => r.DogId == dogId, ct);
        if (rhythmus is null)
        {
            rhythmus = new DogTrainingRhythm { DogId = dogId };
            db.DogTrainingRhythms.Add(rhythmus);
        }
        else
        {
            rhythmus.UpdatedAt = timeProvider.GetUtcNow();
        }
        rhythmus.Days = request.Days;
        rhythmus.Hour = request.Hour;
        await db.SaveChangesAsync(ct);

        return Result<NextTrainingDto>.Success(await NaechstesTrainingAsync(userId, dogId, ct));
    }

    private async Task<NextTrainingDto> NaechstesTrainingAsync(Guid userId, Guid dogId, CancellationToken ct)
    {
        var jetzt = timeProvider.GetUtcNow();
        var daten = await TrainingsDaten.LadenAsync(db, [dogId], [userId], Vereinszeit.Tag(jetzt), ct);
        var naechstes = Trainingsrhythmus.NaechstesTraining(jetzt, daten.Tagebuch(dogId), daten.Gruppentermine(userId), daten.Rhythmus(dogId));
        return naechstes is { } termin
            ? new NextTrainingDto(termin.Tag, termin.Uhrzeit, termin.Quelle)
            : new NextTrainingDto(null, null, TrainingsQuelle.Unbekannt);
    }

    private async Task<TrainingNoteDto> ToDtoAsync(Guid userId, TrainingNote eintrag, CancellationToken ct) =>
        (await ToDtosAsync(userId, eintrag.DogId, [eintrag], ct))[0];

    private async Task<List<TrainingNoteDto>> ToDtosAsync(Guid userId, Guid dogId, IReadOnlyList<TrainingNote> eintraege, CancellationToken ct)
    {
        var jetzt = timeProvider.GetUtcNow();
        var heute = Vereinszeit.Tag(jetzt);
        var daten = await TrainingsDaten.LadenAsync(db, [dogId], [userId], heute, ct);

        var uebungIds = eintraege.Where(n => n.ExerciseId is not null).Select(n => n.ExerciseId!.Value).Distinct().ToList();
        var uebungen = uebungIds.Count == 0
            ? []
            : await db.Exercises.Where(e => uebungIds.Contains(e.Id)).Select(e => new { e.Id, e.Name, e.NameEn }).AsNoTracking().ToDictionaryAsync(e => e.Id, ct);

        return eintraege.Select(n =>
        {
            // Erledigte haben kein Ziel mehr, das sich ausrechnen ließe.
            var ziel = n.DoneAt is null
                ? Trainingsrhythmus.ZielDesEintrags(
                    n.Goal, n.TargetDate, n.CreatedAt, jetzt, daten.Tagebuch(dogId), daten.Gruppentermine(userId), daten.Rhythmus(dogId),
                    erinnerung: false)
                : null;
            var uebung = n.ExerciseId is { } id && uebungen.TryGetValue(id, out var u) ? u : null;
            return new TrainingNoteDto(
                n.Id, n.DogId, n.Text, n.ExerciseId, uebung?.Name, uebung?.NameEn, n.Goal,
                ziel?.Tag, ziel?.Uhrzeit, n.DoneAt, n.DoneInTrainingId, n.CreatedAt);
        }).ToList();
    }

    private void Uebernehme(TrainingNote eintrag, SaveTrainingNoteRequest request)
    {
        eintrag.Text = request.Text.Trim();
        eintrag.ExerciseId = request.ExerciseId;
        // "Nächste Woche" bleibt beim Ändern des Textes bei ihrer Woche: Den Montag nur neu setzen,
        // wenn das Ziel neu ist, sonst schöbe jede Korrektur den Eintrag um eine Woche weiter.
        var bleibtNaechsteWoche = eintrag.Goal == TrainingNoteGoal.NextWeek && eintrag.TargetDate is not null;
        eintrag.Goal = request.Goal;
        eintrag.TargetDate = request.Goal switch
        {
            TrainingNoteGoal.Date => request.TargetDate,
            // Den Montag der Folgewoche legt der Server fest: Er rechnet nach Vereinszeit, nicht nach der Uhr des Geräts.
            TrainingNoteGoal.NextWeek => bleibtNaechsteWoche
                ? eintrag.TargetDate
                : Trainingsrhythmus.MontagDerFolgewoche(Vereinszeit.Tag(timeProvider.GetUtcNow())),
            _ => null
        };
    }

    /// <summary>Die Fehlermeldung zur Eingabe, sonst null.</summary>
    private async Task<string?> PruefeAsync(SaveTrainingNoteRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return "Bitte etwas zum Merken eintragen.";
        var zuLang = Textlaengen.ZuLang(request.Text, Textlaengen.MerkzettelText, "Der Eintrag");
        if (zuLang is not null) return zuLang;

        if (!Enum.IsDefined(request.Goal))
            return "Bitte wählen, wann es drankommen soll.";

        if (request.Goal == TrainingNoteGoal.Date)
        {
            var heute = Vereinszeit.Tag(timeProvider.GetUtcNow());
            if (request.TargetDate is not { } tag)
                return "Bitte ein Datum wählen.";
            if (tag < heute.AddDays(-MaxTageZurueck) || tag > heute.AddDays(MaxTageVoraus))
                return "Das Datum liegt zu weit entfernt.";
        }

        if (request.ExerciseId is { } uebungId && !await db.Exercises.AnyAsync(e => e.Id == uebungId, ct))
            return "Übung nicht gefunden.";

        return null;
    }
}
