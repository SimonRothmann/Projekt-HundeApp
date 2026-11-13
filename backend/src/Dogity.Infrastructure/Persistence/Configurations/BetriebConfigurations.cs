using Dogity.Application.Common;
using Dogity.Domain.Betrieb;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dogity.Infrastructure.Persistence.Configurations;

public class WartungshinweisConfiguration : IEntityTypeConfiguration<Wartungshinweis>
{
    public void Configure(EntityTypeBuilder<Wartungshinweis> builder)
    {
        builder.ToTable("wartungshinweise");
        builder.Property(h => h.Text).HasMaxLength(Textlaengen.WartungshinweisText);
    }
}
