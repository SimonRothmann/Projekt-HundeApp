using Dogity.Domain.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dogity.Infrastructure.Persistence.Configurations;

public class TrainingSessionConfiguration : IEntityTypeConfiguration<TrainingSession>
{
    public void Configure(EntityTypeBuilder<TrainingSession> builder)
    {
        builder.ToTable("training_sessions");
        builder.Property(t => t.Notes).HasMaxLength(4000);
        builder.Property(t => t.TrainerFeedback).HasMaxLength(2000);
        builder.HasIndex(t => new { t.UserId, t.DogId, t.Date });
    }
}

public class TrainingExerciseConfiguration : IEntityTypeConfiguration<TrainingExercise>
{
    public void Configure(EntityTypeBuilder<TrainingExercise> builder)
    {
        builder.ToTable("training_exercises");
        builder.Property(t => t.Difficulty).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Notes).HasMaxLength(2000);

        builder.HasOne(t => t.TrainingSession)
            .WithMany(s => s.Exercises)
            .HasForeignKey(t => t.TrainingSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Exercise)
            .WithMany()
            .HasForeignKey(t => t.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
