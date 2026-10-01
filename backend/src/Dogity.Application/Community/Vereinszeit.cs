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

    /// <summary>Heute in der Vereinszeit.</summary>
    public static DateOnly Heute() => Tag(DateTimeOffset.UtcNow);
}
