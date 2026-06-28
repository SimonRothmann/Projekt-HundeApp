using CanisTrack.Application.Abstractions;
using CanisTrack.Domain.Community;
using CanisTrack.Domain.Dogs;
using CanisTrack.Domain.Planning;
using CanisTrack.Domain.Sports;
using CanisTrack.Domain.Tracking;
using CanisTrack.Domain.Training;
using CanisTrack.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CanisTrack.Infrastructure.Persistence;

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

    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<TrainerAssignment> TrainerAssignments => Set<TrainerAssignment>();
    public DbSet<ClubTrainer> ClubTrainers => Set<ClubTrainer>();

    public DbSet<GpsTrack> GpsTracks => Set<GpsTrack>();
    public DbSet<GpsPoint> GpsPoints => Set<GpsPoint>();

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
        builder.Entity<Club>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<Group>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<GroupMember>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<TrainerAssignment>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<ClubTrainer>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<GpsTrack>().HasQueryFilter(e => e.DeletedAt == null);
        builder.Entity<GpsPoint>().HasQueryFilter(e => e.DeletedAt == null);
    }
}
