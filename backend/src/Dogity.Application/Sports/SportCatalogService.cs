using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Sports;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Sports;

public class SportCatalogService(IApplicationDbContext db) : ISportCatalogService
{
    public async Task<Result<IReadOnlyList<ExerciseDto>>> GetExercisesAsync(Guid sportId, Guid userId, CancellationToken ct = default)
    {
        var exists = await db.Sports.AnyAsync(s => s.Id == sportId, ct);
        if (!exists)
            return Result<IReadOnlyList<ExerciseDto>>.NotFound("Sportart nicht gefunden.");

        var visibleClubIds = await db.GetVisibleClubIdsAsync(userId, ct);

        var exercises = await db.Exercises
            .Where(e => e.SportId == sportId)
            .Where(e => e.ClubId == null || visibleClubIds.Contains(e.ClubId.Value))
            .OrderBy(e => e.Name)
            .Select(e => new ExerciseDto(e.Id, e.SportId, e.Name, e.Description, e.Difficulty, e.Category, e.ScoringCriteria, e.ClubId))
            .ToListAsync(ct);

        return Result<IReadOnlyList<ExerciseDto>>.Success(exercises);
    }

    public async Task<Result<IReadOnlyList<ExerciseDto>>> GetUncategorizedExercisesAsync(Guid userId, CancellationToken ct = default)
    {
        var visibleClubIds = await db.GetVisibleClubIdsAsync(userId, ct);
        var exercises = await db.Exercises
            .Where(e => e.SportId == null)
            .Where(e => e.ClubId == null || visibleClubIds.Contains(e.ClubId.Value))
            .OrderBy(e => e.Name)
            .Select(e => new ExerciseDto(e.Id, e.SportId, e.Name, e.Description, e.Difficulty, e.Category, e.ScoringCriteria, e.ClubId))
            .ToListAsync(ct);
        return Result<IReadOnlyList<ExerciseDto>>.Success(exercises);
    }

    public async Task<Result<IReadOnlyList<SportDto>>> GetSportsAsync(Guid? userId = null, CancellationToken ct = default)
    {
        IReadOnlyCollection<Guid> visibleClubIds = userId is { } uid
            ? await db.GetVisibleClubIdsAsync(uid, ct)
            : Array.Empty<Guid>();

        var sports = await db.Sports
            .Where(s => s.ClubId == null || visibleClubIds.Contains(s.ClubId.Value))
            .OrderBy(s => s.Name)
            .Select(s => new SportDto(s.Id, s.Code, s.Name, s.Description, s.ClubId))
            .ToListAsync(ct);

        return Result<IReadOnlyList<SportDto>>.Success(sports);
    }

    public async Task<Result<SportDto>> CreateSportAsync(Guid actingUserId, bool isAdmin, CreateSportRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            return Result<SportDto>.Failure("Code und Name sind erforderlich.");

        if (request.ClubId is null)
        {
            if (!isAdmin) return Result<SportDto>.Failure("Nur Admins dürfen globale Sportarten anlegen.");
        }
        else
        {
            var isClubTrainer = await db.IsClubTrainerAsync(actingUserId, request.ClubId.Value, ct);
            if (!isClubTrainer) return Result<SportDto>.Failure("Du bist für diesen Verein nicht als Trainer eingetragen.");
        }

        var code = request.Code.Trim();
        var codeTaken = await db.Sports.AnyAsync(s => s.Code == code && s.ClubId == request.ClubId, ct);
        if (codeTaken) return Result<SportDto>.Failure("Ein Sportart-Code muss innerhalb seines Sichtbarkeitsbereichs eindeutig sein.");

        var sport = new Sport
        {
            Code = code,
            Name = request.Name.Trim(),
            Description = request.Description,
            ClubId = request.ClubId,
        };
        db.Sports.Add(sport);
        await db.SaveChangesAsync(ct);

        return Result<SportDto>.Success(new SportDto(sport.Id, sport.Code, sport.Name, sport.Description, sport.ClubId));
    }

