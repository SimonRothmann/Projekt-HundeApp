using Dogity.Application.Common;

namespace Dogity.Application.Tracking;

public interface IGpsTrackService
{
    Task<Result<IReadOnlyList<GpsTrackDto>>> GetByTrainingSessionAsync(Guid userId, Guid trainingSessionId, CancellationToken ct = default);
    Task<Result<GpsTrackDto>> CreateAsync(Guid userId, CreateGpsTrackRequest request, CancellationToken ct = default);
    Task<Result<GpsWalkRunDto>> AddWalkRunAsync(Guid userId, Guid trackId, CreateGpsWalkRunRequest request, CancellationToken ct = default);
    /// <summary>
    /// Berechnet die Auswertung eines Ablaufs neu (Abweichung, Gegenstände,
    /// Stockungen) - z.B. nachdem Marker-Typen korrigiert wurden.
    /// </summary>
    Task<Result<GpsWalkRunDto>> EvaluateWalkRunAsync(Guid userId, Guid trackId, Guid walkRunId, CancellationToken ct = default);

    Task<Result<GpsWalkRunDto>> UpdateWalkRunAsync(Guid userId, Guid trackId, Guid walkRunId, UpdateGpsWalkRunRequest request, CancellationToken ct = default);
    /// <summary>
    /// Holt das Wetter zu Lege- und Suchzeitpunkt (neu). Für Bestandsfährten,
    /// die vor der Wetter-Anbindung aufgezeichnet wurden - Ort und Zeit stecken
    /// bereits in den Punkten, es muss also nichts eingetippt werden.
    /// </summary>
    Task<Result<GpsTrackDto>> RefreshWeatherAsync(Guid userId, Guid trackId, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid userId, Guid trackId, CancellationToken ct = default);
}
