using System.Globalization;
using System.Text.RegularExpressions;
using Dogity.Application.Preferences;

namespace Dogity.Application.Common;

/// <summary>
/// Eine Benachrichtigung vor dem Rendern: deutscher Satz mit Platzhaltern
/// <c>{name}</c> plus die Werte dazu.
///
/// Der Server weiß beim Auslösen nicht, in welcher Sprache die Empfänger:in liest -
/// dasselbe Ereignis geht oft an mehrere Personen. Erst der NotificationService
/// schlägt die Sprache nach und macht daraus den Text (siehe <see cref="Uebersetzung"/>).
///
/// Die Vorlage ist immer ein Zeichenketten-Literal und ein vollständiger Satz:
/// Satzteile als Werte ließen sich nicht übersetzen, die Wortstellung ist je
/// Sprache anders. Fälle mit und ohne Angabe sind deshalb zwei Vorlagen.
/// </summary>
/// <param name="Werte">Texte, Zahlen und <see cref="DateOnly"/> (wird je Sprache als Datum geschrieben).</param>
public sealed record Meldung(string Vorlage, IReadOnlyDictionary<string, object>? Werte = null)
{
    public static Meldung Von(string vorlage, params (string Name, object Wert)[] werte) =>
        new(vorlage, werte.Length == 0 ? null : werte.ToDictionary(w => w.Name, w => w.Wert));
}

