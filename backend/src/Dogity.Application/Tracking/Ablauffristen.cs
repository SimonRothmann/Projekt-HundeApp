using Dogity.Domain.Tracking;

namespace Dogity.Application.Tracking;

/// <summary>
/// Wie lange eine gelegte Spur noch gesucht werden kann - die EINE Stelle im
/// Backend für die Fristen und für das Ende des Legens. Das Frontend führt
/// dieselben Zahlen (ABLAUF_FRIST_STUNDEN und ABLAUF_FRIST_STUNDEN_TRAIL in
/// lib/faehrte.ts, dort auch kannNochAblaufen); beim Ändern beide Stellen anfassen.
/// </summary>
public static class Ablauffristen
{
    /// <summary>
    /// Eine Fährte: bis 24 Stunden nach dem Ende des Legens, unabhängig vom
    /// Kalendertag - abends gelegt und morgens gesucht ist der Normalfall.
    /// </summary>
    public static readonly TimeSpan Faehrte = TimeSpan.FromHours(24);

    /// <summary>
    /// Ein Trail: Er liegt bis zu 24 Stunden, danach folgt die Suche - mit nur
    /// 24 Stunden wäre er beim Start schon nicht mehr zu suchen.
    /// </summary>
    public static readonly TimeSpan Trail = TimeSpan.FromHours(48);

    public static TimeSpan Fuer(GpsTrackKind art) => art == GpsTrackKind.Mantrailing ? Trail : Faehrte;

    /// <summary>
    /// Ende des Legens: der Zeitstempel des letzten AUTOMATISCHEN Punkts.
    /// Manuelle Marker zählen nicht, sie können einen späteren Zeitstempel
    /// tragen. Null, wenn es keinen automatischen Punkt gibt.
    /// </summary>
    public static DateTimeOffset? LegeEnde(IEnumerable<GpsPointDto> punkte) =>
        punkte.Where(p => p.PointType != GpsPointType.Manual).Select(p => (DateTimeOffset?)p.Timestamp).Max();

    /// <summary>
    /// Ob eine Spur, deren Legen um <paramref name="legeEnde"/> endete, zum
    /// Zeitpunkt <paramref name="zeitpunkt"/> noch gesucht werden kann: höchstens
    /// die Frist danach. Ohne Zeitstempel lässt sich die Frist nicht prüfen: nein.
    /// </summary>
    public static bool NochZuSuchen(DateTimeOffset? legeEnde, GpsTrackKind art, DateTimeOffset zeitpunkt) =>
        legeEnde is { } ende && zeitpunkt - ende <= Fuer(art);
}
