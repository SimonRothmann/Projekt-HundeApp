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
