using Dogity.Application.Community;
using Dogity.Application.Reminders;

namespace Dogity.Application.Training;

/// <summary>Woher das erkannte nächste Training stammt - in dieser Reihenfolge wird gesucht.</summary>
public enum TrainingsQuelle
{
    Gruppe,
    Rhythmus,
    Tagebuch,
    Unbekannt
}

/// <summary>Ein Trainingstag aus dem Tagebuch: Datum und - falls eingetragen - die Startzeit (Vereinszeit).</summary>
public readonly record struct TagebuchTraining(DateOnly Datum, TimeOnly? StartTime);

/// <summary>Der selbst angegebene Rhythmus eines Hundes (<see cref="Domain.Dogs.DogTrainingRhythm"/>): Tage als Bitmaske, Stunde wahlweise.</summary>
public readonly record struct GespeicherterRhythmus(int Tage, int? Stunde);

/// <summary>Ein (erwartetes) Training: Tag und, wenn bekannt, die Uhrzeit - beides Vereinszeit.</summary>
public readonly record struct TrainingsTermin(DateOnly Tag, TimeOnly? Uhrzeit, TrainingsQuelle Quelle)
{
    public DateTime? Beginn => Uhrzeit is { } uhrzeit ? Tag.ToDateTime(uhrzeit) : null;
}

/// <summary>
/// Wann ein Hund das nächste Mal trainiert - als reine Funktionen, ohne Datenbank, damit
/// Mitternacht, Zeitumstellung und die Reihenfolge der Quellen sich prüfen lassen. Gesucht wird in
/// dieser Reihenfolge, die erste Quelle mit einem passenden Termin gewinnt:
/// <list type="number">
/// <item>ein kommender Gruppentermin innerhalb von <see cref="GruppenVorlaufTage"/> Tagen,</item>
/// <item>der selbst angegebene Rhythmus des Hundes,</item>
/// <item>ein Muster aus dem Tagebuch: Wochentage mit mindestens <see cref="MusterMindestTage"/> Trainingstagen
/// in den letzten <see cref="TagebuchWochen"/> Wochen, mit dem Median der Startzeiten.</item>
/// </list>
/// Findet sich nichts, ist das nächste Training unbekannt (null).
/// </summary>
public static class Trainingsrhythmus
{
    public const int GruppenVorlaufTage = 14;
    public const int TagebuchWochen = 8;
    public const int MusterMindestTage = 2;

    /// <summary>So lange vor dem Training kommt die Erinnerung.</summary>
    public static readonly TimeSpan ErinnerungsVorlauf = TimeSpan.FromHours(2);

    /// <summary>Wann die Erinnerung für ein Training ohne bekannte Uhrzeit kommt.</summary>
    public const int OhneUhrzeitStunde = 9;

    /// <summary>
    /// Das nächste Training, auf das sich ein Merkzettel-Eintrag von jetzt an beziehen kann: Heute zählt,
    /// solange die Erinnerung dafür noch aussteht (also die Uhrzeit mindestens zwei Stunden entfernt ist, bzw.
    /// es vor 09:00 ist, wenn die Uhrzeit fehlt) - wer um 17:30 etwas für ein Training um 18:00 merkt,
    /// kann nicht mehr rechtzeitig erinnert werden und meint das nächste.
    /// </summary>
    public static TrainingsTermin? NaechstesTraining(
        DateTimeOffset jetzt, IEnumerable<TagebuchTraining> tagebuch, IEnumerable<DateTimeOffset> gruppentermine,
        GespeicherterRhythmus? rhythmus = null)
    {
        var lokal = Vereinszeit.Lokal(jetzt);
        return Waehle(DateOnly.FromDateTime(lokal), DateOnly.FromDateTime(lokal), tagebuch, gruppentermine, rhythmus,
            t => ErinnerungsZeitpunkt(t) >= lokal);
    }

    /// <summary>
    /// Das Training, das heute oder später noch bevorsteht - für den Erinnerungsdienst, der prüft, ob es Zeit
    /// für die Erinnerung ist. Anders als <see cref="NaechstesTraining"/> zählt hier auch ein Training in
    /// weniger als zwei Stunden: Der Tick kommt nie auf die Minute genau, und die Erinnerung soll
    /// trotzdem noch kommen, solange das Training nicht begonnen hat.
    /// </summary>
    public static TrainingsTermin? BevorstehendesTraining(
        DateTimeOffset jetzt, IEnumerable<TagebuchTraining> tagebuch, IEnumerable<DateTimeOffset> gruppentermine,
        GespeicherterRhythmus? rhythmus = null)
    {
        var lokal = Vereinszeit.Lokal(jetzt);
        var heute = DateOnly.FromDateTime(lokal);
        return Waehle(heute, heute, tagebuch, gruppentermine, rhythmus,
            t => t.Beginn is { } beginn ? beginn >= lokal : t.Tag >= heute);
    }

