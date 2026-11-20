using Dogity.Application.Abstractions;
using Dogity.Application.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Common;

/// <summary>
/// Die Uhr einer Person: "heute", Wochen, Erinnerungs- und Ruhezeiten zählen in der Zeitzone
/// ihres Geräts (<see cref="Dogity.Domain.Preferences.UserPreference.TimeZone"/>). Ohne gespeicherte
/// Zone - alle Konten aus Deutschland, Österreich und der Schweiz, Gastkonten - und bei einer Zone,
/// die dieser Server nicht kennt, gilt die <see cref="Vereinszeit"/>. Was einem Verein gehört
/// (Termine, Prüfungen, nächtliche Jobs), bleibt dort; die Regel steht in CODING_GUIDELINES.md.
/// </summary>
public static class Ortszeit
{
    /// <summary>Höchstlänge der IANA-Kennung (die längste echte hat 32 Zeichen).</summary>
    public const int ZoneMaxLaenge = 64;

    /// <summary>Die Zone zur Kennung; null, unbekannt oder ohne Zeitzonen-Datenbank = Vereinszeit.</summary>
    public static TimeZoneInfo Zone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return Vereinszeit.Zone;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { return Vereinszeit.Zone; }
    }

    /// <summary>Ob der Server die Kennung als Zeitzone kennt (für die Prüfung beim Speichern).</summary>
    public static bool Bekannt(string id)
    {
        try { TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { return false; }
    }

    public static DateOnly Tag(DateTimeOffset zeitpunkt, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(zeitpunkt, zone).DateTime);

    /// <summary>Wanduhr in der Zone (Datum und Uhrzeit, wie die Person sie gerade liest).</summary>
    public static DateTime Lokal(DateTimeOffset zeitpunkt, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(zeitpunkt, zone).DateTime;

    /// <summary>Mitternacht zu Beginn des Tages in der Zone als Zeitpunkt (Tage mit 23/25 Stunden: nicht "plus 24 h").</summary>
    public static DateTimeOffset TagesBeginn(DateOnly tag, TimeZoneInfo zone)
    {
        var mitternacht = DateTime.SpecifyKind(tag.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        // In manchen Zonen beginnt die Sommerzeit um 0 Uhr: Mitternacht gibt es dann nicht,
        // der Tag beginnt mit dem ersten Zeitpunkt danach.
        if (zone.IsInvalidTime(mitternacht)) mitternacht = mitternacht.AddHours(1);
        return new(TimeZoneInfo.ConvertTimeToUtc(mitternacht, zone), TimeSpan.Zero);
    }

    /// <summary>
    /// Der früheste und der späteste Kalendertag, der gerade irgendwo auf der Erde gilt (UTC-12 bis UTC+14):
    /// Hintergrunddienste grenzen damit ihre Abfragen ein und entscheiden je Person genauer.
    /// </summary>
    public static (DateOnly Frueh, DateOnly Spaet) TagSpanne(DateTimeOffset zeitpunkt) =>
        (DateOnly.FromDateTime(zeitpunkt.UtcDateTime.AddHours(-12)), DateOnly.FromDateTime(zeitpunkt.UtcDateTime.AddHours(14)));

    /// <summary>Die Zone einer Person (je Aufruf eine kleine Abfrage); ohne Einstellung die Vereinszeit.</summary>
    public static async Task<TimeZoneInfo> ZoneAsync(this IApplicationDbContext db, Guid userId, CancellationToken ct = default) =>
        Zone(await db.UserPreferences.AsNoTracking().Where(p => p.UserId == userId).Select(p => p.TimeZone).FirstOrDefaultAsync(ct));

    /// <summary>Die Zonen mehrerer Personen in einer Abfrage (Hintergrunddienste); wer fehlt, hat die Vereinszeit.</summary>
    public static async Task<Func<Guid, TimeZoneInfo>> ZonenAsync(this IApplicationDbContext db, IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        var ids = userIds.Distinct().ToList();
        var zeilen = await db.UserPreferences.AsNoTracking()
            .Where(p => ids.Contains(p.UserId) && p.TimeZone != null)
            .Select(p => new { p.UserId, p.TimeZone })
            .ToListAsync(ct);
        var zonen = zeilen.ToDictionary(z => z.UserId, z => Zone(z.TimeZone));
        return userId => zonen.GetValueOrDefault(userId, Vereinszeit.Zone);
    }

    /// <summary>Heute in der Zone der Person.</summary>
    public static async Task<DateOnly> HeuteAsync(this IApplicationDbContext db, Guid userId, TimeProvider zeit, CancellationToken ct = default) =>
        Tag(zeit.GetUtcNow(), await db.ZoneAsync(userId, ct));
}
