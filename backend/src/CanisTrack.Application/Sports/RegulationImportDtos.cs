namespace CanisTrack.Application.Sports;

public record ApplyExerciseCandidate(string Name, int MaxPoints);

public record ApplyRegulationImportRequest(Guid RegulationId, IReadOnlyList<ApplyExerciseCandidate> Candidates);
