using Dogity.Application.Common;

namespace Dogity.Application.Account;

/// <summary>
/// Auskunft und Löschung für ein einzelnes Konto - die technische Seite von
/// Art. 15, 17 und 20 DSGVO.
///
/// Bewusst ein Application-Service und nicht (wie Namensänderung oder
/// Passwortwechsel im ProfileController) direkt an Identity gehängt: Beides
/// berührt zwei Dutzend fachliche Tabellen, von den Hunden über die
/// Fährtenpunkte bis zu den Vereinsrollen. Das ist ein fachlicher Use Case
/// und gehört damit hierher, wo er ohne Identity-System testbar ist.
///
/// Das Identity-Konto selbst löscht weiterhin der Aufrufer über
/// <c>IUserLookupService.DeleteUserAsync</c> - erst die Fachdaten, dann das
/// Konto, sonst verwaisen die Zeilen (siehe CommunityOrphanCleanup).
/// </summary>
public interface IAccountDataService
{
    /// <summary>Alles, was über diesen Menschen gespeichert ist, in einer lesbaren Struktur.</summary>
    Task<Result<AccountExportDto>> ExportAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Entfernt die personenbezogenen Daten des Kontos aus allen Fachtabellen.
    /// Idempotent: Ein zweiter Lauf findet nichts mehr und meldet Erfolg.
    /// </summary>
    Task<Result> PurgeAsync(Guid userId, CancellationToken ct = default);
}
