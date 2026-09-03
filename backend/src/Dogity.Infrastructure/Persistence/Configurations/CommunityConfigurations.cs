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

public class GroupTrainerConfiguration : IEntityTypeConfiguration<GroupTrainer>
{
    public void Configure(EntityTypeBuilder<GroupTrainer> builder)
    {
        builder.ToTable("group_trainers");

        builder.HasOne(t => t.Group)
            .WithMany(g => g.Trainers)
            .HasForeignKey(t => t.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.GroupId, t.UserId }).IsUnique();
        // Eigener Index auf UserId: "welche Gruppen betreue ich?" ist die
        // Abfrage, die bei jedem Aufruf der Trainer-Übersicht läuft.
        builder.HasIndex(t => t.UserId);
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

        // Als Text, nicht als Zahl: In der Datenbank soll "Verwaltung" stehen
        // und nicht "1" - das Projekt hält es bei allen Aufzählungen so
        // (siehe DogGender, ClubMembershipStatus).
        builder.Property(t => t.Role).HasConversion<string>().HasMaxLength(20);

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

public class GroupTrainingSessionConfiguration : IEntityTypeConfiguration<GroupTrainingSession>
{
    public void Configure(EntityTypeBuilder<GroupTrainingSession> builder)
    {
        builder.ToTable("group_training_sessions");
        builder.Property(s => s.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Location).HasMaxLength(200);
        builder.Property(s => s.Notes).HasMaxLength(2000);

        builder.HasOne(s => s.Club)
            .WithMany()
            .HasForeignKey(s => s.ClubId)
            .OnDelete(DeleteBehavior.Cascade);

        // Wird die Gruppe gelöscht, verschwinden ihre Termine mit.
        builder.HasOne(s => s.Group)
            .WithMany()
            .HasForeignKey(s => s.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.ClubId, s.StartsAt });
        builder.HasIndex(s => new { s.GroupId, s.StartsAt });
    }
}

public class GroupTrainingSessionItemConfiguration : IEntityTypeConfiguration<GroupTrainingSessionItem>
{
    public void Configure(EntityTypeBuilder<GroupTrainingSessionItem> builder)
    {
        builder.ToTable("group_training_session_items");
        builder.Property(i => i.FreeText).HasMaxLength(500);

        builder.HasOne(i => i.Session)
            .WithMany(s => s.Items)
            .HasForeignKey(i => i.GroupTrainingSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Baustein-Referenz optional (Freitext-Positionen haben keine);
        // Restrict, damit ein gelöschter Baustein einen Termin nicht kaputt macht.
        builder.HasOne(i => i.Exercise)
            .WithMany()
            .HasForeignKey(i => i.GroupTrainingExerciseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.GroupTrainingSessionId);
    }
}

public class GroupTrainingSessionTrainerConfiguration : IEntityTypeConfiguration<GroupTrainingSessionTrainer>
{
    public void Configure(EntityTypeBuilder<GroupTrainingSessionTrainer> builder)
    {
        builder.ToTable("group_training_session_trainers");

        builder.HasOne(t => t.Session)
            .WithMany(s => s.Trainers)
            .HasForeignKey(t => t.GroupTrainingSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.GroupTrainingSessionId, t.UserId }).IsUnique();
        builder.HasIndex(t => t.UserId);
    }
}

public class ClubRegistrationConfiguration : IEntityTypeConfiguration<ClubRegistration>
{
    public void Configure(EntityTypeBuilder<ClubRegistration> builder)
    {
        builder.ToTable("club_registrations");
        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(2000);
        builder.Property(r => r.DecisionNote).HasMaxLength(1000);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        // Antragsteller und Status: die beiden Abfragen, die es gibt -
        // "meine Anträge" und "was liegt zur Freigabe an".
        builder.HasIndex(r => r.RequestedByUserId);
        builder.HasIndex(r => r.Status);
    }
}
