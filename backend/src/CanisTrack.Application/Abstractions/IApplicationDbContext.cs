using CanisTrack.Domain.Community;
using CanisTrack.Domain.Dogs;
using CanisTrack.Domain.Planning;
using CanisTrack.Domain.Sports;
using CanisTrack.Domain.Tracking;
using CanisTrack.Domain.Training;
using Microsoft.EntityFrameworkCore;

namespace CanisTrack.Application.Abstractions;

/// <summary>
/// Abstraktion über die Persistenz, damit Application keine Abhängigkeit
/// zu Npgsql/Identity (Infrastructure) hat - nur zu EF Core selbst
/// (DbSet/DbContext sind reine Abstraktionen ohne Provider-Bindung).
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Dog> Dogs { get; }
    DbSet<DogOwner> DogOwners { get; }

    DbSet<Sport> Sports { get; }
    DbSet<Regulation> Regulations { get; }
    DbSet<RegulationVersion> RegulationVersions { get; }
    DbSet<Exercise> Exercises { get; }
    DbSet<RegulationExercise> RegulationExercises { get; }

    DbSet<TrainingSession> TrainingSessions { get; }
    DbSet<TrainingExercise> TrainingExercises { get; }

    DbSet<Goal> Goals { get; }
    DbSet<TrainingPlan> TrainingPlans { get; }
    DbSet<TrainingPlanItem> TrainingPlanItems { get; }

    DbSet<Club> Clubs { get; }
    DbSet<Group> Groups { get; }
    DbSet<GroupMember> GroupMembers { get; }
    DbSet<TrainerAssignment> TrainerAssignments { get; }
    DbSet<ClubTrainer> ClubTrainers { get; }

    DbSet<GpsTrack> GpsTracks { get; }
    DbSet<GpsPoint> GpsPoints { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
