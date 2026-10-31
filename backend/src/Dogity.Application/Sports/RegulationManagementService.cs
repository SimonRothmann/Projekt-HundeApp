using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Community;
using Dogity.Domain.Sports;
using Microsoft.EntityFrameworkCore;
using static Dogity.Application.Common.Texte;

namespace Dogity.Application.Sports;

public class RegulationManagementService(IApplicationDbContext db, TimeProvider timeProvider) : IRegulationManagementService
{
    public async Task<Result<SportDto>> UpdateSportAsync(Guid actingUserId, bool isAdmin, Guid sportId, UpdateSportRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<SportDto>.Failure("Name ist erforderlich.");

        var nameEnFehler = Textlaengen.ZuLang(request.NameEn, Textlaengen.SportNameEn, "Der englische Name");
        if (nameEnFehler is not null)
            return Result<SportDto>.Failure(nameEnFehler);

        var sport = await db.Sports.FirstOrDefaultAsync(s => s.Id == sportId, ct);
        if (sport is null)
            return Result<SportDto>.NotFound("Sportart nicht gefunden.");

        var authError = await AuthorizeAsync(actingUserId, isAdmin, sport.ClubId, ct);
        if (authError is not null)
            return Result<SportDto>.Failure(authError);

        sport.Name = request.Name.Trim();
        sport.NameEn = NullIfBlank(request.NameEn);
        sport.Description = NullIfBlank(request.Description);
        await db.SaveChangesAsync(ct);

        return Result<SportDto>.Success(new SportDto(sport.Id, sport.Code, sport.Name, sport.Description, sport.ClubId, sport.NameEn));
    }

    public async Task<Result<RegulationDto>> UpdateRegulationAsync(Guid actingUserId, bool isAdmin, Guid regulationId, UpdateRegulationRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<RegulationDto>.Failure("Name ist erforderlich.");

        var enFehler = Textlaengen.ZuLang(request.NameEn, Textlaengen.OrdnungNameEn, "Der englische Name")
            ?? Textlaengen.ZuLang(request.DescriptionEn, Textlaengen.OrdnungBeschreibungEn, "Die englische Beschreibung");
        if (enFehler is not null)
            return Result<RegulationDto>.Failure(enFehler);

        var regulation = await db.Regulations.FirstOrDefaultAsync(r => r.Id == regulationId, ct);
        if (regulation is null)
            return Result<RegulationDto>.NotFound("Prüfungsordnung nicht gefunden.");

        var (global, authError) = await AuthorizeForSportAsync(actingUserId, isAdmin, regulation.SportId, ct);
        if (authError is not null)
            return Result<RegulationDto>.Failure(authError);

        var name = request.Name.Trim();
        var nameEn = NullIfBlank(request.NameEn);
        var beschreibung = NullIfBlank(request.Description);
        var beschreibungEn = NullIfBlank(request.DescriptionEn);

        // Ab einer echten Änderung der vom Katalog gepflegten Felder hat die
        // Handfassung Vorrang: Der Seeder lässt die Ordnung dann in Ruhe (nur
        // bei globalen - Vereinsordnungen kennt der Seeder nicht).
        if (global && (regulation.Name != name || regulation.NameEn != nameEn
            || regulation.Description != beschreibung || regulation.DescriptionEn != beschreibungEn))
            regulation.EditedAt = DateTimeOffset.UtcNow;

        regulation.Name = name;
        regulation.NameEn = nameEn;
        regulation.Description = beschreibung;
        regulation.DescriptionEn = beschreibungEn;
        regulation.SourceUrl = NullIfBlank(request.SourceUrl);

        // Leer heißt hier ausdrücklich "gilt überall" und nicht "unverändert" -
        // sonst ließe sich eine einmal gesetzte Landeszuordnung nie wieder
        // aufheben.
        var land = NullIfBlank(request.CountryCode)?.ToUpperInvariant();
        if (land is { Length: not 2 })
            return Result<RegulationDto>.Failure("Länderkürzel muss zwei Buchstaben haben (ISO 3166-1 alpha-2).");
        regulation.CountryCode = land;

        await db.SaveChangesAsync(ct);

        // Die Fassung wird hier nicht verändert, gehört aber zur Antwort. Sie
        // eigens nachzuschlagen kostet eine Abfrage auf einer Admin-Aktion und
        // hält dafür die Bedeutung von null überall gleich: "es gibt keine
        // Fassung" - und nicht "an dieser Stelle nicht geladen".
        var gueltigAb = (await db.AktuelleFassungAsync(regulationId, Vereinszeit.Tag(timeProvider.GetUtcNow()), ct))?.ValidFrom;

        return Result<RegulationDto>.Success(new RegulationDto(
            regulation.Id, regulation.Name, regulation.SourceUrl, regulation.LastSyncedAt, regulation.LatestKnownVersionLabel, regulation.Description, regulation.CountryCode, gueltigAb, regulation.NameEn, regulation.DescriptionEn, regulation.EditedAt, regulation.SeedKey));
    }