/// <summary>
/// Übersetzung auf dem Server, nach dem Muster des Frontends (lib/i18n): Der
/// deutsche Satz mit Platzhaltern ist der Schlüssel, das Wörterbuch liefert die
/// englische Fassung, und was fehlt, bleibt Deutsch. Der Rückfall ist also
/// immer ein lesbarer Satz; <c>UebersetzungTests</c> sorgt dafür, dass er nie nötig wird.
///
/// Eigene Datei für die englischen Sätze statt neben den Aufrufern: So steht
/// die ganze Sprachfassung an einer Stelle zum Gegenlesen.
/// </summary>
public static partial class Uebersetzung
{
    /// <summary>Schlüssel = deutsche Vorlage, Wert = englische Fassung mit denselben Platzhaltern.</summary>
    public static readonly IReadOnlyDictionary<string, string> Englisch = new Dictionary<string, string>
    {
        // Vereinsantrag (ClubRegistrationService)
        ["Neuer Vereinsantrag: \"{name}\"."] = "New club request: \"{name}\".",
        ["Dein Verein \"{verein}\" wurde freigegeben. Du verwaltest ihn."] = "Your club \"{verein}\" has been approved. You manage it.",
        ["Dein Vereinsantrag \"{verein}\" wurde abgelehnt."] = "Your club request \"{verein}\" was declined.",
        ["Dein Vereinsantrag \"{verein}\" wurde abgelehnt. Begründung: {grund}"] = "Your club request \"{verein}\" was declined. Reason: {grund}",

        // Verein (ClubService)
        ["Du wurdest zum Verein \"{verein}\" hinzugefügt."] = "You have been added to the club \"{verein}\".",
        ["{name} lädt dich in den Verein \"{verein}\" ein."] = "{name} invites you to the club \"{verein}\".",
        ["Eine Trainer:in lädt dich in den Verein \"{verein}\" ein."] = "A trainer invites you to the club \"{verein}\".",
        ["{name} möchte dem Verein \"{verein}\" beitreten."] = "{name} wants to join the club \"{verein}\".",
        ["Jemand möchte dem Verein \"{verein}\" beitreten."] = "Someone wants to join the club \"{verein}\".",
        ["Dein Beitritt zu \"{verein}\" wurde abgelehnt."] = "Your request to join \"{verein}\" was declined.",
        ["Dein Beitritt zu \"{verein}\" wurde angenommen."] = "Your request to join \"{verein}\" was accepted.",
        ["Dein Beitritt zu \"{verein}\" wurde angenommen. Du bist jetzt Mitglied der Gruppe \"{gruppe}\"."] = "Your request to join \"{verein}\" was accepted. You are now a member of the group \"{gruppe}\".",
        ["Dein Beitritt zu \"{verein}\" wurde angenommen. Du bist in die Gruppe \"{gruppe}\" eingeladen - einmal tippen zum Annehmen."] = "Your request to join \"{verein}\" was accepted. You are invited to the group \"{gruppe}\" - tap once to accept.",
        ["Du bist jetzt Trainer bei \"{verein}\"."] = "You are now a trainer at \"{verein}\".",

        // Gruppe (GroupService, GroupRegistrationFormService, GroupTrainingScheduleService)
        ["{name} lädt dich ein, Trainer:in der Gruppe \"{gruppe}\" zu werden."] = "{name} invites you to become a trainer of the group \"{gruppe}\".",
        ["Eine Trainer:in lädt dich ein, Trainer:in der Gruppe \"{gruppe}\" zu werden."] = "A trainer invites you to become a trainer of the group \"{gruppe}\".",
        ["{name} lädt dich in die Gruppe \"{gruppe}\" ein."] = "{name} invites you to the group \"{gruppe}\".",
        ["Eine Trainer:in lädt dich in die Gruppe \"{gruppe}\" ein."] = "A trainer invites you to the group \"{gruppe}\".",
        ["{name} betreut jetzt {hund} und sieht Tagebuch, Ziele und Fährten."] = "{name} now supervises {hund} and can see the diary, goals and tracks.",
        ["Eine Trainer:in betreut jetzt {hund} und sieht Tagebuch, Ziele und Fährten."] = "A trainer now supervises {hund} and can see the diary, goals and tracks.",
        ["{name} betreut jetzt deinen Hund und sieht Tagebuch, Ziele und Fährten."] = "{name} now supervises your dog and can see the diary, goals and tracks.",
        ["Eine Trainer:in betreut jetzt deinen Hund und sieht Tagebuch, Ziele und Fährten."] = "A trainer now supervises your dog and can see the diary, goals and tracks.",
        ["Dein Beitritt zur Gruppe \"{gruppe}\" wurde angenommen."] = "Your request to join the group \"{gruppe}\" was accepted.",
        ["Dein Beitritt zur Gruppe \"{gruppe}\" wurde abgelehnt."] = "Your request to join the group \"{gruppe}\" was declined.",
        ["Neue Anmeldung für {gruppe}: {hund} ({rasse})."] = "New registration for {gruppe}: {hund} ({rasse}).",
        ["{name} hat für {gruppe} am {datum} abgesagt."] = "{name} can't make it to {gruppe} on {datum}.",
        ["Ein Mitglied hat für {gruppe} am {datum} abgesagt."] = "A member can't make it to {gruppe} on {datum}.",
        ["Das Training {gruppe} am {datum} fällt aus."] = "The {gruppe} training on {datum} is cancelled.",

        // Hund (DogService)
        ["{name} lädt dich ein, {hund} gemeinsam zu verwalten."] = "{name} invites you to manage {hund} together.",
        ["Jemand lädt dich ein, {hund} gemeinsam zu verwalten."] = "Someone invites you to manage {hund} together.",
        ["{name} lädt dich ein, einen Hund gemeinsam zu verwalten."] = "{name} invites you to manage a dog together.",
        ["Jemand lädt dich ein, einen Hund gemeinsam zu verwalten."] = "Someone invites you to manage a dog together.",
        ["{name} hat dich als Mitbesitzer:in von {hund} entfernt."] = "{name} has removed you as a co-owner of {hund}.",
        ["Eine Besitzer:in hat dich als Mitbesitzer:in von {hund} entfernt."] = "An owner has removed you as a co-owner of {hund}.",
        ["{name} hat dich als Mitbesitzer:in von einem Hund entfernt."] = "{name} has removed you as a co-owner of a dog.",
        ["Eine Besitzer:in hat dich als Mitbesitzer:in von einem Hund entfernt."] = "An owner has removed you as a co-owner of a dog.",
        ["{name} hat die Betreuung von {hund} beendet."] = "{name} has ended the supervision of {hund}.",
        ["Eine Besitzer:in hat die Betreuung von {hund} beendet."] = "An owner has ended the supervision of {hund}.",
        ["{name} hat die Betreuung von einem Hund beendet."] = "{name} has ended the supervision of a dog.",
        ["Eine Besitzer:in hat die Betreuung von einem Hund beendet."] = "An owner has ended the supervision of a dog.",

        // Training und Ziele (TrainingService, GoalService)
        ["Dein Trainer hat Feedback zu deinem Training vom {datum} hinterlassen."] = "Your trainer left feedback on your training session from {datum}.",
        ["Dein Trainer hat Feedback zum Training von {hund} vom {datum} hinterlassen."] = "Your trainer left feedback on {hund}'s training session from {datum}.",
        ["{name} hat auf dein Feedback zum Training vom {datum} geantwortet."] = "{name} has replied to your feedback on the training session from {datum}.",
        ["Jemand hat auf dein Feedback zum Training vom {datum} geantwortet."] = "Someone has replied to your feedback on the training session from {datum}.",
        ["Der Trainingsplan für {hund} wurde für Woche {woche} automatisch angepasst."] = "The training plan for {hund} was adjusted automatically for week {woche}.",
        ["Der Trainingsplan für deinen Hund wurde für Woche {woche} automatisch angepasst."] = "The training plan for your dog was adjusted automatically for week {woche}.",

        // Erinnerungen (ErinnerungService, Regeln in Erinnerungsregeln)
        ["Morgen {zeit}: {gruppe} - du hast zugesagt."] = "Tomorrow {zeit}: {gruppe} - you have confirmed.",
        ["Morgen {zeit}: {gruppe} - kommst du?"] = "Tomorrow {zeit}: {gruppe} - are you coming?",
        ["Die Fährte von {hund} ist noch nicht abgelaufen. Du kannst sie noch bis {datum}, {zeit} Uhr ablaufen."] = "The track for {hund} has not been walked yet. You can still walk it until {zeit} on {datum}.",
        ["Ein Training wartet auf deine Rückmeldung."] = "One training session is waiting for your feedback.",
        ["{n} Trainings warten auf deine Rückmeldung."] = "{n} training sessions are waiting for your feedback.",
        ["{hund}: Die Impfung {titel} ist bis {datum} gültig."] = "{hund}: The {titel} vaccination is valid until {datum}.",
        ["{hund}: Die Impfung {titel} läuft heute ab."] = "{hund}: The {titel} vaccination expires today.",
        ["{hund}: {titel} ist am {datum} fällig."] = "{hund}: {titel} is due on {datum}.",
        ["{hund}: {titel} ist heute fällig."] = "{hund}: {titel} is due today.",
        ["Morgen {zeit}: Tierarzt mit {hund} ({titel})."] = "Tomorrow {zeit}: vet appointment with {hund} ({titel}).",
        ["Morgen: Tierarzt mit {hund} ({titel})."] = "Tomorrow: vet appointment with {hund} ({titel}).",

        // Konto (AccountMailService, PushTestService) - gehen an Admins
        ["{name} ({email}) möchte das Passwort zurücksetzen."] = "{name} ({email}) wants to reset their password.",
        ["Test-Push aus dem Adminbereich: Kommt diese Mitteilung an, funktioniert Push auf deinen Geräten."] = "Test push from the admin area: if this message arrives, push works on your devices.",
    };

