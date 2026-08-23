using Dogity.Application.Abstractions;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Notifications;
using Dogity.Domain.Planning;
using Dogity.Domain.Sports;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Infrastructure.Persistence;

/// <summary>
/// Zentraler DbContext. Erbt von IdentityDbContext, damit Identity-Tabellen
/// (users/roles/user_roles) und Domain-Tabellen in derselben Datenbank
/// verwaltet werden - passend zum "modularer Monolith"-Prinzip aus
/// ARCHITECTURE.md.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IApplicationDbContext
{
    public DbSet<Dog> Dogs => Set<Dog>();
    public DbSet<DogOwner> DogOwners => Set<DogOwner>();
    public DbSet<DogImage> DogImages => Set<DogImage>();

    public DbSet<Sport> Sports => Set<Sport>();
    public DbSet<Regulation> Regulations => Set<Regulation>();
    public DbSet<RegulationVersion> RegulationVersions => Set<RegulationVersion>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<RegulationExercise> RegulationExercises => Set<RegulationExercise>();

    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
    public DbSet<TrainingExercise> TrainingExercises => Set<TrainingExercise>();

    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<TrainingPlan> TrainingPlans => Set<TrainingPlan>();
    public DbSet<TrainingPlanItem> TrainingPlanItems => Set<TrainingPlanItem>();
    public DbSet<TrainingPlanWeekConfig> TrainingPlanWeekConfigs => Set<TrainingPlanWeekConfig>();
    public DbSet<ExerciseMastery> ExerciseMasteries => Set<ExerciseMastery>();

    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<TrainerAssignment> TrainerAssignments => Set<TrainerAssignment>();
    public DbSet<ClubTrainer> ClubTrainers => Set<ClubTrainer>();
    public DbSet<ClubMembership> ClubMemberships => Set<ClubMembership>();
    public DbSet<GroupTrainingExercise> GroupTrainingExercises => Set<GroupTrainingExercise>();
    public DbSet<GroupTrainingUnit> GroupTrainingUnits => Set<GroupTrainingUnit>();
    public DbSet<GroupTrainingUnitItem> GroupTrainingUnitItems => Set<GroupTrainingUnitItem>();
    public DbSet<GroupTrainingSession> GroupTrainingSessions => Set<GroupTrainingSession>();
    public DbSet<GroupTrainingSessionItem> GroupTrainingSessionItems => Set<GroupTrainingSessionItem>();
    public DbSet<GroupTrainingSessionTrainer> GroupTrainingSessionTrainers => Set<GroupTrainingSessionTrainer>();

    public DbSet<GpsTrack> GpsTracks => Set<GpsTrack>();
    public DbSet<GpsPoint> GpsPoints => Set<GpsPoint>();
    public DbSet<GpsWalkRun> GpsWalkRuns => Set<GpsWalkRun>();
    public DbSet<GpsWalkPoint> GpsWalkPoints => Set<GpsWalkPoint>();
    public DbSet<GpsWalkStop> GpsWalkStops => Set<GpsWalkStop>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<Identity.RefreshToken> RefreshTokens => Set<Identity.RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Identity-Tabellen auf die Namenskonvention aus DATABASE.md mappen
        // (Plural, snake_case).
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Soft Delete: gelöschte Datensätze werden standardmäßig aus allen
        // Abfragen ausgeblendet (siehe AI_RULES.md "Nie: Daten löschen ohne Migration").
        builder.Entity<Dog>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<DogOwner>().HasQueryFilter(e => e.DeletedAt == null);
        // Zusätzlich am Hund entlang gefiltert: DogImage verlangt zwingend einen
        // Hund, und ohne diesen Zusatz warnt EF zu Recht, dass die Beziehung ins
        // Leere zeigen kann, sobald der Hund weggefiltert ist.
        builder.Entity<DogImage>().HasQueryFilter(e => e.DeletedAt == null && e.Dog!.DeletedAt == null);
        builder.Entity<Sport>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<Regulation>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<RegulationVersion>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<Exercise>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<RegulationExercise>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<TrainingSession>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<TrainingExercise>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<Goal>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<TrainingPlan>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<TrainingPlanItem>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<TrainingPlanWeekConfig>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<ExerciseMastery>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<Club>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<Group>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<GroupMember>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<TrainerAssignment>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<ClubTrainer>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<ClubMembership>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<GroupTrainingExercise>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<GroupTrainingUnit>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<GroupTrainingUnitItem>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<GroupTrainingSession>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<GroupTrainingSessionItem>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<GroupTrainingSessionTrainer>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<GpsTrack>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<GpsPoint>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<GpsWalkRun>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<GpsWalkPoint>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<GpsWalkStop>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<Notification>().HasQueryFilter(e => e.DeletedAt == null);
    }
}