    public async Task<Result<RegulationExerciseDto>> AddRegulationExerciseAsync(Guid actingUserId, bool isAdmin, Guid regulationId, AddRegulationExerciseRequest request, CancellationToken ct = default)
    {
        if (request.MaxPoints < 0)
            return Result<RegulationExerciseDto>.Failure("Punkte dürfen nicht negativ sein.");

        var hinweisEnFehler = Textlaengen.ZuLang(request.ScoringNotesEn, Textlaengen.OrdnungsuebungHinweisEn, "Der englische Bewertungshinweis");
        if (hinweisEnFehler is not null)
            return Result<RegulationExerciseDto>.Failure(hinweisEnFehler);

        var (version, global, authError) = await GetEditableVersionAsync(actingUserId, isAdmin, regulationId, ct);
        if (authError is not null)
            return Result<RegulationExerciseDto>.Failure(authError);
        if (version is null)
            return Result<RegulationExerciseDto>.Failure("Keine gültige Version für diese Prüfungsordnung gefunden.");

        var exercise = await db.Exercises.FirstOrDefaultAsync(e => e.Id == request.ExerciseId, ct);
        if (exercise is null)
            return Result<RegulationExerciseDto>.NotFound("Übung nicht gefunden.");

        // Auch entfernte Zeilen ansehen - sonst scheitert das erneute
        // Hinzufügen einer zuvor entfernten Übung am eindeutigen Index
        // (Soft-Delete, siehe RegulationExerciseQueries).
        var existing = await db.FindLinkIncludingRemovedAsync(version.Id, request.ExerciseId, ct);
        if (existing is { DeletedAt: null })
            return Result<RegulationExerciseDto>.Failure("Diese Übung ist bereits Teil der Prüfungsordnung.");

        // Von Hand ergänzte Übungen hängen sich ans Ende der Prüfungsordnung -
        // wo genau sie fachlich hingehören, weiß nur der Seed, und eine
        // eingeschobene Übung würde die dort gepflegte Reihenfolge zerreißen.
        var lastSortOrder = await db.RegulationExercises
            .Where(re => re.RegulationVersionId == version.Id)
            .Select(re => (int?)re.SortOrder)
            .MaxAsync(ct) ?? -1;

        RegulationExercise link;
        if (existing is not null)
        {
            link = existing;
            link.DeletedAt = null;
            link.SortOrder = lastSortOrder + 1;
        }
        else
        {
            link = new RegulationExercise
            {
                RegulationVersionId = version.Id,
                ExerciseId = request.ExerciseId,
                SortOrder = lastSortOrder + 1
            };
            db.RegulationExercises.Add(link);
        }

        link.IsMandatory = request.IsMandatory;
        link.MaxPoints = request.MaxPoints;
        link.ScoringNotes = NullIfBlank(request.ScoringNotes);
        link.ScoringNotesEn = NullIfBlank(request.ScoringNotesEn);
        // Eine von Hand ergänzte oder wieder hinzugefügte Zeile belebt der Seeder
        // nicht um und räumt sie auch nicht als "nicht mehr im Seed" weg.
        if (global)
            link.EditedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return Result<RegulationExerciseDto>.Success(new RegulationExerciseDto(
            exercise.Id, exercise.Name, link.IsMandatory, link.MaxPoints, link.ScoringNotes, exercise.NameEn, link.ScoringNotesEn, link.EditedAt));
    }

