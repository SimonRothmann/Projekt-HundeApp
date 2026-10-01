using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

public enum GroupRegistrationSource
{
    /// <summary>Über das öffentliche Anmeldeformular der Gruppe.</summary>
    Form,

    /// <summary>Aus dem CSV-Export eines früheren Google-Formulars übernommen.</summary>
    Import,

    /// <summary>Von einer Trainer:in von Hand eingetragen.</summary>
    Manual
}

/// <summary>
/// Die Anmeldung einer Kursteilnehmer:in (Mensch + Hund) zu einer Gruppe,
/// etwa der Welpengruppe.
///
/// Bewusst KEIN <see cref="GroupMember"/>: Wer sich anmeldet, hat kein
/// Dogity-Konto und braucht keines. Die Zeile gehört dem Verein und ist nur
/// für dessen Trainer:innen sichtbar (siehe GroupRegistrationService). Eine
/// Verknüpfung zu einem Konto gibt es absichtlich nicht - sie würde Fremden
/// den Weg zu Daten von Menschen öffnen, die der App nie zugestimmt haben.
///
/// Die Daten sind die des früheren Google-Formulars (Vorname, Nachname,
/// Rufname und Rasse des Hundes, Wurftag, Telefon), dazu die Abhak-Felder der
/// Trainer:innen: "bezahlt" (<see cref="PaidAt"/>) und die Anwesenheit je Tag
/// (<see cref="Attendances"/>).
///
/// Hartes Löschen statt DeletedAt-Markierung (Gruppe löschen, Eintrag
/// löschen, Aufbewahrungsfrist): Es sind Daten Dritter ohne Konto, die sich
/// nach Art. 17 DSGVO wirklich entfernen lassen müssen. Der globale Filter
/// bleibt trotzdem, wie bei jeder Entität.
/// </summary>
public class GroupRegistration : Entity
{
    public Guid GroupId { get; set; }
    public Group? Group { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>Rufname des Hundes.</summary>
    public string DogName { get; set; } = string.Empty;
    public string DogBreed { get; set; } = string.Empty;

    /// <summary>Wurftag des Hundes - daraus ergibt sich das Alter.</summary>
    public DateOnly DogBirthDate { get; set; }

    public string Phone { get; set; } = string.Empty;

    public GroupRegistrationSource Source { get; set; } = GroupRegistrationSource.Form;

    /// <summary>Zeitpunkt der Anmeldung; beim Import der Zeitstempel aus dem Google-Formular.</summary>
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Wann die Trainer:in "bezahlt" gesetzt hat; null = offen.</summary>
    public DateTimeOffset? PaidAt { get; set; }

    /// <summary>Wer "bezahlt" gesetzt hat; wird bei Löschung dieses Kontos auf null gesetzt.</summary>
    public Guid? PaidByUserId { get; set; }

    /// <summary>Notiz nur für Trainer:innen - die Angemeldeten sehen sie nie.</summary>
    public string? Notes { get; set; }

    public ICollection<GroupRegistrationAttendance> Attendances { get; set; } = new List<GroupRegistrationAttendance>();
}
