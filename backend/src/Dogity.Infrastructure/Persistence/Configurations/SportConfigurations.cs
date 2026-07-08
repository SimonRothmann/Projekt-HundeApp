using Dogity.Domain.Sports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dogity.Infrastructure.Persistence.Configurations;

public class SportConfiguration : IEntityTypeConfiguration<Sport>
{
    public void Configure(EntityTypeBuilder<Sport> builder)
    {
        builder.ToTable("sports");
        builder.Property(s => s.Code).HasMaxLength(30).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        // Code eindeutig im Scope einer ClubId - so kann jeder Verein einen
        // "GRUNDLAGEN"-Code haben, ohne mit dem globalen Katalog zu kollidieren.
        builder.HasIndex(s => new { s.Code, s.ClubId }).IsUnique();
        builder.HasIndex(s => s.ClubId);
    }
}

public class RegulationConfiguration : IEntityTypeConfiguration<Regulation>
{
    public void Configure(EntityTypeBuilder<Regulation> builder)
    {
        builder.ToTable("regulations");
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.SourceUrl).HasMaxLength(500);
        builder.Property(r => r.LatestKnownVersionLabel).HasMaxLength(50);

        builder.HasOne(r => r.Sport)
            .WithMany(s => s.Regulations)
            .HasForeignKey(r => r.SportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RegulationVersionConfiguration : IEntityTypeConfiguration<RegulationVersion>
{
    public void Configure(EntityTypeBuilder<RegulationVersion> builder)
    {
        builder.ToTable("regulation_versions");
        builder.Property(v => v.VersionLabel).HasMaxLength(50).IsRequired();

        builder.HasOne(v => v.Regulation)
            .WithMany(r => r.Versions)
            .HasForeignKey(v => v.RegulationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("exercises");
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Difficulty).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Category).HasMaxLength(100);
        builder.Property(e => e.ScoringCriteria).HasMaxLength(1000);

        // SportId ist optional (siehe Exercise.SportId-Kommentar). SetNull statt
        // Cascade: eine gelöschte Sportart lässt bestehende Übungen unangetastet
        // und stellt sie damit als "sportartlose" Übungen dar, statt sie
        // ungewollt gleich mit zu löschen.
        builder.HasOne(e => e.Sport)
            .WithMany(s => s.Exercises)
            .HasForeignKey(e => e.SportId)
            .OnDelete(DeleteBehavior.SetNull);

        // Kein DB-Fremdschlüssel zu Club (anderes Modul, siehe Community) -
        // analog zu anderen modulübergreifenden Guid-Referenzen wie
        // TrainerAssignment.TrainerId, die ebenfalls ohne FK auskommen.
        builder.HasIndex(e => e.ClubId);
    }
}

public class RegulationExerciseConfiguration : IEntityTypeConfiguration<RegulationExercise>
{
    public void Configure(EntityTypeBuilder<RegulationExercise> builder)
    {
        builder.ToTable("regulation_exercises");
        builder.Property(re => re.ScoringNotes).HasMaxLength(1000);

        builder.HasOne(re => re.RegulationVersion)
            .WithMany(v => v.RegulationExercises)
            .HasForeignKey(re => re.RegulationVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(re => re.Exercise)
            .WithMany(e => e.RegulationExercises)
            .HasForeignKey(re => re.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(re => new { re.RegulationVersionId, re.ExerciseId }).IsUnique();
    }
}
