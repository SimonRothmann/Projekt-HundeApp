using Dogity.Domain.Common;
using Dogity.Domain.Sports;

namespace Dogity.Domain.Community;

public enum ClubExamStatus
{
    Planned,
    /// <summary>Alle Teams haben ein Ergebnis - setzt der Service von selbst, nimmt ihn mit einem zurückgesetzten Ergebnis wieder zurück.</summary>
    Done,
    Cancelled
}

public enum ClubExamResult
{
    Open,
    Passed,
    Failed,
    NotStarted
}

/// <summary>
/// Ein Prüfungstag des Vereins (siehe docs/BERECHTIGUNGEN.md): Datum, Ort und
/// Richter:in, dazu die Meldeliste (<see cref="Entries"/>). Anlegen, Teams
/// eintragen und Ergebnisse führt allein die Prüfungsleitung des Vereins
/// (<see cref="ClubRollen.Pruefungen"/>); die Mitglieder melden nichts in der
/// App. Training und Vorstand sehen nur, DASS es den Tag gibt.
/// </summary>
public class ClubExam : Entity
{
    public Guid ClubId { get; set; }
    public Club? Club { get; set; }

    public DateOnly Date { get; set; }
    public TimeOnly? StartTime { get; set; }

    public string Location { get; set; } = string.Empty;
    public string? JudgeName { get; set; }

    /// <summary>Nur zur Anzeige - der Server sperrt nach dem Meldeschluss nichts.</summary>
    public DateOnly? RegistrationDeadline { get; set; }
    public string? Note { get; set; }

    public ClubExamStatus Status { get; set; } = ClubExamStatus.Planned;
    public Guid? CreatedByUserId { get; set; }

    /// <summary>
    /// Die angebotenen Ordnungen: Sie steuern die Auswahl beim Eintragen eines
    /// Teams. Leer heißt "keine Vorgabe" - die Ordnungen des Tages ergeben sich
    /// dann aus den Einträgen.
    /// </summary>
    public ICollection<ClubExamRegulation> Regulations { get; set; } = new List<ClubExamRegulation>();
    public ICollection<ClubExamEntry> Entries { get; set; } = new List<ClubExamEntry>();
}

/// <summary>Eine Ordnung, die bei einem Prüfungstag angeboten wird.</summary>
public class ClubExamRegulation : Entity
{
    public Guid ExamId { get; set; }
    public ClubExam? Exam { get; set; }

    public Guid RegulationId { get; set; }
    public Regulation? Regulation { get; set; }
}

/// <summary>
/// Ein Team auf der Meldeliste: entweder ein Vereinsmitglied mit einem seiner
/// Hunde (<see cref="MemberUserId"/> + <see cref="DogId"/>) oder ein Gast ohne
/// Dogity-Bezug (die <c>Guest*</c>-Felder - Freitext Dritter, den der Verein
/// einträgt; sie leben nur hier und gehen mit dem Eintrag).
///
/// Verlässt das Mitglied den Verein, oder wird der Hund oder das Konto
/// gelöscht, werden MemberUserId, DogId und GoalId leer (siehe
/// <c>ClubExamAnonymisierung</c>): Das Ergebnis bleibt für die Vereinszahlen,
/// die Person nicht. Die Anzeige schreibt dann "Ehemaliges Mitglied".
/// </summary>
public class ClubExamEntry : Entity
{
    public Guid ExamId { get; set; }
    public ClubExam? Exam { get; set; }

    public Guid RegulationId { get; set; }
    public Regulation? Regulation { get; set; }

    /// <summary>Reihenfolge der Starterliste innerhalb der Ordnung (Lücken sind erlaubt).</summary>
    public int StartOrder { get; set; }

    public Guid? MemberUserId { get; set; }
    public Guid? DogId { get; set; }

    public string? GuestHandlerName { get; set; }
    public string? GuestDogName { get; set; }
    public string? GuestInfo { get; set; }

    public ClubExamResult Result { get; set; } = ClubExamResult.Open;
    public int? Score { get; set; }
    public string? ResultNote { get; set; }

    /// <summary>Das Ziel am Hund, das dieses Ergebnis trägt (nur bei "bestanden" eines Mitglied-Teams).</summary>
    public Guid? GoalId { get; set; }

    /// <summary>
    /// Das Ziel hat erst dieser Eintrag angelegt (der Hund hatte kein aktives zur
    /// Ordnung). Nimmt die Prüfungsleitung "bestanden" zurück, wird es wieder
    /// gelöscht; ein vorher aktives Ziel wird stattdessen wieder aktiv.
    /// </summary>
    public bool GoalCreated { get; set; }

    public DateTimeOffset? ResultEnteredAt { get; set; }
    public Guid? ResultByUserId { get; set; }
}
