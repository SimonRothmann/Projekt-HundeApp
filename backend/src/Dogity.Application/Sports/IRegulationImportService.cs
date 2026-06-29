using Dogity.Application.Abstractions;
using Dogity.Application.Common;

namespace Dogity.Application.Sports;

/// <summary>
/// Admin-Workflow zum Aktualisieren des Übungskatalogs aus der lokalen
/// Prüfungsordnungs-PDF: erst scannen (Fakten-Vorschläge), dann gezielt
/// einzelne Vorschläge bestätigen (siehe TODO.md "Freigabe vom Admin für
/// einzelne Übungen einholen"). Kein automatisches Schreiben ohne
/// Bestätigung.
/// </summary>
public interface IRegulationImportService
{
    Task<Result<IReadOnlyList<ParsedExerciseCandidate>>> ScanAsync(CancellationToken ct = default);
    Task<Result> ApplyAsync(ApplyRegulationImportRequest request, CancellationToken ct = default);
}
