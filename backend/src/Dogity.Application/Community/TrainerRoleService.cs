using Dogity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

/// <summary>
/// Hält die Identity-Rolle TRAINER mit der Datenlage im Gleichklang.
///
/// Hintergrund: Trainer:in-Sein ist in dieser App datengetrieben - wer eine
/// Gruppe leitet, in einer Gruppe mit-betreut oder einem Verein zugewiesen
/// ist, bekommt die Trainer-Ansicht. Die Rolle TRAINER existierte zwar
/// (siehe Roles.All) und wurde in der Admin-Übersicht als Kennzeichen
/// angezeigt, aber sie wurde nirgends vergeben: Eine frisch ernannte
/// Trainer:in stand dort weiter nur als "USER". Genau diese Lücke schließt
/// dieser Dienst.
///
/// Die Rolle ist bewusst reine Anzeige - kein einziger Endpunkt autorisiert
/// über sie (Zugriffe laufen immer über die konkrete Zuordnung). Ein
/// verspäteter Abgleich kann deshalb nichts aufsperren, was vorher zu war.
/// </summary>
public interface ITrainerRoleService
{
    /// <summary>
    /// Bestimmt neu, ob jemand Trainer:in ist, und setzt die Rolle
    /// entsprechend. Immer nach Änderungen an Gruppen- oder
    /// Vereins-Trainerzuordnungen aufrufen.
    /// </summary>
    Task SyncAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Wie <see cref="SyncAsync"/>, aber für mehrere Betroffene auf einmal.</summary>
    Task SyncAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);

    /// <summary>
    /// Gleicht die Rolle für alle ab, die sie haben oder haben müssten.
    /// Läuft beim Start und holt damit alle Trainer:innen nach, die es schon
    /// vor diesem Abgleich gab. Idempotent - wer schon stimmt, wird nicht
    /// angefasst.
    /// </summary>
    Task BackfillAsync(CancellationToken ct = default);
}

public class TrainerRoleService(IApplicationDbContext db, IUserLookupService userLookup) : ITrainerRoleService
{
    // Einzige Stelle in Application, die den Rollennamen kennt - und auch nur,
    // weil ListUserIdsInRoleAsync ihn als Zeichenkette verlangt. Gesetzt wird
    // die Rolle über SetTrainerRoleAsync, das ohne Namen auskommt.
    private const string TrainerRoleName = "TRAINER";


    public async Task SyncAsync(Guid userId, CancellationToken ct = default)
    {
        // Neu berechnen statt mitzuzählen: Ein Zähler, der bei jeder Änderung
        // hoch- und runtergesetzt wird, läuft irgendwann auseinander - diese
        // Abfrage ist immer richtig und kostet drei Index-Zugriffe.
        var isTrainer = await db.IsAnyTrainerAsync(userId, ct);
        await userLookup.SetTrainerRoleAsync(userId, isTrainer, ct);
    }

    public async Task SyncAsync(IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        foreach (var userId in userIds.Distinct())
            await SyncAsync(userId, ct);
    }

    public async Task BackfillAsync(CancellationToken ct = default)
    {
        // Beide Richtungen: wer laut Daten Trainer:in ist, bekommt die Rolle -
        // und wer sie noch trägt, ohne es zu sein, verliert sie wieder.
        var affected = await db.Groups.Select(g => g.TrainerId).ToListAsync(ct);
        affected.AddRange(await db.GroupTrainers.Select(t => t.UserId).ToListAsync(ct));
        affected.AddRange(await db.ClubTrainers.Select(t => t.UserId).ToListAsync(ct));
        affected.AddRange(await userLookup.ListUserIdsInRoleAsync(TrainerRoleName, ct));

        await SyncAsync(affected, ct);
    }
}
