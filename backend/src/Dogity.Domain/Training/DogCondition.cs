namespace Dogity.Domain.Training;

/// <summary>
/// Wie der Hund an diesem Trainingstag drauf war.
///
/// Bewusst eine kurze, einstufige Liste statt einer Skala: Sie soll mit EINEM
/// Tipp beim Eintragen gesetzt sein. Wer erst überlegen muss, trägt gar nichts
/// ein - und ein Feld, das niemand füllt, taugt auch für keine Auswertung.
///
/// <see cref="Settled"/> ist die Mitte. Ohne sie hätte ein ganz normaler,
/// unauffälliger Trainingstag keine ehrliche Antwort, und die Auswertung
/// bekäme lauter falsche "motiviert".
/// </summary>
public enum DogCondition
{
    /// <summary>Motiviert - zieht mit, arbeitet freudig.</summary>
    Motivated,

    /// <summary>Ausgeglichen - unauffällig, wie üblich.</summary>
    Settled,

    /// <summary>Abgelenkt - bei der Umwelt statt beim Hundeführer.</summary>
    Distracted,

    /// <summary>Müde - kraftlos, langsam, wenig Ausdauer.</summary>
    Tired,

    /// <summary>Gestresst - überdreht, unruhig, kann nicht abschalten.</summary>
    Stressed
}
