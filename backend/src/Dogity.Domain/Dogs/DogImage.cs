using Dogity.Domain.Common;

namespace Dogity.Domain.Dogs;

/// <summary>
/// Das Profilbild eines Hundes, direkt in der Datenbank.
///
/// Bewusst kein Objektspeicher: ein Hundebild wird vom Client vor dem Hochladen
/// auf Profilbildgröße gerechnet und ist damit wenige Zehn Kilobyte groß. In
/// der Datenbank braucht es keinen weiteren Dienst und keine Zugangsdaten - und
/// es landet ohne Zutun in der täglichen Datensicherung, die sonst getrennt für
/// Dateien eingerichtet werden müsste.
///
/// Bewusst eine EIGENE Tabelle und keine Spalte an <see cref="Dog"/>: sonst
/// läse jede Abfrage auf Hunde die Bilddaten mit - auch die Hundeliste und die
/// Trainerübersicht, die nichts als Namen anzeigen. So wird das Bild nur
/// geladen, wenn es jemand ausdrücklich anfordert.
/// </summary>
public class DogImage : Entity
{
    /// <summary>Zugleich der fachliche Schlüssel: ein Hund hat höchstens ein Bild.</summary>
    public Guid DogId { get; set; }
    public Dog? Dog { get; set; }

    public byte[] Data { get; set; } = [];

    /// <summary>MIME-Typ der Daten, z.B. "image/jpeg".</summary>
    public string ContentType { get; set; } = string.Empty;
}
