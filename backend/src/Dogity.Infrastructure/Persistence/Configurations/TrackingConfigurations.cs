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
        // Als String statt int: wie bei GpsWalkStop.Kind. Bestehende Zeilen sind
        // Fährten (Vorgabe "Faehrte"); die beiden Schalter starten mit false.
        builder.Property(t => t.Kind).HasConversion<string>().HasMaxLength(20).HasDefaultValue(GpsTrackKind.Faehrte);
        builder.Property(t => t.ScentArticle).HasMaxLength(Textlaengen.Geruchsartikel);
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
        builder.Property(r => r.Indication).HasConversion<string>().HasMaxLength(20);
        // Bestehende Zeilen gelten als "vor Winkel und Schenkeln" (0); der Backfill rechnet sie nach.
        builder.Property(r => r.EvaluationVersion).HasDefaultValue(0);

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

public class GpsWalkRunLegConfiguration : IEntityTypeConfiguration<GpsWalkRunLeg>
{
    public void Configure(EntityTypeBuilder<GpsWalkRunLeg> builder)
    {
        builder.ToTable("gps_walk_run_legs");

        builder.HasOne(l => l.WalkRun)
            .WithMany(r => r.Legs)
            .HasForeignKey(l => l.WalkRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.WalkRunId);
    }
}

public class GpsWalkRunCornerConfiguration : IEntityTypeConfiguration<GpsWalkRunCorner>
{
    public void Configure(EntityTypeBuilder<GpsWalkRunCorner> builder)
    {
        builder.ToTable("gps_walk_run_corners");
        // Als String wie GpsWalkStop.Kind.
        builder.Property(c => c.Direction).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(c => c.WalkRun)
            .WithMany(r => r.Corners)
            .HasForeignKey(c => c.WalkRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.WalkRunId);
    }
}
