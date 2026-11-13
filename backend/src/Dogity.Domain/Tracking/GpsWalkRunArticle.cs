using Dogity.Domain.Common;

namespace Dogity.Domain.Tracking;

/// <summary>
/// Was der Hund mit einem gelegten Gegenstand gemacht hat. Das weiß nur der Mensch: GPS sieht
/// höchstens, dass das Gespann in die Nähe kam (<see cref="GpsWalkRun.ArticlesFound"/>).
/// </summary>
public enum ArticleResult
{
    /// <summary>Verwiesen: Der Hund hat den Gegenstand angezeigt.</summary>
    Indicated,
    /// <summary>Aufgenommen: Der Hund hat ihn aufgenommen.</summary>
    PickedUp,
    /// <summary>Überlaufen: Der Hund ist daran vorbei.</summary>
    Missed
}

/// <summary>
/// Die Angabe der Hundeführer:in zu einem Gegenstand der gelegten Fährte in einem Ablauf. Anders
/// als Halte, Schenkel und Winkel kein Rechenergebnis, sondern eine Eingabe: Sie bleibt bei einer
/// Neuauswertung des Ablaufs stehen. Je Ablauf und Gegenstand höchstens eine Zeile; "keine
/// Angabe" heißt: keine Zeile. Nur bei der Fährte.
/// </summary>
public class GpsWalkRunArticle : Entity
{
    public Guid WalkRunId { get; set; }
    public GpsWalkRun? WalkRun { get; set; }

    /// <summary>Der Marker (<see cref="GpsPoint"/>) des Gegenstands an der gelegten Fährte.</summary>
    public Guid PointId { get; set; }

    public ArticleResult Result { get; set; }
}
