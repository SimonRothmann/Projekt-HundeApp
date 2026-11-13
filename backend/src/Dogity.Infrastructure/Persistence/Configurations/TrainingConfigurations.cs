using Dogity.Application.Common;
using Dogity.Domain.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dogity.Infrastructure.Persistence.Configurations;

public class TrainingSessionConfiguration : IEntityTypeConfiguration<TrainingSession>
{
    public void Configure(EntityTypeBuilder<TrainingSession> builder)
    {
        builder.ToTable("training_sessions");

        builder.Property(s => s.Condition).HasConversion<string>().HasMaxLength(20);
        // Trägt die Auswertung "Verfassung gegen Bewertung".
        builder.HasIndex(s => new { s.DogId, s.Condition });
        builder.Property(t => t.Notes).HasMaxLength(Textlaengen.TrainingsNotiz);
        builder.Property(t => t.TrainerFeedback).HasMaxLength(Textlaengen.TrainerRueckmeldung);
        // Wie Condition: als Text gespeichert; ohne Antwort bleibt die Spalte leer.
        builder.Property(t => t.OwnerReaction).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.OwnerReply).HasMaxLength(Textlaengen.FeedbackRueckfrage);
        builder.HasIndex(t => new { t.UserId, t.DogId, t.Date });
    }
}

public class TrainingNoteConfiguration : IEntityTypeConfiguration<TrainingNote>
{
    public void Configure(EntityTypeBuilder<TrainingNote> builder)
    {
        builder.ToTable("training_notes");
        // Wie Condition und OwnerReaction: als Text gespeichert, damit die Spalte lesbar bleibt.
        builder.Property(n => n.Goal).HasConversion<string>().HasMaxLength(20);
        builder.Property(n => n.Text).HasMaxLength(Textlaengen.MerkzettelText).IsRequired();

        builder.HasOne(n => n.Dog)
            .WithMany()
            .HasForeignKey(n => n.DogId)
            .OnDelete(DeleteBehavior.Cascade);

        // Wird eine Übung je hart gelöscht, bleibt der Eintrag - nur ohne Übung.
        builder.HasOne(n => n.Exercise)
            .WithMany()
            .HasForeignKey(n => n.ExerciseId)
            .OnDelete(DeleteBehavior.SetNull);

        // Der Merkzettel eines Hundes und die Abfrage des Erinnerungsdienstes: offene Einträge je Hund.
        builder.HasIndex(n => new { n.DogId, n.DoneAt });
    }
}

public class TrainingExerciseConfiguration : IEntityTypeConfiguration<TrainingExercise>
{
    public void Configure(EntityTypeBuilder<TrainingExercise> builder)
    {
        builder.ToTable("training_exercises");
        builder.Property(t => t.Difficulty).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Notes).HasMaxLength(Textlaengen.UebungsNotiz);
        builder.Property(t => t.TrainerNote).HasMaxLength(Textlaengen.UebungsNotiz);
        builder.Property(t => t.FreeTextLabel).HasMaxLength(Textlaengen.EigeneUebung);

        builder.HasOne(t => t.TrainingSession)
            .WithMany(s => s.Exercises)
            .HasForeignKey(t => t.TrainingSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Exercise)
            .WithMany()
            .HasForeignKey(t => t.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);

        // SetNull statt Cascade/Restrict: ein Trainingsplan kann jederzeit
        // gelöscht werden (z.B. Ziel storniert), ohne dass dabei echte
        // Tagebucheinträge mitgelöscht oder das Löschen blockiert wird - der
        // Eintrag bleibt als "nicht mehr einem Plan zugeordnet" bestehen.
        builder.HasOne(t => t.TrainingPlanItem)
            .WithMany()
            .HasForeignKey(t => t.TrainingPlanItemId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
