using Dogity.Domain.Common;

namespace Dogity.Domain.Betrieb;

/// <summary>
/// Die Ankündigung einer längeren Wartung, die der Betreiber in der App einblendet.
/// Es gibt höchstens eine Zeile: Wer einen neuen Hinweis setzt, überschreibt den
/// vorhandenen - eine Liste vergangener Wartungen braucht niemand.
///
/// Das ist nur die Ankündigung VOR der Wartung, solange das Backend noch läuft.
/// Während der Wartung antwortet der Proxy selbst (deploy/caddy/wartung), weil dann
/// keine Datenbank mehr erreichbar ist.
/// </summary>
public class Wartungshinweis : Entity
{
    /// <summary>Eigener Text für das Banner; null = Standardtext des Frontends.</summary>
    public string? Text { get; set; }

    /// <summary>Wann die Wartung beginnt. Ab hier gilt der Hinweis als "läuft".</summary>
    public DateTimeOffset Beginn { get; set; }

    /// <summary>Wann sie voraussichtlich endet; null = offen. Danach verschwindet der Hinweis von selbst.</summary>
    public DateTimeOffset? Ende { get; set; }

    /// <summary>Ob der Hinweis eingeblendet wird.</summary>
    public bool Aktiv { get; set; }

    /// <summary>
    /// Letzte Änderung durch den Betreiber. Das Frontend merkt sich damit, ob jemand
    /// diesen Hinweis schon weggeklickt hat - ein geänderter Hinweis erscheint wieder.
    /// </summary>
    public DateTimeOffset GeaendertAm { get; set; }
}
