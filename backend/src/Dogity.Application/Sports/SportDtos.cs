using Dogity.Domain.Sports;

namespace Dogity.Application.Sports;

public record SportDto(Guid Id, string Code, string Name, string? Description, Guid? ClubId, string? NameEn = null);

public record CreateSportRequest(string Code, string Name, string? Description, Guid? ClubId, string? NameEn = null);

public record ExerciseDto(
    Guid Id,
    Guid? SportId,
    string Name,
    string? Description,
    ExerciseDifficulty Difficulty,
    string? Category,
    string? ScoringCriteria,
    Guid? ClubId,
    string? NameEn = null,
    string? DescriptionEn = null,
    string? ScoringCriteriaEn = null);

/// <summary>
/// Optionale Einschränkung der Übungsliste auf genau einen Katalog: den
/// globalen (ClubId = null) oder den eines Vereins. Ohne Scope liefert die
/// Liste weiterhin alles, was die Person sehen darf.
/// </summary>
public record ExerciseScope(Guid? ClubId)
{
    public static ExerciseScope Global { get; } = new((Guid?)null);

    public static ExerciseScope ForClub(Guid clubId) => new(clubId);
}

public record CreateExerciseRequest(
    Guid? SportId,
    string Name,
    string? Description,
    ExerciseDifficulty Difficulty,
    string? Category,
    string? ScoringCriteria,
    Guid? ClubId,
    string? NameEn = null,
    string? DescriptionEn = null,
    string? ScoringCriteriaEn = null);

public record UpdateExerciseRequest(
    string Name,
    string? Description,
    ExerciseDifficulty Difficulty,
    string? Category,
    string? ScoringCriteria,
    string? NameEn = null,
    string? DescriptionEn = null,
    string? ScoringCriteriaEn = null);

public record UpdateSportRequest(string Name, string? Description, string? NameEn = null);

/// <param name="CurrentVersionValidFrom">
/// Gültig ab der aktuell geführten Fassung; null, wenn es noch keine gibt.
///
/// Steht bewusst schon in der Liste und nicht erst im Detail: die Sitemap
/// braucht für jede der rund fünfzig öffentlichen Seiten ein ehrliches
/// lastmod-Datum, und fünfzig Detail-Abrufe dafür wären Unfug.
/// </param>
public record RegulationDto(
    Guid Id,
    string Name,
    string? SourceUrl,
    DateTimeOffset? LastSyncedAt,
    string? LatestKnownVersionLabel,
    string? Description,
    string? CountryCode,
    DateOnly? CurrentVersionValidFrom,
    string? NameEn = null,
    string? DescriptionEn = null)
{
    /// <summary>
    /// Namen der Prüfungsordnungen, die auf diese folgen (Ausbildungsweg) - für
    /// "Nächste Stufe" auf der öffentlichen Seite. Berechnet statt gespeichert:
    /// Die Reihenfolge steht nur in <see cref="Planning.Ausbildungsweg"/>.
    /// </summary>
    public IReadOnlyList<string> NextStageNames => Planning.Ausbildungsweg.FolgestufenVon(Name);
}

public record UpdateRegulationRequest(string Name, string? Description, string? SourceUrl, string? CountryCode, string? NameEn = null, string? DescriptionEn = null);

public record AddRegulationExerciseRequest(Guid ExerciseId, bool IsMandatory, int MaxPoints, string? ScoringNotes, string? ScoringNotesEn = null);

public record UpdateRegulationExerciseRequest(bool IsMandatory, int MaxPoints, string? ScoringNotes, string? ScoringNotesEn = null);

public record RegulationVersionDto(Guid Id, string VersionLabel, DateOnly ValidFrom);

public record RegulationExerciseDto(
    Guid ExerciseId,
    string ExerciseName,
    bool IsMandatory,
    int MaxPoints,
    string? ScoringNotes,
    string? ExerciseNameEn = null,
    string? ScoringNotesEn = null);

public record RegulationDetailDto(
    RegulationDto Regulation,
    RegulationVersionDto CurrentVersion,
    IReadOnlyList<RegulationExerciseDto> Exercises);

/// <summary>
/// Ein wählbarer Geltungsbereich.
/// </summary>
/// <param name="Code">ISO 3166-1 alpha-2, z.B. "DE".</param>
/// <param name="RegulationCount">
/// Wie viele Prüfungsordnungen hier gelten - null mitgezählt, denn eine
/// international gültige Ordnung gilt auch hier. Null bedeutet nicht
/// "kaputt", sondern "noch keine Inhalte"; die Oberfläche sagt das auch so.
/// </param>
public record CountryDto(string Code, int RegulationCount);
