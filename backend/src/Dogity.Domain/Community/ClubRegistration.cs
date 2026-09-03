using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

public enum ClubRegistrationStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>
/// Der Antrag, einen Verein anzulegen.
///
/// Warum überhaupt ein Antrag und nicht direktes Anlegen: Vereinsnamen sind
/// real vergeben. Wer "Hundesportverein Musterstadt e.V." anlegt, ohne dazu
/// zu gehören, besetzt einen Namen, unter dem sich später die echten
/// Mitglieder sammeln würden - und niemand außer einem globalen Admin käme
/// da wieder heraus. Das ist kein technisches, sondern ein
/// Vertrauensproblem; eine Warteschlange mit einem Klick löst es
/// (Entscheidung des Auftraggebers, siehe
/// docs/VERBAENDE_SPRACHEN_MODULE.md).
///
/// Bewusst eine EIGENE Tabelle und kein Status am Verein selbst: Ein noch
/// nicht freigegebener Verein soll gar nicht erst als Verein existieren.
/// Sonst müsste jede vorhandene Abfrage auf Vereine - Liste, Beitritt,
/// Sichtbarkeit vereinseigener Übungen - um einen Statusfilter ergänzt
/// werden, und die eine vergessene Stelle wäre das Leck.
/// </summary>
public class ClubRegistration : Entity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid RequestedByUserId { get; set; }

    public ClubRegistrationStatus Status { get; set; } = ClubRegistrationStatus.Pending;

    public DateTimeOffset? DecidedAt { get; set; }
    public Guid? DecidedByUserId { get; set; }

    /// <summary>Begründung bei Ablehnung - der Antragsteller soll wissen, warum.</summary>
    public string? DecisionNote { get; set; }

    /// <summary>Nach der Freigabe der daraus entstandene Verein.</summary>
    public Guid? ClubId { get; set; }
}
