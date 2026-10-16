namespace Dogity.Application.Common;

/// <summary>
/// Höchstlängen der Freitexte - eine Quelle für die Spalten (EF-Konfiguration)
/// und die Prüfungen in den Services. Das Frontend spiegelt sie in
/// <c>frontend/src/lib/textlaengen.ts</c> als maxLength.
///
/// Vorher kannte nur die Datenbank die Grenzen: Ein zu langer Text scheiterte
/// erst beim Speichern, als 500er ohne Meldung - und hielt als offline
/// erfasster Eintrag die ganze Warteschlange an (siehe
/// Dogity.Api/Hosting/Fehlerantworten.cs für das Netz darunter).
/// </summary>
public static class Textlaengen
{
    public const int TrainingsNotiz = 4000;
    public const int UebungsNotiz = 2000;
    public const int TrainerRueckmeldung = 2000;
    /// <summary>TrainingSession.OwnerReply: Rückfrage des Besitzers zum Trainer-Feedback.</summary>
    public const int FeedbackRueckfrage = 500;
    public const int EigeneUebung = 150;
    public const int FaehrtenKommentar = 2000;
    public const int MarkerBeschriftung = 200;
    /// <summary>Untergrund, Wetter, Wind einer Fährte.</summary>
    public const int Kurzangabe = 100;
    /// <summary>Goal.ExamNote: Anmerkung zum Prüfungsergebnis.</summary>
    public const int PruefungsNotiz = 500;
    /// <summary>GroupRegistration: Vor-/Nachname der Angemeldeten und Rufname des Hundes.</summary>
    public const int Anmeldename = 100;
    /// <summary>Dog.Name: Rufname des Hundes.</summary>
    public const int Hundename = 100;
    /// <summary>Dog.Breed und GroupRegistration.DogBreed.</summary>
    public const int Hunderasse = 100;
    /// <summary>GroupRegistration.Phone.</summary>
    public const int Telefon = 30;
    /// <summary>GroupRegistration.Notes: Notiz der Trainer:innen zur Anmeldung.</summary>
    public const int AnmeldeNotiz = 500;
    /// <summary>PushSubscription.Endpoint: Adresse beim Push-Dienst (kein Freitext, vom Browser geliefert).</summary>
    public const int PushEndpoint = 2048;
    /// <summary>PushSubscription.P256dh/Auth: Schlüssel des Geräts (base64url, 87 bzw. 22 Zeichen).</summary>
    public const int PushSchluessel = 200;
    /// <summary>PushSubscription.UserAgent: Kurzbezeichnung des Geräts, vom Server gebildet.</summary>
    public const int GeraeteBezeichnung = 200;
    /// <summary>Sport.NameEn: englischer Name, so lang wie <c>Sport.Name</c>.</summary>
    public const int SportNameEn = 100;
    /// <summary>Regulation.NameEn: englischer Name, so lang wie <c>Regulation.Name</c>.</summary>
    public const int OrdnungNameEn = 100;
    /// <summary>Exercise.NameEn: englischer Name, so lang wie <c>Exercise.Name</c>.</summary>
    public const int UebungNameEn = 150;
    /// <summary>Suchbegriff der Admin-Nutzerliste (wird nicht gespeichert).</summary>
    public const int NutzerSuche = 100;

    /// <summary>
    /// Meldung, wenn <paramref name="text"/> (ohne Rand-Leerzeichen, so wie er
    /// gespeichert wird) länger ist als erlaubt - sonst null.
    /// </summary>
    public static string? ZuLang(string? text, int hoechstens, string was) =>
        text is not null && text.Trim().Length > hoechstens
            ? $"{was} ist zu lang (höchstens {hoechstens} Zeichen)."
            : null;
}
