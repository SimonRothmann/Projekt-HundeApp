using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dogity.Infrastructure.Persistence.Configurations;

public class ClubConfiguration : IEntityTypeConfiguration<Club>
{
    public void Configure(EntityTypeBuilder<Club> builder)
    {
        builder.ToTable("clubs");
        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
    }
}

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups");
        builder.Property(g => g.Name).HasMaxLength(150).IsRequired();

        builder.HasOne(g => g.Club)
            .WithMany(c => c.Groups)
            .HasForeignKey(g => g.ClubId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(g => g.TrainerId);
    }
}

public class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        builder.ToTable("group_members");
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(m => m.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.GroupId, m.UserId }).IsUnique();
    }
}

public class TrainerAssignmentConfiguration : IEntityTypeConfiguration<TrainerAssignment>
{
    public void Configure(EntityTypeBuilder<TrainerAssignment> builder)
    {
        builder.ToTable("trainer_assignments");
        builder.HasIndex(a => new { a.TrainerId, a.DogId }).IsUnique();
        builder.HasIndex(a => a.DogId);
    }
}

public class ClubTrainerConfiguration : IEntityTypeConfiguration<ClubTrainer>
{
    public void Configure(EntityTypeBuilder<ClubTrainer> builder)
    {
        builder.ToTable("club_trainers");

        builder.HasOne(t => t.Club)
            .WithMany(c => c.Trainers)
            .HasForeignKey(t => t.ClubId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.ClubId, t.UserId }).IsUnique();
        builder.HasIndex(t => t.UserId);
    }
}

public class ClubMembershipConfiguration : IEntityTypeConfiguration<ClubMembership>
{
    public void Configure(EntityTypeBuilder<ClubMembership> builder)
    {
        builder.ToTable("club_memberships");
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(m => m.Club)
            .WithMany(c => c.Memberships)
            .HasForeignKey(m => m.ClubId)
            .OnDelete(DeleteBehavior.Cascade);

        // Kein Unique-Index auf (ClubId, UserId): nach einer Ablehnung soll
        // erneutes Anfragen möglich sein, ohne die alte Rejected-Zeile zu
        // löschen - der Service prüft stattdessen gezielt auf existierende
        // Pending/Approved-Zeilen vor dem Insert.
        builder.HasIndex(m => new { m.ClubId, m.UserId });
        builder.HasIndex(m => m.UserId);
    }
}

public class GroupTrainingUnitConfiguration : IEntityTypeConfiguration<GroupTrainingUnit>
{
    public void Configure(EntityTypeBuilder<GroupTrainingUnit> builder)
    {
        builder.ToTable("group_training_units");
        builder.Property(u => u.Title).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Description).HasMaxLength(2000);
        builder.Property(u => u.Category).HasConversion<string>().HasMaxLength(20);

        // Kopiert ein Trainer eine Vorlage in seine Gruppe, wird die Gruppe
        // referenziert; wird die Gruppe gelöscht, verschwinden ihre Einheiten mit.
        builder.HasOne(u => u.Group)
            .WithMany()
            .HasForeignKey(u => u.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(u => u.GroupId);
        builder.HasIndex(u => new { u.Category, u.CreatedByUserId });
    }
}

public class GroupTrainingUnitItemConfiguration : IEntityTypeConfiguration<GroupTrainingUnitItem>
{
    public void Configure(EntityTypeBuilder<GroupTrainingUnitItem> builder)
    {
        builder.ToTable("group_training_unit_items");
        builder.Property(i => i.Title).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Description).HasMaxLength(2000);
        builder.Property(i => i.Focus).HasMaxLength(80);

        builder.HasOne(i => i.Unit)
            .WithMany(u => u.Items)
            .HasForeignKey(i => i.GroupTrainingUnitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.GroupTrainingUnitId);
    }
}
