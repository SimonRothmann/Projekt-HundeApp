using Dogity.Domain.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dogity.Infrastructure.Persistence.Configurations;

public class GpsTrackConfiguration : IEntityTypeConfiguration<GpsTrack>
{
    public void Configure(EntityTypeBuilder<GpsTrack> builder)
    {
        builder.ToTable("gps_tracks");
        builder.Property(t => t.Surface).HasMaxLength(100);
        builder.Property(t => t.Weather).HasMaxLength(100);
        builder.Property(t => t.Wind).HasMaxLength(100);
        builder.Property(t => t.Comment).HasMaxLength(2000);
        builder.HasIndex(t => t.TrainingSessionId);
    }
}

public class GpsPointConfiguration : IEntityTypeConfiguration<GpsPoint>
{
    public void Configure(EntityTypeBuilder<GpsPoint> builder)
    {
        builder.ToTable("gps_points");

        builder.Property(p => p.Label).HasMaxLength(200);

        builder.HasOne(p => p.Track)
            .WithMany(t => t.Points)
            .HasForeignKey(p => p.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.TrackId, p.Timestamp });
    }
}

public class GpsWalkRunConfiguration : IEntityTypeConfiguration<GpsWalkRun>
{
    public void Configure(EntityTypeBuilder<GpsWalkRun> builder)
    {
        builder.ToTable("gps_walk_runs");
        builder.Property(r => r.Comment).HasMaxLength(2000);

        builder.HasOne(r => r.Track)
            .WithMany(t => t.WalkRuns)
            .HasForeignKey(r => r.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.TrackId);
    }
}

public class GpsWalkPointConfiguration : IEntityTypeConfiguration<GpsWalkPoint>
{
    public void Configure(EntityTypeBuilder<GpsWalkPoint> builder)
    {
        builder.ToTable("gps_walk_points");

        builder.HasOne(p => p.WalkRun)
            .WithMany(r => r.Points)
            .HasForeignKey(p => p.WalkRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.WalkRunId, p.Timestamp });
    }
}
