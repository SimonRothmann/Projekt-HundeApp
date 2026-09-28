using Dogity.Application.Common;
using Dogity.Domain.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dogity.Infrastructure.Persistence.Configurations;

public class GpsTrackConfiguration : IEntityTypeConfiguration<GpsTrack>
{
    public void Configure(EntityTypeBuilder<GpsTrack> builder)
    {
        builder.ToTable("gps_tracks");
        builder.Property(t => t.Surface).HasMaxLength(Textlaengen.Kurzangabe);
        builder.Property(t => t.Weather).HasMaxLength(Textlaengen.Kurzangabe);
        builder.Property(t => t.Wind).HasMaxLength(Textlaengen.Kurzangabe);
        builder.Property(t => t.Comment).HasMaxLength(Textlaengen.FaehrtenKommentar);
        builder.HasIndex(t => t.TrainingSessionId);
    }
}

public class GpsPointConfiguration : IEntityTypeConfiguration<GpsPoint>
{
    public void Configure(EntityTypeBuilder<GpsPoint> builder)
    {
        builder.ToTable("gps_points");

        builder.Property(p => p.Label).HasMaxLength(Textlaengen.MarkerBeschriftung);

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
        builder.Property(r => r.Comment).HasMaxLength(Textlaengen.FaehrtenKommentar);

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

public class GpsWalkStopConfiguration : IEntityTypeConfiguration<GpsWalkStop>
{
    public void Configure(EntityTypeBuilder<GpsWalkStop> builder)
    {
        builder.ToTable("gps_walk_stops");

        builder.Property(s => s.MarkerLabel).HasMaxLength(Textlaengen.MarkerBeschriftung);
        // Als String statt int: in der DB direkt lesbar, robust gegen spätere
        // Umsortierung der Enum-Werte (Konvention wie bei GroupTraining).
        builder.Property(s => s.Kind).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(s => s.WalkRun)
            .WithMany(r => r.Stops)
            .HasForeignKey(s => s.WalkRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.WalkRunId);
    }
}
