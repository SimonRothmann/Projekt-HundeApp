using Dogity.Domain.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dogity.Infrastructure.Persistence.Configurations;

public class QuizCatalogConfiguration : IEntityTypeConfiguration<QuizCatalog>
{
    public void Configure(EntityTypeBuilder<QuizCatalog> builder)
    {
        builder.ToTable("quiz_catalogs");

        builder.Property(c => c.Code).HasMaxLength(40).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(1000);
        builder.Property(c => c.Publisher).HasMaxLength(200).IsRequired();
        builder.Property(c => c.SourceUrl).HasMaxLength(500);
        builder.Property(c => c.Edition).HasMaxLength(40);
        builder.Property(c => c.Audience).HasConversion<string>().HasMaxLength(20);

        // Der Seeder gleicht über den Code ab.
        builder.HasIndex(c => c.Code).IsUnique();
    }
}

public class QuizQuestionConfiguration : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> builder)
    {
        builder.ToTable("quiz_questions");

        builder.Property(q => q.Section).HasMaxLength(10).IsRequired();
        builder.Property(q => q.SectionName).HasMaxLength(120);
        builder.Property(q => q.Number).HasMaxLength(20).IsRequired();
        builder.Property(q => q.Text).HasMaxLength(2000).IsRequired();
        builder.Property(q => q.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(q => q.SampleSolution).HasMaxLength(2000);
        builder.Property(q => q.ImageName).HasMaxLength(100);

        builder.HasOne(q => q.Catalog)
            .WithMany(c => c.Questions)
            .HasForeignKey(q => q.CatalogId)
            .OnDelete(DeleteBehavior.Cascade);

        // Eine Fragennummer je Katalog. ACHTUNG: der Index kennt DeletedAt
        // nicht - wird eine Frage entfernt und später wieder aufgenommen, muss
        // der Seeder die vorhandene Zeile wiederbeleben statt eine zweite
        // anzulegen (FindIncludingRemovedAsync, siehe SoftDeleteRevival).
        builder.HasIndex(q => new { q.CatalogId, q.Number }).IsUnique();

        // Trägt die Verwaltungsansicht "was wurde schon angefasst".
        builder.HasIndex(q => q.EditedAt);
    }
}

public class QuizOptionConfiguration : IEntityTypeConfiguration<QuizOption>
{
    public void Configure(EntityTypeBuilder<QuizOption> builder)
    {
        builder.ToTable("quiz_options");

        builder.Property(o => o.Text).HasMaxLength(1000).IsRequired();
        // Vorgabe Answer, damit die vorhandenen Zeilen bei der Migration einen
        // gültigen Enum-Wert bekommen (nicht "" -> Lesefehler).
        builder.Property(o => o.Kind).HasConversion<string>().HasMaxLength(20).HasDefaultValue(QuizOptionKind.Answer);
        builder.Property(o => o.MatchKey).HasMaxLength(8);
        builder.Property(o => o.ImageName).HasMaxLength(100);

        builder.HasOne(o => o.Question)
            .WithMany(q => q.Options)
            .HasForeignKey(o => o.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => o.QuestionId);
    }
}

public class QuizMasteryConfiguration : IEntityTypeConfiguration<QuizMastery>
{
    public void Configure(EntityTypeBuilder<QuizMastery> builder)
    {
        builder.ToTable("quiz_masteries");

        builder.Property(m => m.Box).HasDefaultValue(1);

        builder.HasOne(m => m.Question)
            .WithMany()
            .HasForeignKey(m => m.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ein Lernstand je (Nutzer, Frage). "Von vorne anfangen" setzt die
        // Werte dieser Zeile zurück und löscht sie NICHT - damit läuft der
        // eindeutige Index nie in den Soft-Delete-Konflikt.
        builder.HasIndex(m => new { m.UserId, m.QuestionId }).IsUnique();

        // Trägt die Auswahl "was ist jetzt dran".
        builder.HasIndex(m => new { m.UserId, m.DueAt });
    }
}
