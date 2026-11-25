namespace Dogity.Application.Weather;

/// <summary>Welches Wetterfeld ein Auftrag füllt.</summary>
public enum WetterZiel
{
    /// <summary>Fährte: Wetter zum Zeitpunkt des Legens.</summary>
    FaehrteGelegt,

    /// <summary>Fährte: Wetter zum Zeitpunkt des Suchens (Beginn des ersten Ablaufs).</summary>
    FaehrteGesucht,

    /// <summary>Training: Wetter zu Datum und Startzeit.</summary>
    Training,

    /// <summary>
    /// Schweißfährte/Trail: Regen und Temperaturspanne vom Ende des Legens bis zum Beginn
    /// des ersten Ablaufs (Zeitpunkt = Ende des Legens, Ende = Beginn des Ablaufs).
    /// </summary>
    FaehrteStehzeit
}

/// <summary>
/// Eine Wetterabfrage, die nach dem Speichern im Hintergrund läuft. Trägt nur,
/// was die Abfrage braucht - Ort und Zeitpunkt, wie sie beim Einreihen galten.
/// Der Hintergrunddienst prüft vor dem Eintragen, ob der Datensatz noch genau
/// so aussieht (siehe <see cref="WetterNachtrag"/>); wurde er inzwischen
/// geändert oder gelöscht, wird der Auftrag verworfen.
/// </summary>
/// <param name="Id">Die Fährte bzw. das Training.</param>
/// <param name="Ende">Nur bei <see cref="WetterZiel.FaehrteStehzeit"/>: Ende des Zeitraums, der bei <c>Zeitpunkt</c> beginnt.</param>
public record WetterAuftrag(WetterZiel Ziel, Guid Id, double Breite, double Laenge, DateTimeOffset Zeitpunkt, DateTimeOffset? Ende = null);

/// <summary>
/// Übergabe an die Wetterabfrage im Hintergrund. Reiht nur ein und kehrt sofort
/// zurück: Wer speichert, wartet nie auf Open-Meteo. Bauart und Begründung wie
/// <see cref="Notifications.IMailQueue"/> - geht bei einem Neustart ein Auftrag
/// verloren, fehlt dem Eintrag nur das Wetter (Anreicherung), und bei einer
/// Fährte lässt es sich per Knopf nachholen.
/// </summary>
public interface IWetterQueue
{
    /// <returns>false, wenn die Warteschlange voll ist und der Auftrag verworfen wurde.</returns>
    bool Enqueue(WetterAuftrag auftrag);
}