    public async Task<Result<IReadOnlyList<RegulationDto>>> GetRegulationsAsync(Guid sportId, string? country = null, CancellationToken ct = default)
    {
        var exists = await db.Sports.AnyAsync(s => s.Id == sportId, ct);
        if (!exists)
            return Result<IReadOnlyList<RegulationDto>>.NotFound("Sportart nicht gefunden.");

        var land = Laendercode(country);

        var regulations = await db.Regulations
            .Where(r => r.SportId == sportId)
            // Ohne Land alles; mit Land die des Landes PLUS die
            // international gültigen. Letztere wegzulassen wäre der
            // naheliegende Fehler: null heißt "gilt überall", also auch hier.
            .Where(r => land == null || r.CountryCode == land || r.CountryCode == null)
            .OrderBy(r => r.Name)
            .Select(r => new RegulationDto(r.Id, r.Name, r.SourceUrl, r.LastSyncedAt, r.LatestKnownVersionLabel, r.Description, r.CountryCode))
            .ToListAsync(ct);

        return Result<IReadOnlyList<RegulationDto>>.Success(regulations);
    }

    public async Task<Result<IReadOnlyList<CountryDto>>> GetCountriesAsync(CancellationToken ct = default)
    {
        // Die Zählung kommt aus den Ordnungen, die Liste aus der Tabelle.
        // Beides aus derselben Quelle zu holen ginge nicht: Ein Land ohne
        // Inhalte käme in den Ordnungen gar nicht vor - und genau die
        // sollen wählbar sein.
        var international = await db.Regulations.CountAsync(r => r.CountryCode == null, ct);

        var jeLand = await db.Regulations
            .Where(r => r.CountryCode != null)
            .GroupBy(r => r.CountryCode!)
            .Select(g => new { Code = g.Key, Anzahl = g.Count() })
            .ToListAsync(ct);

        var laender = await db.Countries
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Code)
            .Select(c => c.Code)
            .ToListAsync(ct);

        return Result<IReadOnlyList<CountryDto>>.Success(laender
            .Select(code => new CountryDto(
                code,
                (jeLand.FirstOrDefault(l => l.Code == code)?.Anzahl ?? 0) + international))
            .ToList());
    }

    /// <summary>
    /// Normalisiert eine Landesangabe aus dem Aufruf: Groß, getrimmt,
    /// oder null. Ein leerer Wert bedeutet "keine Einschränkung" und darf
    /// nicht als Land "" gegen die Datenbank laufen.
    /// </summary>
    private static string? Laendercode(string? wert)
    {
        var getrimmt = wert?.Trim();
        return string.IsNullOrEmpty(getrimmt) ? null : getrimmt.ToUpperInvariant();
    }

    public async Task<Result<RegulationDetailDto>> GetRegulationDetailAsync(Guid regulationId, CancellationToken ct = default)
    {
        var regulation = await db.Regulations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == regulationId, ct);
        if (regulation is null)
            return Result<RegulationDetailDto>.NotFound("Prüfungsordnung nicht gefunden.");

        var currentVersion = await db.RegulationVersions
            .Where(v => v.RegulationId == regulationId)
            .OrderByDescending(v => v.ValidFrom)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
        if (currentVersion is null)
            return Result<RegulationDetailDto>.Failure("Keine gültige Version für diese Prüfungsordnung gefunden.");

        // Reihenfolge der Prüfungsordnung (RegulationExercise.SortOrder), nicht
        // die zufällige Reihenfolge der Datenbank: sonst stand z.B. bei der
        // FCI-IGP 1 das "Sitz mit Abholen" vor der "Leinenführigkeit" und bei
        // der VDH-VK1 lagen die Sprint-Disziplinen zwischen den
        // Gehorsamsübungen. Der Name als zweites Kriterium hält die Ausgabe
        // auch dort stabil, wo mehrere Übungen denselben (oder noch keinen)
        // SortOrder haben - etwa von Hand ergänzte Übungen.
        var exercises = await db.RegulationExercises
            .Where(re => re.RegulationVersionId == currentVersion.Id)
            .OrderBy(re => re.SortOrder)
            .ThenBy(re => re.Exercise!.Name)
            .Select(re => new RegulationExerciseDto(re.ExerciseId, re.Exercise!.Name, re.IsMandatory, re.MaxPoints, re.ScoringNotes))
            .ToListAsync(ct);

        var detail = new RegulationDetailDto(
            new RegulationDto(regulation.Id, regulation.Name, regulation.SourceUrl, regulation.LastSyncedAt, regulation.LatestKnownVersionLabel, regulation.Description, regulation.CountryCode),
            new RegulationVersionDto(currentVersion.Id, currentVersion.VersionLabel, currentVersion.ValidFrom),
            exercises);

        return Result<RegulationDetailDto>.Success(detail);
    }
}