    public async Task<Result> UpdateRegulationExerciseAsync(Guid actingUserId, bool isAdmin, Guid regulationId, Guid exerciseId, UpdateRegulationExerciseRequest request, CancellationToken ct = default)
    {
        if (request.MaxPoints < 0)
            return Result.Failure("Punkte dürfen nicht negativ sein.");

        var hinweisEnFehler = Textlaengen.ZuLang(request.ScoringNotesEn, Textlaengen.OrdnungsuebungHinweisEn, "Der englische Bewertungshinweis");
        if (hinweisEnFehler is not null)
            return Result.Failure(hinweisEnFehler);

        var (version, global, authError) = await GetEditableVersionAsync(actingUserId, isAdmin, regulationId, ct);
        if (authError is not null)
            return Result.Failure(authError);
        if (version is null)
            return Result.Failure("Keine gültige Version für diese Prüfungsordnung gefunden.");

        var link = await db.RegulationExercises.FirstOrDefaultAsync(
            re => re.RegulationVersionId == version.Id && re.ExerciseId == exerciseId, ct);
        if (link is null)
            return Result.Failure("Übung ist nicht Teil dieser Prüfungsordnung.");

        var hinweis = NullIfBlank(request.ScoringNotes);
        var hinweisEn = NullIfBlank(request.ScoringNotesEn);
        // Nur eine echte Änderung zählt als Handpflege (siehe UpdateRegulationAsync).
        if (global && (link.IsMandatory != request.IsMandatory || link.MaxPoints != request.MaxPoints
            || link.ScoringNotes != hinweis || link.ScoringNotesEn != hinweisEn))
            link.EditedAt = DateTimeOffset.UtcNow;

        // SortOrder bleibt bewusst unberührt: Punkte oder Pflicht-Kennzeichen zu
        // ändern darf die Reihenfolge der Prüfungsordnung nicht verwerfen.
        link.IsMandatory = request.IsMandatory;
        link.MaxPoints = request.MaxPoints;
        link.ScoringNotes = hinweis;
        link.ScoringNotesEn = hinweisEn;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RemoveRegulationExerciseAsync(Guid actingUserId, bool isAdmin, Guid regulationId, Guid exerciseId, CancellationToken ct = default)
    {
        var (version, global, authError) = await GetEditableVersionAsync(actingUserId, isAdmin, regulationId, ct);
        if (authError is not null)
            return Result.Failure(authError);
        if (version is null)
            return Result.Failure("Keine gültige Version für diese Prüfungsordnung gefunden.");

        var link = await db.RegulationExercises.FirstOrDefaultAsync(
            re => re.RegulationVersionId == version.Id && re.ExerciseId == exerciseId, ct);
        if (link is null)
            return Result.Failure("Übung ist nicht Teil dieser Prüfungsordnung.");

        // Soft-Delete (RegulationExercise hat einen DeletedAt-QueryFilter) - die
        // Übung selbst bleibt im Katalog, nur die Zuordnung zur PO wird entfernt.
        // EditedAt dazu, damit der Seeder die Zeile nicht wiederbelebt.
        link.DeletedAt = DateTimeOffset.UtcNow;
        if (global)
            link.EditedAt = link.DeletedAt;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RevertRegulationAsync(Guid actingUserId, bool isAdmin, Guid regulationId, CancellationToken ct = default)
    {
        var regulation = await db.Regulations.FirstOrDefaultAsync(r => r.Id == regulationId, ct);
        if (regulation is null)
            return Result.NotFound("Prüfungsordnung nicht gefunden.");

        var (global, authError) = await AuthorizeForSportAsync(actingUserId, isAdmin, regulation.SportId, ct);
        if (authError is not null)
            return Result.Failure(authError);
        if (!global)
            return Result.Failure("Nur globale Prüfungsordnungen werden aus dem Katalog gepflegt.");
        if (regulation.EditedAt is null)
            return Result.Failure("Diese Prüfungsordnung wurde nicht von Hand geändert.");

        // Die englischen Texte gehören zur Handfassung: Leer, füllt der Seeder sie
        // beim nächsten Start wieder aus dem Katalog (er setzt nur leere Felder).
        regulation.EditedAt = null;
        regulation.NameEn = null;
        regulation.DescriptionEn = null;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RevertRegulationExerciseAsync(Guid actingUserId, bool isAdmin, Guid regulationId, Guid exerciseId, CancellationToken ct = default)
    {
        var (version, global, authError) = await GetEditableVersionAsync(actingUserId, isAdmin, regulationId, ct);
        if (authError is not null)
            return Result.Failure(authError);
        if (version is null)
            return Result.Failure("Keine gültige Version für diese Prüfungsordnung gefunden.");
        if (!global)
            return Result.Failure("Nur globale Prüfungsordnungen werden aus dem Katalog gepflegt.");

        // Auch eine von Hand entfernte Zeile: Sie soll sich zurücknehmen lassen.
        var link = await db.FindLinkIncludingRemovedAsync(version.Id, exerciseId, ct);
        if (link is null)
            return Result.Failure("Übung ist nicht Teil dieser Prüfungsordnung.");
        if (link.EditedAt is null)
            return Result.Failure("Diese Zuordnung wurde nicht von Hand geändert.");

        link.EditedAt = null;
        link.ScoringNotesEn = null; // der Seeder setzt es mit dem Katalogtext neu
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ----- Helpers -----

    private async Task<(RegulationVersion? Version, bool Global, string? Error)> GetEditableVersionAsync(Guid actingUserId, bool isAdmin, Guid regulationId, CancellationToken ct)
    {
        var regulation = await db.Regulations.FirstOrDefaultAsync(r => r.Id == regulationId, ct);
        if (regulation is null)
            return (null, false, "Prüfungsordnung nicht gefunden.");

        var (global, authError) = await AuthorizeForSportAsync(actingUserId, isAdmin, regulation.SportId, ct);
        if (authError is not null)
            return (null, global, authError);

        // Aktuelle Version = die gültige Fassung (wie GetRegulationDetailAsync).
        var version = await db.AktuelleFassungAsync(regulationId, Vereinszeit.Tag(timeProvider.GetUtcNow()), ct);
        return (version, global, null);
    }

    // Global: Die Sportart gehört keinem Verein, ihre Ordnungen pflegt der Seeder mit.
    private async Task<(bool Global, string? Error)> AuthorizeForSportAsync(Guid actingUserId, bool isAdmin, Guid sportId, CancellationToken ct)
    {
        var sport = await db.Sports.FirstOrDefaultAsync(s => s.Id == sportId, ct);
        if (sport is null)
            return (false, "Sportart nicht gefunden.");
        return (sport.ClubId is null, await AuthorizeAsync(actingUserId, isAdmin, sport.ClubId, ct));
    }

    private async Task<string?> AuthorizeAsync(Guid actingUserId, bool isAdmin, Guid? clubId, CancellationToken ct)
    {
        if (clubId is null)
            return isAdmin ? null : "Nur Admins dürfen globale Sportarten und Prüfungsordnungen bearbeiten.";

        var isClubTrainer = await db.IsClubTrainerAsync(actingUserId, clubId.Value, ct);
        return isClubTrainer ? null : "Du bist für diesen Verein nicht als Trainer eingetragen.";
    }
}
