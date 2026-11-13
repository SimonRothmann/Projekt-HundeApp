using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

/// <summary>
/// Ein Arbeitseinsatz des Vereins (Platzpflege, Aufbau Prüfungstag, Vereinsheim ...): Die Verwaltung
/// trägt ihn ein und hakt ab, wer geholfen hat (<see cref="Participations"/>) - damit sich später
/// zeigen lässt, wer wie oft und wie lange mitgeholfen hat (Dank, Ehrungen, Planung). Es gibt kein
/// Soll, keine Selbsteintragung und keine Bestätigung (siehe docs/BERECHTIGUNGEN.md).
///
/// Aufbewahrt werden Einsätze bis zum Beginn des drittletzten Kalenderjahres; danach löscht ein
/// täglicher Lauf sie samt Teilnahmen endgültig (<c>ClubWorkRetention</c>).
/// </summary>
public class ClubWorkEvent : Entity
{
    public Guid ClubId { get; set; }
    public Club? Club { get; set; }

    public DateOnly Date { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Note { get; set; }

    /// <summary>Dauer für alle Helfer:innen, sofern bei einer Teilnahme nichts anderes steht.</summary>
    public int DurationMinutes { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public ICollection<ClubWorkParticipation> Participations { get; set; } = new List<ClubWorkParticipation>();
}

/// <summary>
/// Eine abgehakte Person bei einem Arbeitseinsatz. Verlässt die Person den Verein, oder wird ihr
/// Konto gelöscht, wird <see cref="UserId"/> leer (siehe <c>ClubWorkAnonymisierung</c>): Die Stunden
/// zählen weiter in den Summen, die Anzeige schreibt "Ehemaliges Mitglied".
///
/// Teilnahmen werden endgültig gelöscht (mit dem Einsatz oder beim Austragen), nicht weich: Sie sind
/// Angaben zu einer Person, und ein Austragen auf Widerspruch soll sie wirklich entfernen.
/// </summary>
public class ClubWorkParticipation : Entity
{
    public Guid ClubWorkEventId { get; set; }
    public ClubWorkEvent? Event { get; set; }

    public Guid? UserId { get; set; }

    /// <summary>
    /// Eine Person ohne Dogity-Konto (<see cref="ClubWorkGuest"/>). Höchstens eines von <see cref="UserId"/> und
    /// GuestId ist gesetzt; sind beide leer, ist es ein ehemaliges Mitglied.
    /// </summary>
    public Guid? GuestId { get; set; }
    public ClubWorkGuest? Guest { get; set; }

    /// <summary>Abweichende Dauer dieser Person; null = die Dauer des Einsatzes.</summary>
    public int? Minutes { get; set; }

    public Guid? RecordedByUserId { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Eine Helfer:in ohne Dogity-Konto, von der Verwaltung angelegt (Paket A2): Nicht alle Mitglieder nutzen die App,
/// sollen aber über mehrere Einsätze hinweg als dieselbe Person gezählt werden. Der Nachname ist optional - manche
/// Vereine kennen nur "Opa Karl". Die Person hat keine Ansicht; Auskunft und Löschung laufen über den Verein.
///
/// Wird endgültig gelöscht (von der Verwaltung samt Teilnahmen, oder vom täglichen Lauf, wenn keine Teilnahme mehr
/// bleibt), nicht weich: Es sind Angaben zu einer Person.
/// </summary>
public class ClubWorkGuest : Entity
{
    public Guid ClubId { get; set; }
    public Club? Club { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }

    public Guid? CreatedByUserId { get; set; }
}