    /// <summary>
    /// Das erste erkannte Training an oder nach <paramref name="ab"/> (etwa dem Montag der Folgewoche), wahlweise
    /// nur bis einschließlich <paramref name="bis"/>.
    /// </summary>
    public static TrainingsTermin? ErstesTrainingAb(
        DateOnly ab, DateOnly heute, IEnumerable<TagebuchTraining> tagebuch, IEnumerable<DateTimeOffset> gruppentermine,
        GespeicherterRhythmus? rhythmus = null, DateOnly? bis = null) =>
        Waehle(heute, ab, tagebuch, gruppentermine, rhythmus, t => bis is not { } ende || t.Tag <= ende);

    /// <summary>
    /// Der Tag (und die Uhrzeit, wenn bekannt), auf den ein Merkzettel-Eintrag zielt:
    /// <list type="bullet">
    /// <item>Datum: der gewählte Tag, mit der Uhrzeit des erkannten Trainings an diesem Tag.</item>
    /// <item>Nächste Woche: das erste erkannte Training in der Woche ab dem Montag <paramref name="zielDatum"/> (sonst
    /// dieser Montag ohne Uhrzeit; fehlt <paramref name="zielDatum"/>, der Montag nach dem Anlegen).</item>
    /// <item>Nächstes Training: das nächste erkannte, das noch nicht begonnen hat (<see cref="BevorstehendesTraining"/>).
    /// Weil es jedes Mal neu bestimmt wird, rutscht der Eintrag nach dem Trainingstag von selbst auf das nächste.
    /// Wurde er erst nach dem Zeitpunkt der Erinnerung für dieses Training gemerkt (um 17:30 für 18:00), meint er das
    /// folgende: Die Maske hat ihm "nächste Woche" gezeigt (<see cref="NaechstesTraining"/>), und eine Erinnerung
    /// zehn Minuten vor dem Training käme zu spät. Ist nichts erkannt, verhält er sich für die Erinnerung
    /// (<paramref name="erinnerung"/>) wie "Nächste Woche"; zum Anzeigen ist es dann unbekannt (null).</item>
    /// </list>
    /// </summary>
    public static TrainingsTermin? ZielDesEintrags(
        Domain.Training.TrainingNoteGoal ziel, DateOnly? zielDatum, DateTimeOffset angelegt, DateTimeOffset jetzt,
        IReadOnlyList<TagebuchTraining> tagebuch, IReadOnlyList<DateTimeOffset> gruppentermine, GespeicherterRhythmus? rhythmus,
        bool erinnerung)
    {
        var heute = Vereinszeit.Tag(jetzt);
        switch (ziel)
        {
            case Domain.Training.TrainingNoteGoal.Date:
                if (zielDatum is not { } tag) return null;
                return ErstesTrainingAb(tag, heute, tagebuch, gruppentermine, rhythmus) is { } amTag && amTag.Tag == tag
                    ? amTag
                    : new TrainingsTermin(tag, null, TrainingsQuelle.Unbekannt);

            case Domain.Training.TrainingNoteGoal.NextWeek:
                return AbMontag(zielDatum ?? MontagDerFolgewoche(Vereinszeit.Tag(angelegt)), heute, tagebuch, gruppentermine, rhythmus);

            default:
                var naechstes = BevorstehendesTraining(jetzt, tagebuch, gruppentermine, rhythmus);
                if (naechstes is { } termin && Vereinszeit.Lokal(angelegt) > ErinnerungsZeitpunkt(termin))
                    naechstes = ErstesTrainingAb(termin.Tag.AddDays(1), heute, tagebuch, gruppentermine, rhythmus);
                if (naechstes is not null || !erinnerung) return naechstes;
                return AbMontag(MontagDerFolgewoche(Vereinszeit.Tag(angelegt)), heute, tagebuch, gruppentermine, rhythmus);
        }
    }

    private static TrainingsTermin AbMontag(
        DateOnly montag, DateOnly heute, IReadOnlyList<TagebuchTraining> tagebuch, IReadOnlyList<DateTimeOffset> gruppentermine,
        GespeicherterRhythmus? rhythmus) =>
        // Nur Montag bis Sonntag: Ein Gruppentermin 14 Tage später wäre nicht mehr "nächste Woche".
        ErstesTrainingAb(montag, heute, tagebuch, gruppentermine, rhythmus, montag.AddDays(6))
        ?? new TrainingsTermin(montag, null, TrainingsQuelle.Unbekannt);

    /// <summary>Der Montag der Woche nach der von <paramref name="tag"/>.</summary>
    public static DateOnly MontagDerFolgewoche(DateOnly tag)
    {
        var bis = ((int)DayOfWeek.Monday - (int)tag.DayOfWeek + 7) % 7;
        return tag.AddDays(bis == 0 ? 7 : bis);
    }

    /// <summary>
    /// Wann die Erinnerung für das Training kommt: zwei Stunden vorher, nicht vor 08:00 Uhr am Trainingstag
    /// (Ruhezeit der Erinnerungen); ohne Uhrzeit um 09:00 Uhr.
    /// </summary>
    public static DateTime ErinnerungsZeitpunkt(TrainingsTermin termin)
    {
        var fruehestens = termin.Tag.ToDateTime(new TimeOnly(Erinnerungsregeln.ErsteStunde, 0));
        if (termin.Beginn is not { } beginn) return termin.Tag.ToDateTime(new TimeOnly(OhneUhrzeitStunde, 0));

        var zeitpunkt = beginn - ErinnerungsVorlauf;
        return zeitpunkt < fruehestens ? fruehestens : zeitpunkt;
    }