    /// <summary>Der Satz in der Sprache der Person: Platzhalter ersetzt, Datumswerte nach Landesbrauch.</summary>
    public static string Rendere(Meldung meldung, string? locale)
    {
        var englisch = Sprachen.IstEnglisch(locale);
        var vorlage = englisch && Englisch.TryGetValue(meldung.Vorlage, out var fassung) ? fassung : meldung.Vorlage;
        return Einsetzen(vorlage, meldung.Werte, englisch);
    }

    /// <summary>
    /// Datum in der Sprache: deutsch 12.09.2026, englisch 12 Sep 2026. Englisch mit
    /// InvariantCulture statt "en-GB": Die Monatskürzel der Landeskultur hängen von
    /// der ICU-Version des Servers ab (en-GB schreibt "Sept"), ein Satz in einer
    /// Benachrichtigung soll auf jedem Rechner gleich aussehen.
    /// </summary>
    public static string Datum(DateOnly datum, bool englisch) =>
        datum.ToString(englisch ? "d MMM yyyy" : "dd.MM.yyyy", CultureInfo.InvariantCulture);

    // Wie einsetzen() im Frontend: Unbekannte Platzhalter bleiben stehen, statt zu verschwinden.
    private static string Einsetzen(string vorlage, IReadOnlyDictionary<string, object>? werte, bool englisch)
    {
        if (werte is null) return vorlage;
        return Platzhalter().Replace(vorlage, treffer =>
        {
            if (!werte.TryGetValue(treffer.Groups[1].Value, out var wert)) return treffer.Value;
            return wert switch
            {
                DateOnly datum => Datum(datum, englisch),
                // Zeitpunkte zählen hier nur als Tag. Ohne diese Zeilen fielen sie in
                // den Zweig darunter und kämen im US-Format samt Uhrzeit in den Satz.
                DateTime zeitpunkt => Datum(DateOnly.FromDateTime(zeitpunkt), englisch),
                DateTimeOffset zeitpunkt => Datum(DateOnly.FromDateTime(zeitpunkt.DateTime), englisch),
                IFormattable zahl => zahl.ToString(null, CultureInfo.InvariantCulture),
                _ => wert.ToString() ?? "",
            };
        });
    }

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Platzhalter();
}
