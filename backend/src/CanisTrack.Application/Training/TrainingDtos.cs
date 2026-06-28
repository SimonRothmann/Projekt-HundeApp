using CanisTrack.Domain.Sports;

namespace CanisTrack.Application.Training;

public record TrainingExerciseDto(
    Guid Id,
    Guid ExerciseId,
    string ExerciseName,
    int Rating,
    ExerciseDifficulty Difficulty,
    bool Success,
    string? Notes);

public record TrainingSessionDto(
    Guid Id,
    Guid DogId,
    DateOnly Date,
    int DurationMinutes,
    string? Notes,
    IReadOnlyList<TrainingExerciseDto> Exercises,
    string? TrainerFeedback,
    DateTimeOffset? FeedbackAt);

public record SetFeedbackRequest(string Feedback);

public record CreateTrainingExerciseRequest(
    Guid ExerciseId,
    int Rating,
    ExerciseDifficulty Difficulty,
    bool Success,
    string? Notes);

public record CreateTrainingSessionRequest(
    Guid DogId,
    DateOnly Date,
    int DurationMinutes,
    string? Notes,
    IReadOnlyList<CreateTrainingExerciseRequest> Exercises);
