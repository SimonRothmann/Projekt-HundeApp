using Dogity.Application.Training;
using Dogity.Domain.Training;

namespace Dogity.Application.Tests.Training;

/// <summary>
/// Das nächste Training eines Hundes als reine Funktion: Gruppentermin vor gespeichertem Rhythmus vor dem Muster aus
/// dem Tagebuch, Median der Uhrzeit, "heute zählt, wenn die Erinnerung noch aussteht". Die Uhrzeiten im Test sind
/// UTC; im November gilt in Deutschland UTC+1.
/// </summary>
public class TrainingsrhythmusTests
{
    private static DateTimeOffset Utc(int tag, int stunde, int minute = 0, int monat = 11) =>
        new(2026, monat, tag, stunde, minute, 0, TimeSpan.Zero);

    // Mittwoch, 4.11.2026, 10:00 Uhr deutscher Zeit.
    private static readonly DateTimeOffset Jetzt = Utc(4, 9);

    private static readonly TagebuchTraining[] Keine = [];
    private static readonly DateTimeOffset[] KeineTermine = [];

    private static TagebuchTraining Tag(int tag, int? stunde, int minute = 0, int monat = 10) =>
        new(new DateOnly(2026, monat, tag), stunde is { } s ? new TimeOnly(s, minute) : null);

    /// <summary>Dienstage 20.10. und 27.10. um 18:00 (Wochentag Dienstag, 2 Trainingstage).</summary>
    private static readonly TagebuchTraining[] ZweiDienstage = [Tag(20, 18), Tag(27, 18)];

    // ---------------------------------------------------------------- Reihenfolge der Quellen

    [Fact]
    public void Gruppentermin_GehtVorTagebuchUndRhythmus()
    {
        // Freitag, 6.11., 18:00 Uhr deutscher Zeit.
        var termin = Trainingsrhythmus.NaechstesTraining(Jetzt, ZweiDienstage, [Utc(6, 17)], new GespeicherterRhythmus(32, 10));

        Assert.Equal(new TrainingsTermin(new DateOnly(2026, 11, 6), new TimeOnly(18, 0), TrainingsQuelle.Gruppe), termin);
    }

    [Fact]
    public void Gruppentermin_ErsterVonVielen()
    {
        var termin = Trainingsrhythmus.NaechstesTraining(Jetzt, Keine, [Utc(13, 17), Utc(6, 17), Utc(20, 17)]);

        Assert.Equal(new DateOnly(2026, 11, 6), termin!.Value.Tag);
    }

    [Fact]
    public void Gruppentermin_WeiterAlsVierzehnTage_ZaehltNicht()
    {
        // 19.11. liegt 15 Tage nach dem 4.11.
        var termin = Trainingsrhythmus.NaechstesTraining(Jetzt, ZweiDienstage, [Utc(19, 17)]);

        Assert.Equal(TrainingsQuelle.Tagebuch, termin!.Value.Quelle);
    }

    [Fact]
    public void Gruppentermin_AmGrenztag_VierzehnTage_ZaehltNoch()
    {
        var termin = Trainingsrhythmus.NaechstesTraining(Jetzt, Keine, [Utc(18, 17)]);

        Assert.Equal(TrainingsQuelle.Gruppe, termin!.Value.Quelle);
    }

    [Fact]
    public void GespeicherterRhythmus_GehtVorDemTagebuch()
    {
        // Samstag (32), 10 Uhr - das Tagebuch sagt Dienstag.
        var termin = Trainingsrhythmus.NaechstesTraining(Jetzt, ZweiDienstage, KeineTermine, new GespeicherterRhythmus(32, 10));

        Assert.Equal(new TrainingsTermin(new DateOnly(2026, 11, 7), new TimeOnly(10, 0), TrainingsQuelle.Rhythmus), termin);
    }

    [Fact]
    public void GespeicherterRhythmus_OhneStunde_HatKeineUhrzeit()
    {
        var termin = Trainingsrhythmus.NaechstesTraining(Jetzt, Keine, KeineTermine, new GespeicherterRhythmus(32, null));

        Assert.Equal(new TrainingsTermin(new DateOnly(2026, 11, 7), null, TrainingsQuelle.Rhythmus), termin);
    }

