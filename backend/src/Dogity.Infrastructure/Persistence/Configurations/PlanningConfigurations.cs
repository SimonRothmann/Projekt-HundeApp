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
