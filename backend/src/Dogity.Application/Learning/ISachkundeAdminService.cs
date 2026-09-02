using Dogity.Application.Common;

namespace Dogity.Application.Learning;

/// <summary>
/// Verwaltung der Fragenkataloge: alles ansehen und von Hand überarbeiten.
///
/// Die Kataloge stammen aus einer PDF-Auswertung. Die kommt weit, aber nicht
/// überall hin - Trennstriche, verschluckte Leerzeichen, umgebrochene Sätze.
/// Solche Stellen zieht nur ein Mensch glatt, und genau dafür ist das hier.
/// </summary>
public interface ISachkundeAdminService
{
    /// <summary>
    /// Alle Fragen zum Durchsehen, wahlweise gefiltert.
    /// </summary>
    /// <param name="catalogCode">Katalog eingrenzen (optional).</param>
    /// <param name="section">Themenkomplex eingrenzen (optional).</param>
    /// <param name="search">Volltext über Frage, Antworten und Musterlösung (optional).</param>
    /// <param name="onlyEdited">Nur die von Hand überarbeiteten Fragen.</param>
    /// <param name="onlyFlagged">
    /// Nur Fragen mit auffälligen Textstellen - der Vorschlag, wo sich das
    /// Nachsehen am ehesten lohnt (siehe <see cref="AdminQuizQuestionDto.Flags"/>).
    /// </param>
    Task<Result<IReadOnlyList<AdminQuizQuestionDto>>> GetQuestionsAsync(
        string? catalogCode, string? section, string? search, bool onlyEdited, bool onlyFlagged,
        CancellationToken ct = default);

    /// <summary>
    /// Überschreibt Text, Musterlösung und Antwortzeilen einer Frage und
    /// merkt sich, dass hier von Hand eingegriffen wurde - der Seeder lässt
    /// die Frage danach in Ruhe.
    /// </summary>
    Task<Result<AdminQuizQuestionDto>> UpdateQuestionAsync(
        Guid userId, Guid questionId, UpdateQuizQuestionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Nimmt die Handbearbeitung zurück. Die Katalogfassung kommt beim
    /// nächsten Start der Anwendung wieder (dann greift der Seeder erneut).
    /// </summary>
    Task<Result> RevertQuestionAsync(Guid questionId, CancellationToken ct = default);
}