    [Fact]
    public void GespeicherterRhythmus_MehrereTage_NimmtDenNaechsten()
    {
        // Montag (1) und Freitag (16) - ab Mittwoch ist der Freitag der nächste.
        var termin = Trainingsrhythmus.NaechstesTraining(Jetzt, Keine, KeineTermine, new GespeicherterRhythmus(1 | 16, 9));

        Assert.Equal(new DateOnly(2026, 11, 6), termin!.Value.Tag);
    }

    [Fact]
    public void UngueltigerRhythmus_WirdUebergangen_DasTagebuchEntscheidet()
    {
        var termin = Trainingsrhythmus.NaechstesTraining(Jetzt, ZweiDienstage, KeineTermine, new GespeicherterRhythmus(0, 10));

        Assert.Equal(TrainingsQuelle.Tagebuch, termin!.Value.Quelle);
    }

    // ---------------------------------------------------------------- Muster aus dem Tagebuch

    [Fact]
    public void Tagebuch_WochentagMitZweiTrainings_IstDasMuster()
    {
        var termin = Trainingsrhythmus.NaechstesTraining(Jetzt, ZweiDienstage, KeineTermine);

        Assert.Equal(new TrainingsTermin(new DateOnly(2026, 11, 10), new TimeOnly(18, 0), TrainingsQuelle.Tagebuch), termin);
    }

    [Fact]
    public void Tagebuch_UhrzeitIstDerMedian_UngeradeAnzahl()
    {
        var tagebuch = new[] { Tag(13, 17), Tag(20, 18), Tag(27, 19, 30) };

        var termin = Trainingsrhythmus.NaechstesTraining(Jetzt, tagebuch, KeineTermine);

        Assert.Equal(new TimeOnly(18, 0), termin!.Value.Uhrzeit);
    }

    [Fact]
    public void Tagebuch_UhrzeitIstDerMedian_GeradeAnzahl_Mittelwert()
    {
        var tagebuch = new[] { Tag(20, 17), Tag(27, 18, 30) };

        var termin = Trainingsrhythmus.NaechstesTraining(Jetzt, tagebuch, KeineTermine);

        Assert.Equal(new TimeOnly(17, 45), termin!.Value.Uhrzeit);
    }

    [Fact]
    public void Tagebuch_OhneStartzeiten_MusterOhneUhrzeit()
    {
        var termin = Trainingsrhythmus.NaechstesTraining(Jetzt, [Tag(20, null), Tag(27, null)], KeineTermine);

        Assert.Equal(new TrainingsTermin(new DateOnly(2026, 11, 10), null, TrainingsQuelle.Tagebuch), termin);
    }

    [Fact]
    public void Tagebuch_EinTrainingJeWochentag_KeinMuster_Unbekannt()
    {
        var termin = Trainingsrhythmus.NaechstesTraining(Jetzt, [Tag(20, 18), Tag(22, 18), Tag(24, 18)], KeineTermine);

        Assert.Null(termin);
    }

    [Fact]
    public void Tagebuch_Leer_Unbekannt() =>
        Assert.Null(Trainingsrhythmus.NaechstesTraining(Jetzt, Keine, KeineTermine));

    [Fact]
    public void Tagebuch_ZweiEintraegeAmSelbenTag_SindEinTrainingstag()
    {
        var termin = Trainingsrhythmus.NaechstesTraining(Jetzt, [Tag(20, 18), Tag(20, 19)], KeineTermine);

        Assert.Null(termin);
    }

    [Fact]
    public void Tagebuch_AelterAlsAchtWochen_ZaehltNicht()
    {
        // 8 Wochen vor dem 4.11. ist der 9.9.; 1.9. und 8.9. liegen davor.
        var termin = Trainingsrhythmus.NaechstesTraining(Jetzt, [Tag(1, 18, monat: 9), Tag(8, 18, monat: 9)], KeineTermine);

        Assert.Null(termin);
    }

