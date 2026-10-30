using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

/// <summary>
/// Was jemand innerhalb eines Vereins darf - als Menge, denn die Rollen
/// lassen sich frei kombinieren (etwa Training und Vorstand). Die Rechte
/// addieren sich; <see cref="Verwaltung"/> schließt alle anderen ein.
///
/// Mehrere Stufen, weil eine flache Rolle für Selbstverwaltung nicht reicht:
/// Dürfte jede:r Trainer:in andere abberufen, könnte auch die Person, die
/// den Verein angelegt hat, aus ihm entfernt werden - und niemand könnte es
/// zurücknehmen.
///
/// Gespeichert als Zahl (Bitmenge), nicht als Text: Die Rechteprüfungen
/// fragen in der Datenbank nach einzelnen Bits. Die Werte dürfen sich nie
/// mehr ändern. Die API überträgt sie ebenfalls numerisch.
/// </summary>
[Flags]
public enum ClubRole
{
    /// <summary>Trainiert und pflegt die EIGENEN Gruppen sowie den vereinseigenen Katalog.</summary>
    Training = 1,

    /// <summary>Zusätzlich: Stammdaten ändern, alle Rollen vergeben und entziehen.</summary>
    Verwaltung = 2,

    /// <summary>
    /// Übergeordnete:r Trainer:in: verwaltet alle Gruppen und Termine des
    /// Vereins und beruft Trainer:innen (nur die Rolle Training).
    /// </summary>
    Ausbildungswart = 4,

    /// <summary>
    /// Beitrittsanfragen, Einladungen, Einladungslink und Mitgliederliste -
    /// ohne Trainerrechte.
    /// </summary>
    Mitgliederverwaltung = 8,

    /// <summary>Soll künftig Zahlen und Mitgliederliste lesen; ändert nichts.</summary>
    Vorstand = 16,
}

/// <summary>
/// Rollenmengen, auf denen die einzelnen Rechte beruhen. Die Zuordnung Recht
/// -> Rollen steht an einer Stelle (ClubAccessQueries); hier nur die Bausteine.
/// </summary>
public static class ClubRollen
{
    /// <summary>Alle gültigen Rollen - alles darüber hinaus ist eine ungültige Eingabe.</summary>
    public const ClubRole Alle =
        ClubRole.Training | ClubRole.Verwaltung | ClubRole.Ausbildungswart | ClubRole.Mitgliederverwaltung | ClubRole.Vorstand;

    /// <summary>Rollen, die eine Person zur Trainer:in des Vereins machen.</summary>
    public const ClubRole Trainer = ClubRole.Training | ClubRole.Ausbildungswart | ClubRole.Verwaltung;

    /// <summary>Rollen, die alle Gruppen des Vereins verwalten dürfen.</summary>
    public const ClubRole AlleGruppen = ClubRole.Ausbildungswart | ClubRole.Verwaltung;

    /// <summary>Rollen, die Mitglieder, Anfragen und Einladungen verwalten dürfen.</summary>
    public const ClubRole Mitglieder = ClubRole.Mitgliederverwaltung | ClubRole.Verwaltung;

    /// <summary>Rollen, die Trainer:innen berufen und abberufen dürfen.</summary>
    public const ClubRole TrainerBerufen = ClubRole.Ausbildungswart | ClubRole.Verwaltung;

    /// <summary>Rollen, die die Vereinsübersicht lesen dürfen.</summary>
    public const ClubRole Uebersicht = ClubRole.Vorstand | ClubRole.Verwaltung;
}

/// <summary>
/// Gibt einer Person Rollen in genau einem Verein. Nur mit einer Trainer-Rolle
/// (<see cref="ClubRollen.Trainer"/>) dürfen Gruppen unter diesem Verein
/// angelegt und vereinsspezifische Übungen gepflegt werden (siehe
/// Exercise.ClubId); was jede Rolle sonst darf, steht in ClubAccessQueries.
///
/// Die Tabelle heißt weiter ClubTrainers, obwohl Mitgliederverwaltung und
/// Vorstand keine Trainer:innen sind: Die Rollen sind erst nachträglich
/// dazugekommen, ein Umbenennen hieße eine Migration ohne Nutzen.
/// </summary>
public class ClubTrainer : Entity
{
    public Guid ClubId { get; set; }
    public Club? Club { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// Nie leer: Wer keine Rolle mehr hat, verliert die Zeile. Fällt die
    /// letzte <see cref="ClubRole.Verwaltung"/> eines Vereins durch
    /// Kontolöschung weg, rückt die dienstälteste Person mit einer
    /// Trainer-Rolle nach (siehe <c>ClubNachfolge</c>).
    /// </summary>
    public ClubRole Role { get; set; } = ClubRole.Training;
}
