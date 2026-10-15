using Dogity.Application.Common;
using Dogity.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dogity.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.Property(n => n.Message).HasMaxLength(500).IsRequired();
        builder.Property(n => n.LinkPath).HasMaxLength(200);

        builder.HasIndex(n => new { n.UserId, n.IsRead });
    }
}

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("push_subscriptions");
        builder.Property(s => s.Endpoint).HasMaxLength(Textlaengen.PushEndpoint).IsRequired();
        builder.Property(s => s.P256dh).HasMaxLength(Textlaengen.PushSchluessel).IsRequired();
        builder.Property(s => s.Auth).HasMaxLength(Textlaengen.PushSchluessel).IsRequired();
        builder.Property(s => s.UserAgent).HasMaxLength(Textlaengen.GeraeteBezeichnung);

        // Die Adresse ist die Kennung des Geräts: ein Gerät, eine Zeile. Meldet
        // sich auf demselben Gerät eine andere Person an, wandert die Zeile
        // (Upsert), statt dass zwei Personen dasselbe Gerät bedienen lassen.
        builder.HasIndex(s => s.Endpoint).IsUnique();
        builder.HasIndex(s => s.UserId);
    }
}
