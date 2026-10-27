using Dogity.Domain.Preferences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dogity.Infrastructure.Persistence.Configurations;

public class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
        builder.ToTable("user_preferences");
        builder.Property(p => p.Locale).HasMaxLength(10);
        builder.Property(p => p.Country).HasMaxLength(2);

        // Vorgabe "ja": Bestehende Zeilen bekommen mit der Migration true - wer
        // vorher nichts eingestellt hat, hat auch nichts abgelehnt.
        // ValueGeneratedNever, weil EF bei einem bool mit Datenbank-Vorgabe true
        // sonst ein ausdrückliches false für "nicht gesetzt" hält, beim Einfügen
        // weglässt - und die Datenbank daraus wieder true machte.
        builder.Property(p => p.EmailNotifications).HasDefaultValue(true).ValueGeneratedNever();

        // Erinnerungen: dieselbe Überlegung (und derselbe Kniff mit ValueGeneratedNever)
        // wie oben. Die Vorgaben stehen zusätzlich in der Datenbank, damit bestehende
        // Zeilen bei der Migration gültige Werte bekommen: alles an, Sonntag, 18 Uhr.
        builder.Property(p => p.ReminderSessions).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(p => p.ReminderTracks).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(p => p.ReminderTrainerSummary).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(p => p.ReminderHealth).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(p => p.TrainerSummaryDays).HasDefaultValue(64).ValueGeneratedNever();
        builder.Property(p => p.TrainerSummaryHour).HasDefaultValue(18).ValueGeneratedNever();

        // Eine Einstellungszeile je Nutzer. Ohne den eindeutigen Index
        // entstünde beim gleichzeitigen Speichern aus zwei Geräten eine
        // zweite Zeile, und ab da läse man je nach Zufall die eine oder die
        // andere.
        builder.HasIndex(p => p.UserId).IsUnique();

        builder.HasMany(p => p.DisabledModules)
            .WithOne(m => m.UserPreference!)
            .HasForeignKey(m => m.UserPreferenceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Sports)
            .WithOne(s => s.UserPreference!)
            .HasForeignKey(s => s.UserPreferenceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserDisabledModuleConfiguration : IEntityTypeConfiguration<UserDisabledModule>
{
    public void Configure(EntityTypeBuilder<UserDisabledModule> builder)
    {
        builder.ToTable("user_disabled_modules");
        builder.Property(m => m.ModuleKey).HasMaxLength(60).IsRequired();
        builder.HasIndex(m => new { m.UserPreferenceId, m.ModuleKey }).IsUnique();
    }
}

public class UserSportSelectionConfiguration : IEntityTypeConfiguration<UserSportSelection>
{
    public void Configure(EntityTypeBuilder<UserSportSelection> builder)
    {
        builder.ToTable("user_sport_selections");
        builder.HasIndex(s => new { s.UserPreferenceId, s.SportId }).IsUnique();
    }
}

public class DogSportSelectionConfiguration : IEntityTypeConfiguration<DogSportSelection>
{
    public void Configure(EntityTypeBuilder<DogSportSelection> builder)
    {
        builder.ToTable("dog_sport_selections");

        builder.HasOne(s => s.Dog)
            .WithMany(d => d.Sports)
            .HasForeignKey(s => s.DogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.DogId, s.SportId }).IsUnique();
    }
}
