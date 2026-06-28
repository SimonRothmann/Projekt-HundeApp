using CanisTrack.Domain.Dogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanisTrack.Infrastructure.Persistence.Configurations;

public class DogConfiguration : IEntityTypeConfiguration<Dog>
{
    public void Configure(EntityTypeBuilder<Dog> builder)
    {
        builder.ToTable("dogs");
        builder.Property(d => d.Name).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Breed).HasMaxLength(100);
        builder.Property(d => d.Gender).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.ImageUrl).HasMaxLength(2048);
    }
}

public class DogOwnerConfiguration : IEntityTypeConfiguration<DogOwner>
{
    public void Configure(EntityTypeBuilder<DogOwner> builder)
    {
        builder.ToTable("dog_owners");
        builder.Property(o => o.Role).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(o => o.Dog)
            .WithMany(d => d.Owners)
            .HasForeignKey(o => o.DogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => new { o.DogId, o.UserId }).IsUnique();
    }
}
