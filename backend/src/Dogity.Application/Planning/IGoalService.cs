using Dogity.Application.Common;
using Dogity.Domain.Planning;

namespace Dogity.Application.Planning;

public interface IGoalService
{
    Task<Result<IReadOnlyList<GoalDto>>> GetByDogAsync(Guid userId, Guid dogId, CancellationToken ct = default);
    Task<Result<GoalDto>> GetByIdAsync(Guid userId, Guid goalId, CancellationToken ct = default);
    Task<Result<GoalDto>> CreateAsync(Guid userId, CreateGoalRequest request, CancellationToken ct = default);
    Task<Result<GoalDto>> UpdateStatusAsync(Guid userId, Guid goalId, GoalStatus status, CancellationToken ct = default);

    /// <summary>
    /// Schließt ein aktives Ziel mit dem Prüfungsergebnis ab: bestanden (Ziel
    /// erreicht, Tag/Punkte/Notiz gespeichert) oder nicht bestanden (Ziel bleibt
    /// aktiv, neues Zieldatum). Die Rechte sind dieselben wie bei UpdateStatusAsync.
    /// </summary>
    Task<Result<GoalDto>> CompleteAsync(Guid userId, Guid goalId, CompleteGoalRequest request, CancellationToken ct = default);

    /// <summary>
    /// Trägt das Ergebnis eines bereits erreichten Ziels nach oder korrigiert
    /// es - auch für alte Ziele, die ohne Ergebnis als erreicht markiert wurden.
    /// </summary>
    Task<Result<GoalDto>> UpdateExamResultAsync(Guid userId, Guid goalId, UpdateExamResultRequest request, CancellationToken ct = default);

    /// <summary>
    /// Verschiebt das Zieldatum eines aktiven Ziels, ohne dass eine Prüfung
    /// stattgefunden hätte (anders als "nicht bestanden" in CompleteAsync). Der
    /// Plan wird dabei wie dort um die fehlenden Wochen verlängert. Nötig, wenn
    /// eine Trainingspause die Zeit bis zur Prüfung knapp macht.
    /// </summary>
    Task<Result<GoalDto>> UpdateTargetDateAsync(Guid userId, Guid goalId, DateOnly newTargetDate, CancellationToken ct = default);

    /// <summary>
    /// Setzt die Plan-Konfiguration eines Ziels (Übungen/Woche, Trainingstage/
    /// Woche) für den adaptiven Generator.
    /// </summary>
    Task<Result<GoalDto>> UpdateConfigAsync(Guid userId, Guid goalId, int weeklyExerciseCount, int trainingDaysPerWeek, CancellationToken ct = default);

    /// <summary>
    /// Setzt die Trainingstage für EINE Woche abweichend vom Plan-Default
    /// (siehe TrainingPlanWeekConfig). Bestehende Übungen der Woche auf einem
    /// entfallenden Tag werden auf den letzten gültigen Tag geholt.
    /// </summary>
    Task<Result<GoalDto>> UpdateWeekConfigAsync(Guid userId, Guid goalId, int weekNumber, int trainingDaysPerWeek, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid userId, Guid goalId, CancellationToken ct = default);

    /// <summary>
    /// Fügt dem Plan manuell ein weiteres Wochenziel hinzu (siehe TODO.md
    /// "Trainingsplan überarbeitet") - z.B. eine zweite Übung in derselben
    /// Woche oder eine zusätzliche Übungseinheit. Ersetzt einen reinen
    /// Pausenwochen-Platzhalter in der Zielwoche, falls vorhanden.
    /// </summary>
    Task<Result<GoalDto>> AddPlanItemAsync(Guid userId, Guid goalId, AddTrainingPlanItemRequest request, CancellationToken ct = default);
    Task<Result<GoalDto>> UpdatePlanItemAsync(Guid userId, Guid goalId, Guid itemId, UpdateTrainingPlanItemRequest request, CancellationToken ct = default);
    Task<Result<GoalDto>> RemovePlanItemAsync(Guid userId, Guid goalId, Guid itemId, CancellationToken ct = default);

    /// <summary>
    /// Generiert eine Woche des Plans adaptiv neu (siehe
    /// docs/SMART_TRAINING_PLAN.md, P4). Erhält manuelle/Trainer-Items und
    /// Auto-Items mit geloggtem Fortschritt; ersetzt nur fortschrittslose
    /// Auto-Items durch eine frische, mastery-basierte Auswahl.
    /// </summary>
    Task<Result<GoalDto>> RegenerateWeekAsync(Guid userId, Guid goalId, int weekNumber, CancellationToken ct = default);

