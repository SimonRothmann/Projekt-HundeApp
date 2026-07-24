using Dogity.Domain.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dogity.Infrastructure.Persistence.Configurations;

public class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.ToTable("goals");
        builder.Property(g => g.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(g => g.Notes).HasMaxLength(2000);
        // DB-Defaults, damit bestehende Ziele bei der Migration sinnvolle Werte
        // bekommen (statt 0) - passend zum bisherigen Generatorverhalten.
        builder.Property(g => g.WeeklyExerciseCount).HasDefaultValue(5);
        builder.Property(g => g.TrainingDaysPerWeek).HasDefaultValue(2);
        builder.HasIndex(g => new { g.DogId, g.Status });

        builder.HasOne(g => g.TrainingPlan)
            .WithOne(p => p.Goal)
            .HasForeignKey<TrainingPlan>(p => p.GoalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(g => g.Regulation)
            .WithMany()
            .HasForeignKey(g => g.RegulationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TrainingPlanConfiguration : IEntityTypeConfiguration<TrainingPlan>
{
    public void Configure(EntityTypeBuilder<TrainingPlan> builder)
    {
        builder.ToTable("training_plans");
        builder.HasIndex(p => p.GoalId).IsUnique();
    }
}

public class TrainingPlanItemConfiguration : IEntityTypeConfiguration<TrainingPlanItem>
{
    public void Configure(EntityTypeBuilder<TrainingPlanItem> builder)
    {
        builder.ToTable("training_plan_items");

        builder.Property(i => i.DayIndex).HasDefaultValue(1);
        // Default Auto, damit bestehende Plan-Items bei der Migration einen
        // gültigen Enum-Wert bekommen (nicht "" -> Mapping-Fehler beim Lesen).
        builder.Property(i => i.Source).HasConversion<string>().HasMaxLength(20).HasDefaultValue(PlanItemSource.Auto);
        builder.Property(i => i.Reason).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Difficulty).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(i => i.TrainingPlan)
            .WithMany(p => p.Items)
            .HasForeignKey(i => i.TrainingPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Exercise)
            .WithMany()
            .HasForeignKey(i => i.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.TrainingPlanId, i.WeekNumber });
    }
}

public class ExerciseMasteryConfiguration : IEntityTypeConfiguration<ExerciseMastery>
{
    public void Configure(EntityTypeBuilder<ExerciseMastery> builder)
    {
        builder.ToTable("exercise_masteries");

        builder.Property(m => m.Box).HasDefaultValue(1);

        builder.HasOne(m => m.Dog)
            .WithMany()
            .HasForeignKey(m => m.DogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Exercise)
            .WithMany()
            .HasForeignKey(m => m.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ein Wiedervorlage-Zustand je (Hund, Übung).
        builder.HasIndex(m => new { m.DogId, m.ExerciseId }).IsUnique();
    }
}