    /// <summary>
    /// <paramref name="heute"/> bestimmt das Fenster für das Tagebuch, <paramref name="von"/> den ersten Tag, ab dem
    /// Termine in Frage kommen; <paramref name="zaehlt"/> schließt vergangene oder zu knappe Termine aus.
    /// </summary>
    private static TrainingsTermin? Waehle(
        DateOnly heute, DateOnly von, IEnumerable<TagebuchTraining> tagebuch, IEnumerable<DateTimeOffset> gruppentermine,
        GespeicherterRhythmus? rhythmus, Func<TrainingsTermin, bool> zaehlt)
    {
        var gruppe = gruppentermine
            .Select(Vereinszeit.Lokal)
            .Select(l => new TrainingsTermin(DateOnly.FromDateTime(l), TimeOnly.FromDateTime(l), TrainingsQuelle.Gruppe))
            .Where(t => t.Tag >= von && t.Tag <= von.AddDays(GruppenVorlaufTage) && zaehlt(t));
        if (Erster(gruppe) is { } gruppenTermin) return gruppenTermin;

        if (rhythmus is { } vorgabe && Erinnerungsregeln.GueltigeTage(vorgabe.Tage))
        {
            TimeOnly? uhrzeit = vorgabe.Stunde is { } stunde && stunde is >= 0 and <= 23 ? new TimeOnly(stunde, 0) : null;
            var gewaehlt = Wochentage(von, vorgabe.Tage, _ => uhrzeit, TrainingsQuelle.Rhythmus).Where(zaehlt);
            if (Erster(gewaehlt) is { } gewaehlterTermin) return gewaehlterTermin;
        }

        var muster = TagebuchMuster(heute, tagebuch);
        var ausTagebuch = Wochentage(von, muster.Keys.Aggregate(0, (bits, tag) => bits | Erinnerungsregeln.TagesBit(tag)),
            tag => muster[tag.DayOfWeek], TrainingsQuelle.Tagebuch).Where(zaehlt);
        return Erster(ausTagebuch);
    }

    private static TrainingsTermin? Erster(IEnumerable<TrainingsTermin> termine)
    {
        var liste = termine.OrderBy(t => t.Tag).ThenBy(t => t.Uhrzeit ?? TimeOnly.MinValue).ToList();
        return liste.Count == 0 ? null : liste[0];
    }

    /// <summary>
    /// Die Tage der Maske ab <paramref name="von"/>, acht Tage lang: So kommt jeder Wochentag vor, auch ein
    /// heute schon verpasster noch eine Woche später.
    /// </summary>
    private static IEnumerable<TrainingsTermin> Wochentage(
        DateOnly von, int maske, Func<DateOnly, TimeOnly?> uhrzeit, TrainingsQuelle quelle)
    {
        for (var i = 0; i <= 7; i++)
        {
            var tag = von.AddDays(i);
            if ((maske & Erinnerungsregeln.TagesBit(tag.DayOfWeek)) != 0)
                yield return new TrainingsTermin(tag, uhrzeit(tag), quelle);
        }
    }

    /// <summary>Wochentage mit mindestens zwei Trainingstagen in den letzten acht Wochen, je mit dem Median der Startzeiten.</summary>
    private static Dictionary<DayOfWeek, TimeOnly?> TagebuchMuster(DateOnly heute, IEnumerable<TagebuchTraining> tagebuch)
    {
        var seit = heute.AddDays(-7 * TagebuchWochen);
        return tagebuch
            .Where(t => t.Datum >= seit && t.Datum <= heute)
            // Mehrere Einträge am selben Tag sind ein Trainingstag.
            .GroupBy(t => t.Datum)
            .GroupBy(tag => tag.Key.DayOfWeek)
            .Where(woche => woche.Count() >= MusterMindestTage)
            .ToDictionary(
                woche => woche.Key,
                // Je Tag die früheste eingetragene Startzeit.
                woche => Median(woche
                    .Select(tag => tag.Select(t => t.StartTime).Where(zeit => zeit is not null).Min())
                    .Where(zeit => zeit is not null)
                    .Select(zeit => zeit!.Value)
                    .ToList()));
    }

    /// <summary>Mitte der Zeiten; bei gerader Anzahl der Mittelwert der beiden mittleren, auf die Minute abgeschnitten. Leer ohne Zeiten.</summary>
    private static TimeOnly? Median(List<TimeOnly> zeiten)
    {
        if (zeiten.Count == 0) return null;
        zeiten.Sort();

        var mitte = zeiten.Count / 2;
        if (zeiten.Count % 2 == 1) return zeiten[mitte];

        var minuten = (zeiten[mitte - 1].ToTimeSpan().TotalMinutes + zeiten[mitte].ToTimeSpan().TotalMinutes) / 2;
        return TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Math.Floor(minuten)));
    }
}
