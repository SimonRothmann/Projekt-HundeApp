using CanisTrack.Domain.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanisTrack.Infrastructure.Persistence.Configurations;

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

        builder.HasOne(p => p.Track)
            .WithMany(t => t.Points)
            .HasForeignKey(p => p.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.TrackId, p.Timestamp });
    }
}
