using Dogity.Domain.Dogs;

namespace Dogity.Application.Dogs;

/// <summary>
/// Wie es um eine Frist steht. Numerisch serialisiert (die API hat keinen
/// JsonStringEnumConverter); das Frontend spiegelt die Werte in lib/gesundheit.ts.
/// </summary>
public enum HealthState
{
    Ok,
    DueSoon,
    Overdue
}

/// <summary>
/// Was "aktuell" und "fällig" bei den Gesundheitseinträgen heißt - als reine Funktionen,
/// ohne Datenbank, mit "heute" von außen (Vereinszeit). Dogity rechnet dabei keine
/// Impfintervalle aus: Es vergleicht nur das Datum, das die Person aus dem Impfpass
/// eingetragen hat, mit dem heutigen Tag.
/// </summary>
public static class Gesundheitsstand
{
    /// <summary>Ab so vielen Tagen vor Ablauf gilt ein Eintrag als "bald fällig" (gelb).</summary>
    public const int BaldTage = 30;

    /// <summary>
    /// Zustand einer Frist. Das Datum gilt einschließlich: Am Tag selbst ist der Schutz noch
    /// da ("läuft heute ab"), überfällig ist er ab dem Tag danach. Ohne Datum gibt es keine
    /// Frist, also nichts zu beanstanden.
    /// </summary>
    public static HealthState Zustand(DateOnly? faellig, DateOnly heute)
    {
        if (faellig is not { } tag) return HealthState.Ok;
        if (tag < heute) return HealthState.Overdue;
        return tag.DayNumber - heute.DayNumber <= BaldTage ? HealthState.DueSoon : HealthState.Ok;
    }

    /// <summary>
    /// Die aktuellen Einträge: je Art und Titel (Groß-/Kleinschreibung und Rand-Leerzeichen
    /// zählen nicht) der jüngste nach Datum, bei gleichem Datum der zuletzt angelegte. So
    /// beendet eine erneuerte Impfung die Frist der alten, ohne dass jemand die alte anfasst.
    /// Tierarzttermine gehören nicht dazu - sie haben keine Frist, die sich erneuern ließe.
    /// </summary>
    public static IReadOnlyList<DogHealthRecord> Aktuelle(IEnumerable<DogHealthRecord> eintraege) =>
        eintraege
            .Where(e => e.Kind != DogHealthKind.VetAppointment)
            .GroupBy(e => (e.DogId, e.Kind, Titel: Schluessel(e.Title)))
            .Select(g => g.OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id).First())
            .ToList();

    /// <summary>Der Titel als Vergleichsschlüssel für "derselbe Eintrag, erneuert".</summary>
    public static string Schluessel(string titel) => titel.Trim().ToUpperInvariant();

    /// <summary>
    /// Der Stand je Hund (die Einträge dürfen mehrere Hunde enthalten). Hunde, bei denen weder
    /// eine Frist noch ein künftiger Tierarzttermin eingetragen ist, fehlen im Ergebnis.
    /// </summary>
    public static Dictionary<Guid, DogHealthSummaryDto> Zusammenfassungen(IEnumerable<DogHealthRecord> eintraege, DateOnly heute)
    {
        var ergebnis = new Dictionary<Guid, DogHealthSummaryDto>();
        foreach (var hund in eintraege.GroupBy(e => e.DogId))
        {
            var mitFrist = Aktuelle(hund)
                .Where(e => e.DueDate is not null)
                .Select(e => (Eintrag: e, Zustand: Zustand(e.DueDate, heute)))
                .OrderBy(x => x.Eintrag.DueDate)
                .ThenBy(x => x.Eintrag.Title, StringComparer.Ordinal)
                .ToList();

            var termin = hund
                .Where(e => e.Kind == DogHealthKind.VetAppointment && e.Date >= heute)
                .OrderBy(e => e.Date).ThenBy(e => e.Time ?? TimeOnly.MaxValue).ThenBy(e => e.CreatedAt)
                .FirstOrDefault();

            if (mitFrist.Count == 0 && termin is null) continue;

            DogHealthDueDto? naechste = null;
            if (mitFrist.Count > 0)
            {
                var (eintrag, zustand) = mitFrist[0];
                naechste = new DogHealthDueDto(eintrag.Id, eintrag.Kind, eintrag.Title, eintrag.DueDate!.Value, zustand);
            }
            ergebnis[hund.Key] = new DogHealthSummaryDto(
                mitFrist.Count(x => x.Zustand == HealthState.Overdue),
                mitFrist.Count(x => x.Zustand == HealthState.DueSoon),
                naechste,
                termin is null ? null : new DogHealthAppointmentDto(termin.Id, termin.Title, termin.Date, termin.Time));
        }
        return ergebnis;
    }
}
