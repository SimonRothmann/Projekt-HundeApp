namespace Dogity.Application.Notifications;

/// <summary>Welche Kontomail die Drossel prüft - jede Art hat ihr eigenes Tageskontingent.</summary>
public enum KontomailArt
{
    Willkommen,
    PasswortZuruecksetzen,
}

/// <summary>
/// Schutz vor Mail-Missbrauch bei den anonymen Endpunkten (Registrierung,
/// "Passwort vergessen"): Beide lösen eine Mail an eine beliebige Adresse aus,
/// ohne dass die Adresse bestätigt wäre. Die IP-Drosselung (Program.cs) bremst
/// nur einzelne Absender; die Drossel hier schützt die Postfächer und den Ruf
/// unseres eigenen Versandservers (deploy/mail) - eine Flut an Mails an fremde
/// Adressen brächte ihn schnell auf Sperrlisten.
///
/// - Reset-Mails: höchstens eine je Adresse und 10 Minuten. Die Antwort an die
///   anfragende Person bleibt gleich (sonst wäre die Adresse aufzählbar), nur die
///   Mail bleibt aus.
/// - Tageskontingent je Art (UTC-Tag), getrennt für Willkommen und Reset: Eine
///   Registrierungsflut verbraucht so nicht das, was Passwort-Resets brauchen.
///
/// Nur im Speicher, wie die Warteschlange: Ein Neustart setzt die Zähler zurück.
/// Als Singleton registriert.
/// </summary>
public class MailDrossel(TimeProvider time)
{
    private static readonly TimeSpan ResetAbstand = TimeSpan.FromMinutes(10);
    private const int WillkommenProTag = 100;
    private const int ResetProTag = 100;

    // Ab dieser Größe werden abgelaufene Einträge weggeräumt, damit viele
    // verschiedene Adressen den Speicher nicht dauerhaft füllen.
    private const int AufraeumenAb = 1000;

    private readonly object _sperre = new();
    private readonly Dictionary<string, DateTimeOffset> _letzteResetMail = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<KontomailArt, int> _heute = [];
    private DateOnly _tag;

    /// <summary>true, wenn die Mail gesendet werden darf - und zählt sie dann mit.</summary>
    public bool Erlaubt(KontomailArt art, string adresse)
    {
        var jetzt = time.GetUtcNow();
        var tag = DateOnly.FromDateTime(jetzt.UtcDateTime);

        lock (_sperre)
        {
            if (tag != _tag)
            {
                _tag = tag;
                _heute.Clear();
            }

            var bisher = _heute.GetValueOrDefault(art);
            if (bisher >= (art == KontomailArt.Willkommen ? WillkommenProTag : ResetProTag))
                return false;

            if (art == KontomailArt.PasswortZuruecksetzen)
            {
                if (_letzteResetMail.TryGetValue(adresse, out var zuletzt) && jetzt - zuletzt < ResetAbstand)
                    return false;

                if (_letzteResetMail.Count >= AufraeumenAb)
                {
                    foreach (var abgelaufen in _letzteResetMail.Where(e => jetzt - e.Value >= ResetAbstand).Select(e => e.Key).ToList())
                        _letzteResetMail.Remove(abgelaufen);
                }
                _letzteResetMail[adresse] = jetzt;
            }

            _heute[art] = bisher + 1;
            return true;
        }
    }
}
