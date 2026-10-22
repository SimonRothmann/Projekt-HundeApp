using Dogity.Application.Common;
using Dogity.Domain.Dogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dogity.Infrastructure.Persistence.Configurations;

public class DogConfiguration : IEntityTypeConfiguration<Dog>
{
    public void Configure(EntityTypeBuilder<Dog> builder)
    {
        builder.ToTable("dogs");
        builder.Property(d => d.Name).HasMaxLength(Textlaengen.Hundename).IsRequired();
        builder.Property(d => d.Breed).HasMaxLength(Textlaengen.Hunderasse);
        builder.Property(d => d.Gender).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.ImageUrl).HasMaxLength(2048);
    }
}

public class DogImageConfiguration : IEntityTypeConfiguration<DogImage>
{
    public void Configure(EntityTypeBuilder<DogImage> builder)
    {
        builder.ToTable("dog_images");
        builder.Property(i => i.Data).HasColumnType("bytea").IsRequired();
        builder.Property(i => i.ContentType).HasMaxLength(100).IsRequired();

        builder.HasOne(i => i.Dog)
            .WithOne()
            .HasForeignKey<DogImage>(i => i.DogId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ein Hund, höchstens ein Bild - ein zweiter Upload ersetzt das alte.
        builder.HasIndex(i => i.DogId).IsUnique();
    }
}

public class DogPauseConfiguration : IEntityTypeConfiguration<DogPause>
{
    public void Configure(EntityTypeBuilder<DogPause> builder)
    {
        builder.ToTable("dog_pauses");
        // Wie Dog.Gender: als Text gespeichert, damit die Spalte lesbar bleibt.
        builder.Property(p => p.Reason).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Note).HasMaxLength(Textlaengen.PausenNotiz);

        builder.HasOne(p => p.Dog)
            .WithMany()
            .HasForeignKey(p => p.DogId)
            .OnDelete(DeleteBehavior.Cascade);

        // Der Verlauf eines Hundes, neueste zuerst.
        builder.HasIndex(p => new { p.DogId, p.StartDate });
    }
}

public class DogOwnerConfiguration : IEntityTypeConfiguration<DogOwner>
{
    public void Configure(EntityTypeBuilder<DogOwner> builder)
    {
        builder.ToTable("dog_owners");
        builder.Property(o => o.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.InvitedEmail).HasMaxLength(256);

        builder.HasOne(o => o.Dog)
            .WithMany(d => d.Owners)
            .HasForeignKey(o => o.DogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => new { o.DogId, o.UserId }).IsUnique();
    }
}