    /// <summary>
    /// Die Prüfungsreife des Ziels (siehe <see cref="Pruefungsreife"/>). Rechte wie beim
    /// Plan: Besitzer:innen und betreuende Trainer:innen, sonst "nicht gefunden". Die
    /// Theoriezeile enthält nur, wer den Hund besitzt - nie die Trainer:in. Gibt es für das
    /// Ziel keine Prüfungsreife (eigenes, erreichtes oder beendetes Ziel, keine
    /// Prüfungsordnung), ist es ebenfalls "nicht gefunden".
    /// </summary>
    Task<Result<PruefungsreifeDto>> GetPruefungsreifeAsync(Guid userId, Guid goalId, CancellationToken ct = default);

    /// <summary>
    /// Alle gewichtbaren Übungen eines Ziels (PO- bzw. Sport-Katalog) mit ihrer
    /// aktuellen manuellen Gewichtung, Beherrschungs-Status und "diese Woche
    /// geplant?". Leer bei individuellen Zielen (kein adaptiver Plan).
    /// </summary>
    Task<Result<IReadOnlyList<WeightableExerciseDto>>> GetWeightableExercisesAsync(Guid userId, Guid goalId, CancellationToken ct = default);

    /// <summary>
    /// Setzt die manuelle Gewichtung ("mehr/weniger üben", −2..+2) einer Übung
    /// des Ziels. Wirkt ab dem nächsten (automatischen) Wochen-Neuaufbau.
    /// </summary>
    Task<Result> SetExercisePriorityAsync(Guid userId, Guid goalId, Guid exerciseId, int value, CancellationToken ct = default);

    /// <summary>
    /// System-Pass (kein Benutzerkontext): regeneriert für alle aktiven,
    /// nicht-individuellen Ziele die KOMMENDE Woche adaptiv neu, sofern sie
    /// fällig ist (LastPlanGeneratedAt null oder älter als ~6 Tage). Wird vom
    /// Hintergrund-Scheduler aufgerufen. Gibt die Anzahl regenerierter Ziele
    /// zurück.
    /// </summary>
    /// <summary>
    /// Schaltet die automatische wöchentliche Plan-Anpassung ein oder aus. Sie
    /// schaltet sich von selbst ab, sobald eine betreuende Trainer:in den Plan
    /// bearbeitet - hiermit lässt sie sich wieder einschalten.
    /// </summary>
    Task<Result<GoalDto>> SetPlanAutoRegenerationAsync(Guid userId, Guid goalId, bool enabled, CancellationToken ct = default);

    Task<int> RegenerateDuePlansAsync(CancellationToken ct = default);

    // ---------- Prüfungen des Vereins (Paket V4) ----------
    // Rechte hat der Aufrufer (ClubExamService) über die Vereinsrolle geprüft; diese Methoden prüfen
    // keinen Hundezugriff und speichern nicht - der Aufrufer speichert Eintrag und Ziel in einem Zug.

    /// <summary>Die Höchstpunktzahl je Prüfungsordnung; Ordnungen ohne Punktwertung fehlen.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetHoechstpunkteAsync(IReadOnlyCollection<Guid> regulationIds, CancellationToken ct = default);

    /// <summary>
    /// Die Regeln für ein Ergebnis wie beim Ergebnis eines Ziels (Prüfungstag nicht in der Zukunft, Punkte
    /// 0 bis Höchstpunktzahl der Ordnung, Länge der Notiz): die Fehlermeldung, sonst null.
    /// </summary>
    Task<string?> PruefeVereinsergebnisAsync(Guid regulationId, DateOnly tag, int? punkte, string? notiz, CancellationToken ct = default);

    /// <summary>
    /// "Bestanden" am Hund: Das laufende Ziel zur Ordnung wird erreicht (Prüfungstag, Punkte, Notiz); gibt es
    /// keins, entsteht ein erreichtes Ziel wie eine nachgetragene Leistung.
    /// </summary>
    Task<VereinsZiel> TrageVereinsergebnisAsync(Guid dogId, Guid regulationId, VereinsErgebnis ergebnis, CancellationToken ct = default);

    /// <summary>
    /// Zieht das Ziel nach, wenn die Prüfungsleitung ein Ergebnis ändert (<paramref name="neu"/>) oder zurücknimmt
    /// (<paramref name="neu"/> = null): ein dafür angelegtes Ziel wird gelöscht, ein vorher laufendes wieder aktiv
    /// ohne Prüfungsergebnis. Hat die Familie das Ziel inzwischen verändert oder gelöscht (erkannt daran, dass es nicht
    /// mehr genau dem <paramref name="bisher"/> eingetragenen Ergebnis entspricht), bleibt es unberührt - false.
    /// </summary>
    Task<bool> ZieheVereinsergebnisNachAsync(Guid goalId, Guid dogId, VereinsErgebnis bisher, VereinsErgebnis? neu, bool neuAngelegt, CancellationToken ct = default);
}