    [Fact]
    public void Tagebuch_ZweiWochentage_NimmtDenNaechsten()
    {
        // Dienstag und Freitag je zweimal; ab Mittwoch kommt der Freitag zuerst.
        var tagebuch = new[] { Tag(20, 18), Tag(27, 18), Tag(23, 10), Tag(30, 10) };

        var termin = Trainingsrhythmus.NaechstesTraining(Jetzt, tagebuch, KeineTermine);

        Assert.Equal(new TrainingsTermin(new DateOnly(2026, 11, 6), new TimeOnly(10, 0), TrainingsQuelle.Tagebuch), termin);
    }

    // ---------------------------------------------------------------- Heute zählt, solange die Erinnerung aussteht

    private static readonly TagebuchTraining[] ZweiMittwoche = [Tag(21, 18), Tag(28, 18)];

    [Theory]
    [InlineData(10, 0, 4)]   // 11:00 Uhr deutscher Zeit: 7 Stunden bis 18:00 -> heute
    [InlineData(15, 0, 4)]   // genau zwei Stunden vorher -> heute
    [InlineData(15, 1, 11)]  // zwei Stunden minus eine Minute -> die Erinnerung stünde schon aus, nächste Woche
    [InlineData(17, 30, 11)]
    public void HeuteZaehlt_WennDieErinnerungNochAussteht(int stunde, int minute, int erwarteterTag)
    {
        var termin = Trainingsrhythmus.NaechstesTraining(Utc(4, stunde, minute), ZweiMittwoche, KeineTermine);

        Assert.Equal(new DateOnly(2026, 11, erwarteterTag), termin!.Value.Tag);
    }

    [Theory]
    [InlineData(7, 59, 4)]   // 08:59 deutscher Zeit, Erinnerung um 09:00 steht aus
    [InlineData(8, 0, 4)]    // genau 09:00 zählt noch, wie "genau zwei Stunden vorher"
    [InlineData(8, 1, 11)]   // 09:01 - die Erinnerung ist durch, nächste Woche
    public void HeuteOhneUhrzeit_ZaehltBisNeunUhr(int stunde, int minute, int erwarteterTag)
    {
        var termin = Trainingsrhythmus.NaechstesTraining(Utc(4, stunde, minute), Keine, KeineTermine, new GespeicherterRhythmus(4, null));

        Assert.Equal(new DateOnly(2026, 11, erwarteterTag), termin!.Value.Tag);
    }

    [Fact]
    public void HeuteMorgensFrueh_TrainingNochVorDerErinnerungsgrenze_ZaehltDurchDieAchtUhrGrenze()
    {
        // Training 09:30, Erinnerung frühestens 08:00: Um 07:30 steht sie noch aus.
        var termin = Trainingsrhythmus.NaechstesTraining(Utc(4, 6, 30), Keine, KeineTermine, new GespeicherterRhythmus(4, 9));

        Assert.Equal(new DateOnly(2026, 11, 4), termin!.Value.Tag);
    }

    [Fact]
    public void Bevorstehend_ZaehltAuchEinTrainingInWenigerAlsZweiStunden()
    {
        // 16:30 Uhr: 90 Minuten bis 18:00 - NaechstesTraining meint schon die nächste Woche, die Erinnerung läuft noch.
        var jetzt = Utc(4, 15, 30);

        Assert.Equal(new DateOnly(2026, 11, 11), Trainingsrhythmus.NaechstesTraining(jetzt, ZweiMittwoche, KeineTermine)!.Value.Tag);
        Assert.Equal(new DateOnly(2026, 11, 4), Trainingsrhythmus.BevorstehendesTraining(jetzt, ZweiMittwoche, KeineTermine)!.Value.Tag);
    }

    [Fact]
    public void Bevorstehend_NachDemBeginn_IstEsDasNaechste()
    {
        var termin = Trainingsrhythmus.BevorstehendesTraining(Utc(4, 17, 5), ZweiMittwoche, KeineTermine);

        Assert.Equal(new DateOnly(2026, 11, 11), termin!.Value.Tag);
    }

