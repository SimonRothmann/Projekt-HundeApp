using Dogity.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dogity.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);

        // Lookup beim Refresh erfolgt ausschließlich über den Hash - eindeutig,
        // damit ein Token nicht doppelt existiert.
        builder.HasIndex(t => t.TokenHash).IsUnique();
        // Widerruf aller Tokens eines Nutzers (Logout, Admin-Sperre).
        builder.HasIndex(t => t.UserId);
    }
}
