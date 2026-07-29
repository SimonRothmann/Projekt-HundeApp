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

public class GroupTrainingExerciseConfiguration : IEntityTypeConfiguration<GroupTrainingExercise>
{
    public void Configure(EntityTypeBuilder<GroupTrainingExercise> builder)
    {
        builder.ToTable("group_training_exercises");
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Focus).HasMaxLength(80);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.Category).HasConversion<string>().HasMaxLength(20);
        // Prüfungs-Tags als [Flags]-Enum in einer int-Spalte (Mehrfach-Auswahl).
        builder.Property(e => e.ExamTargets).HasConversion<int>();

        builder.HasOne(e => e.Club)
            .WithMany()
            .HasForeignKey(e => e.ClubId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.ClubId, e.Category });
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

        builder.HasOne(u => u.Club)
            .WithMany()
            .HasForeignKey(u => u.ClubId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(u => new { u.ClubId, u.Category });
    }
}

public class GroupTrainingUnitItemConfiguration : IEntityTypeConfiguration<GroupTrainingUnitItem>
{
    public void Configure(EntityTypeBuilder<GroupTrainingUnitItem> builder)
    {
        builder.ToTable("group_training_unit_items");

        builder.HasOne(i => i.Unit)
            .WithMany(u => u.Items)
            .HasForeignKey(i => i.GroupTrainingUnitId)
            .OnDelete(DeleteBehavior.Cascade);

        // Löschen eines Bausteins darf eine Einheit nicht kaputt machen -
        // Restrict; der Service entfernt/ersetzt die Referenz bewusst.
        builder.HasOne(i => i.Exercise)
            .WithMany()
            .HasForeignKey(i => i.GroupTrainingExerciseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.GroupTrainingUnitId);
        builder.HasIndex(i => i.GroupTrainingExerciseId);
    }
}