    [Fact]
    public void Gruppentermin_HeuteInWenigerAlsZweiStunden_ZaehltFuersNaechsteNichtMehr()
    {
        // Heute 18:00 (17:00 UTC), jetzt 17:30 Uhr deutscher Zeit; der nächste Gruppentermin ist am 11.11.
        var termin = Trainingsrhythmus.NaechstesTraining(Utc(4, 16, 30), Keine, [Utc(4, 17), Utc(11, 17)]);

        Assert.Equal(new DateOnly(2026, 11, 11), termin!.Value.Tag);
    }

    [Fact]
    public void Tagesgrenze_ZaehltInVereinszeit()
    {
        // 23:30 UTC am 4.11. ist 00:30 Uhr am 5.11. in Deutschland: Das Training liegt am 5.
        var termin = Trainingsrhythmus.NaechstesTraining(Utc(4, 12), Keine, [Utc(4, 23, 30)]);

        Assert.Equal(new TrainingsTermin(new DateOnly(2026, 11, 5), new TimeOnly(0, 30), TrainingsQuelle.Gruppe), termin);
    }

    // ---------------------------------------------------------------- Zeitpunkt der Erinnerung, Folgewoche

    [Theory]
    [InlineData(18, 0, 16, 0)]
    [InlineData(10, 0, 8, 0)]   // genau 08:00
    [InlineData(9, 30, 8, 0)]   // 07:30 läge in der Ruhezeit
    [InlineData(6, 0, 8, 0)]
    public void Erinnerungszeitpunkt_ZweiStundenVorher_NieVorAchtUhr(int stunde, int minute, int erwartetStunde, int erwartetMinute)
    {
        var termin = new TrainingsTermin(new DateOnly(2026, 11, 4), new TimeOnly(stunde, minute), TrainingsQuelle.Rhythmus);

        Assert.Equal(new DateTime(2026, 11, 4, erwartetStunde, erwartetMinute, 0), Trainingsrhythmus.ErinnerungsZeitpunkt(termin));
    }

    [Fact]
    public void Erinnerungszeitpunkt_OhneUhrzeit_NeunUhr()
    {
        var termin = new TrainingsTermin(new DateOnly(2026, 11, 4), null, TrainingsQuelle.Rhythmus);

        Assert.Equal(new DateTime(2026, 11, 4, 9, 0, 0), Trainingsrhythmus.ErinnerungsZeitpunkt(termin));
    }

    [Theory]
    [InlineData(2026, 11, 4, 2026, 11, 9)]   // Mittwoch
    [InlineData(2026, 11, 9, 2026, 11, 16)]  // Montag: die nächste, nicht heute
    [InlineData(2026, 11, 8, 2026, 11, 9)]   // Sonntag
    public void MontagDerFolgewoche(int j, int m, int t, int ej, int em, int et) =>
        Assert.Equal(new DateOnly(ej, em, et), Trainingsrhythmus.MontagDerFolgewoche(new DateOnly(j, m, t)));

    // ---------------------------------------------------------------- Ziel eines Eintrags

    private static TrainingsTermin? Ziel(
        TrainingNoteGoal ziel, DateOnly? datum, DateTimeOffset angelegt, DateTimeOffset jetzt, bool erinnerung,
        IReadOnlyList<TagebuchTraining>? tagebuch = null, GespeicherterRhythmus? rhythmus = null) =>
        Trainingsrhythmus.ZielDesEintrags(ziel, datum, angelegt, jetzt, tagebuch ?? ZweiMittwoche, KeineTermine, rhythmus, erinnerung);

    [Fact]
    public void Ziel_NaechsteWoche_ErstesErkanntesTrainingAbMontag()
    {
        var termin = Ziel(TrainingNoteGoal.NextWeek, new DateOnly(2026, 11, 9), Jetzt, Jetzt, erinnerung: false);

        Assert.Equal(new TrainingsTermin(new DateOnly(2026, 11, 11), new TimeOnly(18, 0), TrainingsQuelle.Tagebuch), termin);
    }

