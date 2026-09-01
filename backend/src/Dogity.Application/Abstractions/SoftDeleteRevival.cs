using System.Linq.Expressions;
using Dogity.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Abstractions;

/// <summary>
/// Hilfsmittel für Tabellen mit Soft-Delete UND eindeutigem Index.
///
/// Das Muster hat in dieser App schon einmal zugeschlagen (siehe
/// <see cref="RegulationExerciseQueries"/>): Entfernt wird über
/// <c>DeletedAt</c>, der eindeutige Index kennt aber kein <c>DeletedAt</c>.
/// Eine gewöhnliche Abfrage findet die entfernte Zeile nicht mehr, der Code
/// legt eine zweite an - und die Datenbank weist sie zurück. Für den Aufrufer
/// ist das ein 500er ohne verwertbare Meldung.
///
/// Betroffen ist jedes Paar, das entfernt und später wieder angelegt werden
/// kann: Gruppenmitglieder, Vereinstrainer, Mitbesitzer eines Hundes.
/// </summary>
public static class SoftDeleteRevival
{
    /// <summary>
    /// Sucht eine Zeile einschließlich der weichgelöschten.
    /// </summary>
    /// <returns>
    /// <c>Row</c> ist die gefundene Zeile (oder <c>null</c>, wenn es sie noch
    /// nie gab), <c>IsActive</c> sagt, ob sie gerade gilt. Der übliche Ablauf:
    /// bei <c>IsActive</c> die "gibt es schon"-Meldung liefern, bei einer
    /// vorhandenen aber entfernten Zeile <c>DeletedAt</c> auf <c>null</c>
    /// setzen, sonst neu anlegen.
    /// </returns>
    public static async Task<(TEntity? Row, bool IsActive)> FindIncludingRemovedAsync<TEntity>(
        this DbSet<TEntity> set,
        Expression<Func<TEntity, bool>> match,
        CancellationToken ct = default)
        where TEntity : Entity
    {
        var row = await set.IgnoreQueryFilters().FirstOrDefaultAsync(match, ct);
        return (row, row is not null && row.DeletedAt is null);
    }
}
