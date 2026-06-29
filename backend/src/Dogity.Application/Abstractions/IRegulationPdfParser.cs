using Dogity.Application.Common;

namespace Dogity.Application.Abstractions;

/// <summary>
/// Ein per Text-Scan in der lokalen Prüfungsordnungs-PDF gefundener
/// Kandidat: nur Übungsname + Punktzahl (Fakten, nicht die geschützte
/// Beschreibungsprosa). Muss von einem Admin geprüft und bestätigt werden,
/// bevor daraus ein Exercise/RegulationExercise-Eintrag entsteht.
/// </summary>
public record ParsedExerciseCandidate(string Name, int MaxPoints);

/// <summary>
/// Liest die lokale, urheberrechtlich geschützte aber zur Nutzung
/// freigegebene Prüfungsordnungs-PDF (siehe .gitignore "/Prüfungsordnung/")
/// und extrahiert nur Fakten (Übungsname + Punktzahl) per Texterkennung -
/// kein Kopieren der Beschreibungsprosa. Implementierung lebt in
/// Infrastructure (ruft das externe Tool "pdftotext" auf).
/// </summary>
public interface IRegulationPdfParser
{
    Task<Result<IReadOnlyList<ParsedExerciseCandidate>>> ScanAsync(CancellationToken ct = default);
}
