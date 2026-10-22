namespace Dogity.Application.Community;

/// <summary>
/// Die Uhr der Vereine: Termine und Anwesenheitstage zählen nach deutscher
/// Zeit, egal wo der Server läuft. Ein Termin um 00:30 Uhr am Samstag liegt in
/// UTC noch am Freitag - ohne diese Umrechnung hakte jemand den falschen Tag ab.
/// </summary>
public static class Vereinszeit
{
    public static readonly TimeZoneInfo Zone = FindeVereinszeit();

    private static TimeZoneInfo FindeVereinszeit()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"); }
        // Schlanke Container ohne Zeitzonen-Datenbank: lieber UTC als ein Absturz.
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }

    /// <summary>Der Kalendertag eines Zeitpunkts in der Vereinszeit.</summary>
    public static DateOnly Tag(DateTimeOffset zeitpunkt) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(zeitpunkt, Zone).DateTime);

    /// <summary>Wanduhr in der Vereinszeit (Datum und Uhrzeit, wie sie in Deutschland gerade zu lesen sind).</summary>
    public static DateTime Lokal(DateTimeOffset zeitpunkt) =>
        TimeZoneInfo.ConvertTime(zeitpunkt, Zone).DateTime;

    /// <summary>
    /// Mitternacht zu Beginn des Tages in Vereinszeit als Zeitpunkt. Zeitumstellungen
    /// fallen in Deutschland nachts um 2/3 Uhr, Mitternacht ist daher immer eindeutig;
    /// ein Tag kann aber 23 oder 25 Stunden haben - deshalb den Folgetag so berechnen
    /// und nicht "plus 24 Stunden".
    /// </summary>
    public static DateTimeOffset TagesBeginn(DateOnly tag) =>
        new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(tag.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), Zone), TimeSpan.Zero);

    /// <summary>Heute in der Vereinszeit.</summary>
    public static DateOnly Heute() => Tag(DateTimeOffset.UtcNow);
}