    [Fact]
    public void Ziel_NaechsteWoche_GruppentermineNachDemSonntagZaehlenNicht()
    {
        // Donnerstag, 19.11. liegt zehn Tage nach dem Montag: nicht mehr "nächste Woche", dann gilt das Muster der Woche.
        var termin = Trainingsrhythmus.ZielDesEintrags(
            TrainingNoteGoal.NextWeek, new DateOnly(2026, 11, 9), Jetzt, Jetzt, ZweiMittwoche, [Utc(19, 17)], null, erinnerung: false);

        Assert.Equal(new TrainingsTermin(new DateOnly(2026, 11, 11), new TimeOnly(18, 0), TrainingsQuelle.Tagebuch), termin);

        var nurSpaeter = Trainingsrhythmus.ZielDesEintrags(
            TrainingNoteGoal.NextWeek, new DateOnly(2026, 11, 9), Jetzt, Jetzt, Keine, [Utc(19, 17)], null, erinnerung: false);

        Assert.Equal(new TrainingsTermin(new DateOnly(2026, 11, 9), null, TrainingsQuelle.Unbekannt), nurSpaeter);
    }

    [Fact]
    public void Ziel_NaechsteWoche_OhneErkanntesTraining_MontagOhneUhrzeit()
    {
        var termin = Ziel(TrainingNoteGoal.NextWeek, new DateOnly(2026, 11, 9), Jetzt, Jetzt, erinnerung: false, tagebuch: Keine);

        Assert.Equal(new TrainingsTermin(new DateOnly(2026, 11, 9), null, TrainingsQuelle.Unbekannt), termin);
    }

    [Fact]
    public void Ziel_Datum_BekommtDieUhrzeitDesErkanntenTrainingsAnDiesemTag()
    {
        Assert.Equal(new TimeOnly(18, 0), Ziel(TrainingNoteGoal.Date, new DateOnly(2026, 11, 11), Jetzt, Jetzt, false)!.Value.Uhrzeit);
        Assert.Null(Ziel(TrainingNoteGoal.Date, new DateOnly(2026, 11, 12), Jetzt, Jetzt, false)!.Value.Uhrzeit);
    }

    [Fact]
    public void Ziel_NaechstesTraining_Unbekannt_ZumAnzeigenNull_FuerErinnerungWieNaechsteWoche()
    {
        Assert.Null(Ziel(TrainingNoteGoal.NextTraining, null, Jetzt, Jetzt, erinnerung: false, tagebuch: Keine));

        var erinnerung = Ziel(TrainingNoteGoal.NextTraining, null, Jetzt, Jetzt, erinnerung: true, tagebuch: Keine);
        Assert.Equal(new DateOnly(2026, 11, 9), erinnerung!.Value.Tag); // Montag nach dem Anlegen
    }

    [Fact]
    public void Ziel_NaechstesTraining_AmTrainingstagBleibtHeuteBisZumBeginn_AuchInDenLetztenZweiStunden()
    {
        // Mittwoch, 17:00 Uhr, Training 18:00; der Eintrag wurde gestern gemerkt: Er zielt noch auf heute (Karte auf der Startseite).
        var termin = Ziel(TrainingNoteGoal.NextTraining, null, Utc(3, 10), Utc(4, 16), erinnerung: false);

        Assert.Equal(new DateOnly(2026, 11, 4), termin!.Value.Tag);
    }

    [Fact]
    public void Ziel_NaechstesTraining_ErstNachDemErinnerungszeitpunktGemerkt_MeintDasFolgende()
    {
        // Mittwoch 17:30 Uhr gemerkt, Erinnerung wäre um 16:00 gewesen: Die Maske zeigte "nächste Woche".
        var termin = Ziel(TrainingNoteGoal.NextTraining, null, Utc(4, 16, 30), Utc(4, 16, 40), erinnerung: false);

        Assert.Equal(new DateOnly(2026, 11, 11), termin!.Value.Tag);
    }

    [Fact]
    public void Ziel_NaechstesTraining_NachDemTrainingsbeginn_IstEsDasFolgende()
    {
        var termin = Ziel(TrainingNoteGoal.NextTraining, null, Utc(3, 10), Utc(4, 17, 30), erinnerung: false);

        Assert.Equal(new DateOnly(2026, 11, 11), termin!.Value.Tag);
    }
}
