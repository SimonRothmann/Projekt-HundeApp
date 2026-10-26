using Dogity.Application.Planning;
using Dogity.Domain.Planning;

namespace Dogity.Application.Tests.Planning;

/// <summary>
/// Die Regel der Prüfungsreife als reine Funktion: Zustand je Übung, die
/// Gesamtampel an ihren Kanten, Gewichte (Punkte, Ordnungen ohne Punkte,
/// Wahlübungen), Pausen und Zeit bis zur Prüfung.
/// </summary>
public class PruefungsreifeTests
{
    // Mittwoch, 4. November 2026.
    private static readonly DateOnly Heute = new(2026, 11, 4);
    private static readonly DateOnly Ziel = Heute.AddDays(30);
    private static readonly IReadOnlyList<PausenZeitraum> KeinePause = [];

    private static ReifeUebung Offen(string name = "A", bool pflicht = true, int punkte = 10) =>
        new(Guid.NewGuid(), name, null, pflicht, punkte, 0, 1, 0, null);

    /// <summary>Eine Übung, die vor <paramref name="tage"/> Tagen trainiert wurde.</summary>
    private static ReifeUebung Trainiert(
        string name = "A", int box = 4, double bewertung = 4.5, int tage = 3, bool pflicht = true, int punkte = 10, int sessions = 5) =>
        new(Guid.NewGuid(), name, null, pflicht, punkte, sessions, box, bewertung, Heute.AddDays(-tage));

    private static ReifeUebung Sicher(string name = "A", bool pflicht = true, int punkte = 10) =>
        Trainiert(name, pflicht: pflicht, punkte: punkte);

    private static PruefungsreifeUebungDto Bewerte(ReifeUebung u, IEnumerable<PausenZeitraum>? pausen = null) =>
        Pruefungsreife.Bewerte(u, Heute, pausen ?? KeinePause);

    private static PruefungsreifeDto Reife(params ReifeUebung[] uebungen) =>
        Pruefungsreife.Berechne(uebungen, Heute, Ziel, KeinePause)!;

    // ---- Zustand je Übung ----

    [Fact]
    public void Uebung_NieTrainiert_IstOffen()
    {
        var r = Bewerte(Offen());

        Assert.Equal(ReifeZustand.Offen, r.Zustand);
        Assert.Null(r.Grund);
        Assert.Null(r.TageSeit);
        Assert.Null(r.Bewertung);
    }

    [Fact]
    public void Uebung_MitSessionCountNull_IstOffenAuchMitMasteryZeile()
    {
        // Eine Mastery-Zeile entsteht auch durch "mehr/weniger üben" - ohne Training bleibt die Übung offen.
        var u = new ReifeUebung(Guid.NewGuid(), "A", null, true, 10, 0, 5, 5.0, Heute.AddDays(-1));

        Assert.Equal(ReifeZustand.Offen, Bewerte(u).Zustand);
    }

    [Fact]
    public void Uebung_GefestigtGutUndFrisch_IstSicher()
    {
        var r = Bewerte(Trainiert(box: 4, bewertung: 4.0, tage: 5));

        Assert.Equal(ReifeZustand.Sicher, r.Zustand);
        Assert.Null(r.Grund);
        Assert.Equal(5, r.TageSeit);
    }

    [Theory]
    [InlineData(3, 4.5, 3)]    // Box zu niedrig
    [InlineData(5, 3.9, 3)]    // Bewertung knapp darunter
    [InlineData(5, 4.5, 29)]   // einen Tag zu lange her
    public void Uebung_JeEineBedingungVerfehlt_IstWackelig(int box, double bewertung, int tage)
    {
        var r = Bewerte(Trainiert(box: box, bewertung: bewertung, tage: tage));

        Assert.Equal(ReifeZustand.Wackelig, r.Zustand);
        Assert.NotNull(r.Grund);
    }

    [Theory]
    [InlineData(4, 28)]
    [InlineData(5, 28)]
    public void Uebung_AmRandDerAchtundzwanzigTage_IstNochSicher(int box, int tage)
    {
        Assert.Equal(ReifeZustand.Sicher, Bewerte(Trainiert(box: box, tage: tage)).Zustand);
    }

    [Fact]
    public void Grund_SchwacheBewertung_VorSeltenTrainiert()
    {
        // Box UND Bewertung verfehlt: Die Bewertung ist die aufschlussreichere Auskunft.
        var r = Bewerte(Trainiert(box: 2, bewertung: 2.8));

        Assert.Equal(ReifeGrund.SchwacheBewertung, r.Grund);
        Assert.Equal(2.8, r.Bewertung);
    }

    [Fact]
    public void Grund_BoxZuNiedrigBeiGuterBewertung_SeltenTrainiert()
    {
        Assert.Equal(ReifeGrund.SeltenTrainiert, Bewerte(Trainiert(box: 3, bewertung: 4.6)).Grund);
    }

    [Fact]
    public void Grund_FrueherSicherJetztLangeHer_LangeHer()
    {
        var r = Bewerte(Trainiert(box: 5, bewertung: 4.8, tage: 35));

        Assert.Equal(ReifeGrund.LangeHer, r.Grund);
        Assert.Equal(35, r.TageSeit);
    }

    [Fact]
    public void Bewertung_WirdAbgerundet_NieZurSchwelleHochgerundet()
    {
        // 3,96 würde auf "4,0" runden und eine Übung als wackelig zeigen, die "4,0" hat.
        var r = Bewerte(Trainiert(box: 5, bewertung: 3.96));

        Assert.Equal(ReifeZustand.Wackelig, r.Zustand);
        Assert.Equal(3.9, r.Bewertung);
    }

    [Fact]
    public void Bewertung_GleitendMittelDicht_AmGrenzwert_ZaehltAlsVier()
    {
        // Der gleitende Mittelwert rechnet in Fließkommazahlen: 4,0 darf nicht an der letzten Stelle scheitern.
        Assert.Equal(ReifeZustand.Sicher, Bewerte(Trainiert(box: 4, bewertung: 4.0 - 1e-12)).Zustand);
    }

    // ---- Pausen ----

    [Fact]
    public void Pause_VerlaengertZuletztTrainiertNicht_UebungBleibtSicher()
    {
        // 40 Kalendertage her, davon 21 Pause: 19 aktive Tage - wer verletzt war, verliert nichts.
        var pause = new PausenZeitraum(Heute.AddDays(-30), Heute.AddDays(-10));
        var u = Trainiert(box: 5, bewertung: 4.7, tage: 40);

        Assert.Equal(ReifeZustand.Wackelig, Bewerte(u).Zustand);

        var mitPause = Bewerte(u, [pause]);
        Assert.Equal(ReifeZustand.Sicher, mitPause.Zustand);
        Assert.Equal(40, mitPause.TageSeit);
        Assert.Equal(21, mitPause.PausenTage);
    }

    [Fact]
    public void Pause_ZuLange_AktiveTageUeberschreitenTrotzdemDieGrenze()
    {
        var pause = new PausenZeitraum(Heute.AddDays(-20), Heute.AddDays(-15)); // 6 Tage
        var u = Trainiert(box: 5, bewertung: 4.7, tage: 40);                   // 34 aktive Tage

        var r = Bewerte(u, [pause]);

        Assert.Equal(ReifeZustand.Wackelig, r.Zustand);
        Assert.Equal(ReifeGrund.LangeHer, r.Grund);
    }

    [Fact]
    public void Pause_OffeneUebungenBleibenOffen()
    {
        var pause = new PausenZeitraum(Heute.AddDays(-30), null);

        Assert.Equal(ReifeZustand.Offen, Bewerte(Offen(), [pause]).Zustand);
    }

    // ---- Ampel: Kanten ----

    /// <summary>Zehn Pflichtübungen à 10 Punkte; <paramref name="sichere"/> davon sicher, der Rest wackelig (nicht offen).</summary>
    private static PruefungsreifeDto ReifeMitSicheren(int sichere, int gesamt = 10)
    {
        var uebungen = Enumerable.Range(0, gesamt)
            .Select(i => i < sichere ? Sicher($"S{i}") : Trainiert($"W{i}", box: 2, bewertung: 3.0))
            .ToArray();
        return Reife(uebungen);
    }

    [Fact]
    public void Ampel_AchtzigProzentSicher_IstGruen()
    {
        Assert.Equal(Reifeampel.Gruen, ReifeMitSicheren(8).Ampel);
    }

    [Fact]
    public void Ampel_NeunUndSiebzigProzentNachPunkten_IstGelb()
    {
        // 79 von 100 Punkten: Fünf Übungen, davon vier mit 19/20/20/20 Punkten sicher.
        var r = Reife(
            Sicher("A", punkte: 19), Sicher("B", punkte: 20), Sicher("C", punkte: 20), Sicher("D", punkte: 20),
            Trainiert("E", box: 2, bewertung: 3.0, punkte: 21));

        Assert.Equal(Reifeampel.Gelb, r.Ampel);
    }

    [Fact]
    public void Ampel_ExaktAchtzigProzentNachPunkten_IstGruen()
    {
        var r = Reife(
            Sicher("A", punkte: 30), Sicher("B", punkte: 30), Sicher("C", punkte: 20),
            Trainiert("D", box: 2, bewertung: 3.0, punkte: 20));

        Assert.Equal(Reifeampel.Gruen, r.Ampel);
    }

    [Fact]
    public void Ampel_FuenfzigProzentSicher_IstGelb()
    {
        Assert.Equal(Reifeampel.Gelb, ReifeMitSicheren(5).Ampel);
    }

    [Fact]
    public void Ampel_NeunUndVierzigProzentNachPunkten_IstRot()
    {
        // 49 von 100 Punkten sicher.
        var r = Reife(
            Sicher("A", punkte: 49),
            Trainiert("B", box: 2, bewertung: 3.0, punkte: 51));

        Assert.Equal(Reifeampel.Rot, r.Ampel);
    }

    [Fact]
    public void Ampel_ExaktFuenfzigProzentNachPunkten_IstGelb()
    {
        var r = Reife(
            Sicher("A", punkte: 50),
            Trainiert("B", box: 2, bewertung: 3.0, punkte: 50));

        Assert.Equal(Reifeampel.Gelb, r.Ampel);
    }

    [Fact]
    public void Ampel_NeunUndSiebzigKommaNeunProzent_IstGelb()
    {
        // 799 von 1000: knapp unter der Grünschwelle (Ganzzahlrechnung ohne Rundung).
        var r = Reife(
            Sicher("A", punkte: 799),
            Trainiert("B", box: 2, bewertung: 3.0, punkte: 201));

        Assert.Equal(Reifeampel.Gelb, r.Ampel);
    }

    [Fact]
    public void Ampel_EinePflichtuebungOffen_IstRotAuchBeiVielSicherem()
    {
        var uebungen = Enumerable.Range(0, 9).Select(i => Sicher($"S{i}")).Append(Offen("Offen")).ToArray();

        Assert.Equal(Reifeampel.Rot, Reife(uebungen).Ampel);
    }

    [Fact]
    public void Ampel_AlleSicher_IstGruen()
    {
        Assert.Equal(Reifeampel.Gruen, Reife(Sicher("A"), Sicher("B")).Ampel);
    }

    [Fact]
    public void Ampel_AllesOffen_IstRot()
    {
        Assert.Equal(Reifeampel.Rot, Reife(Offen("A"), Offen("B")).Ampel);
    }

    // ---- Gewichte ----

    [Fact]
    public void Gewicht_NachHoechstpunkten_EineGroesseUebungWiegtSchwerer()
    {
        // Fußarbeit 60 sicher, drei kleine à 10 wackelig: 60 von 90 = 67 % -> gelb.
        var grosseSicher = Reife(
            Sicher("Fußarbeit", punkte: 60),
            Trainiert("B", box: 2, bewertung: 3.0, punkte: 10),
            Trainiert("C", box: 2, bewertung: 3.0, punkte: 10),
            Trainiert("D", box: 2, bewertung: 3.0, punkte: 10));
        Assert.Equal(Reifeampel.Gelb, grosseSicher.Ampel);

        // Umgekehrt nur die drei kleinen sicher: 30 von 90 = 33 % -> rot, obwohl drei von vier Übungen sicher sind.
        var kleineSicher = Reife(
            Trainiert("Fußarbeit", box: 2, bewertung: 3.0, punkte: 60),
            Sicher("B", punkte: 10), Sicher("C", punkte: 10), Sicher("D", punkte: 10));
        Assert.Equal(Reifeampel.Rot, kleineSicher.Ampel);
    }

    [Fact]
    public void Gewicht_OrdnungOhnePunkte_JedeUebungZaehltGleich()
    {
        // Vier Übungen, drei sicher (75 %) -> gelb; bei vier von fünf (80 %) -> grün.
        Assert.Equal(Reifeampel.Gelb,
            Reife(Sicher("A", punkte: 0), Sicher("B", punkte: 0), Sicher("C", punkte: 0),
                Trainiert("D", box: 2, bewertung: 3.0, punkte: 0)).Ampel);

        Assert.Equal(Reifeampel.Gruen,
            Reife(Sicher("A", punkte: 0), Sicher("B", punkte: 0), Sicher("C", punkte: 0), Sicher("D", punkte: 0),
                Trainiert("E", box: 2, bewertung: 3.0, punkte: 0)).Ampel);
    }

    [Fact]
    public void Gewicht_UebungOhnePunkteInPunkteOrdnung_BekommtDasKleinsteGewicht()
    {
        // BH: Teil B ohne Punkte ist Pflicht und zählt wie die kleinste bewertete Übung (10).
        var uebungen = new[]
        {
            Sicher("Fußarbeit", punkte: 30),
            Sicher("Sitz", punkte: 10),
            Trainiert("Verkehr", box: 2, bewertung: 3.0, punkte: 0),
        };

        var gewichte = Pruefungsreife.Gewichte(uebungen);

        Assert.Equal([60, 20, 20], gewichte);
        // 80 von 100 -> grün.
        Assert.Equal(Reifeampel.Gruen, Reife(uebungen).Ampel);
    }

    [Fact]
    public void Wahluebung_ZaehltMitHalbemGewicht()
    {
        var gewichte = Pruefungsreife.Gewichte([Offen("Pflicht", punkte: 10), Offen("Wahl", pflicht: false, punkte: 10)]);

        Assert.Equal([20, 10], gewichte);
    }

    [Fact]
    public void Wahluebung_Offen_LoestKeinRotAus_UndZaehltNurMitHalbemGewicht()
    {
        // Pflicht 4 x 10 sicher (Gewicht 80), Wahl 10 offen (halbes Gewicht, 10): 80 von 90 = 89 % -> grün.
        var uebungen = Enumerable.Range(0, 4).Select(i => Sicher($"P{i}")).Append(Offen("Wahl", pflicht: false)).ToArray();

        Assert.Equal(Reifeampel.Gruen, Reife(uebungen).Ampel);
    }

    [Fact]
    public void Wahluebung_ZiehtBeiVielGewichtDenAnteilUnterDieGrenze()
    {
        // Eine Pflicht 10 sicher (20), eine Wahl 100 offen (100): 20 von 120 = 17 % -> rot (nicht wegen "offen", sondern wegen des Anteils).
        var r = Reife(Sicher("Pflicht", punkte: 10), Offen("Wahl", pflicht: false, punkte: 100));

        Assert.Equal(Reifeampel.Rot, r.Ampel);
    }

    // ---- Sortierung / "Was noch fehlt" ----

    [Fact]
    public void Uebungen_FehlendesZuerst_NachGewichtAbsteigend_DannDasSichere()
    {
        var r = Reife(
            Sicher("Sicher", punkte: 50),
            Trainiert("Kleinwackelig", box: 2, bewertung: 3.0, punkte: 10),
            Offen("GrossOffen", punkte: 30),
            Trainiert("MittelWackelig", box: 2, bewertung: 3.0, punkte: 20));

        Assert.Equal(["GrossOffen", "MittelWackelig", "Kleinwackelig", "Sicher"], r.Uebungen.Select(u => u.Name));
    }

    [Fact]
    public void Uebungen_GleichesGewicht_OffenesVorWackeligem()
    {
        var r = Reife(Trainiert("Wackelig", box: 2, bewertung: 3.0), Offen("Offen"));

        Assert.Equal(["Offen", "Wackelig"], r.Uebungen.Select(u => u.Name));
    }

    [Fact]
    public void Zaehler_StimmenMitDenZustaenden()
    {
        var r = Reife(Sicher("A"), Sicher("B"), Trainiert("C", box: 2, bewertung: 3.0), Offen("D"));

        Assert.Equal((2, 1, 1), (r.Sicher, r.Wackelig, r.Offen));
        Assert.Equal(new PruefungsreifeKurzDto(Reifeampel.Rot, 1, 1, 2, ZieldatumVerschieben: false), Pruefungsreife.Kurz(r));
    }

    [Fact]
    public void KeineUebungen_KeinePruefungsreife()
    {
        Assert.Null(Pruefungsreife.Berechne([], Heute, Ziel, KeinePause));
    }

    // ---- Theorie ----

    private static TheorieZeileDto Theorie(ReifeZustand zustand) => new(zustand, "SWHV-BHVT-ERW", 0, 0, 0);

    [Fact]
    public void Theorie_NichtSicher_HaeltGruenZurueck_NichtAberImmerRot()
    {
        var uebungen = new[] { Sicher("A"), Sicher("B") };

        Assert.Equal(Reifeampel.Gruen, Pruefungsreife.Berechne(uebungen, Heute, Ziel, KeinePause, Theorie(ReifeZustand.Sicher))!.Ampel);
        Assert.Equal(Reifeampel.Gelb, Pruefungsreife.Berechne(uebungen, Heute, Ziel, KeinePause, Theorie(ReifeZustand.Wackelig))!.Ampel);
        // Offene Theorie macht nicht rot: Rot ist Sache der Übungen.
        Assert.Equal(Reifeampel.Gelb, Pruefungsreife.Berechne(uebungen, Heute, Ziel, KeinePause, Theorie(ReifeZustand.Offen))!.Ampel);
    }

    [Fact]
    public void Theorie_OhneTheoriezeile_ZaehltNicht()
    {
        var r = Pruefungsreife.Berechne([Sicher("A")], Heute, Ziel, KeinePause, theorie: null)!;

        Assert.Equal(Reifeampel.Gruen, r.Ampel);
        Assert.Null(r.Theorie);
    }

    [Theory]
    [InlineData(new bool[0], ReifeZustand.Offen)]
    [InlineData(new[] { true }, ReifeZustand.Wackelig)]
    [InlineData(new[] { false }, ReifeZustand.Wackelig)]
    [InlineData(new[] { true, false }, ReifeZustand.Wackelig)]
    [InlineData(new[] { false, true }, ReifeZustand.Wackelig)]
    [InlineData(new[] { true, true }, ReifeZustand.Sicher)]
    [InlineData(new[] { true, true, false }, ReifeZustand.Sicher)] // nur die letzten zwei zählen
    public void TheorieZustand_AusDenLetztenZweiUebungspruefungen(bool[] letzteBestanden, ReifeZustand erwartet)
    {
        Assert.Equal(erwartet, Pruefungsreife.TheorieZustand(letzteBestanden));
    }

    // ---- Zeit bis zur Prüfung ----

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(7, 1)]
    [InlineData(8, 2)]
    [InlineData(21, 3)]
    [InlineData(22, 4)]
    public void Zeit_WochenWerdenAufgerundet(int tage, int wochen)
    {
        var (t, pausenTage, w) = Pruefungsreife.ZeitBis(Heute, Heute.AddDays(tage), KeinePause);

        Assert.Equal((tage, 0, wochen), (t, pausenTage, w));
    }

    [Fact]
    public void Zeit_ZielInDerVergangenheit_NullWochen()
    {
        Assert.Equal((0, 0, 0), Pruefungsreife.ZeitBis(Heute, Heute.AddDays(-3), KeinePause));
    }

    [Fact]
    public void Zeit_BekanntePauseVorDemZiel_ZaehltNichtAlsTrainingszeit()
    {
        // 28 Tage bis zum Ziel, davon 14 Tage Pause: zwei Trainingswochen statt vier.
        var pause = new PausenZeitraum(Heute.AddDays(5), Heute.AddDays(18));

        var (tage, pausenTage, wochen) = Pruefungsreife.ZeitBis(Heute, Heute.AddDays(28), [pause]);

        Assert.Equal((28, 14, 2), (tage, pausenTage, wochen));
    }

    [Fact]
    public void Zeit_OffenePause_GiltNurBisHeute()
    {
        // "Bis auf Weiteres" hat kein bekanntes Ende: Es zählt nur der heutige Tag mit.
        var pause = new PausenZeitraum(Heute.AddDays(-5), null);

        var (_, pausenTage, wochen) = Pruefungsreife.ZeitBis(Heute, Heute.AddDays(28), [pause]);

        Assert.Equal(1, pausenTage);
        Assert.Equal(4, wochen);
    }

    // ---- Zieldatum verschieben ----

    [Theory]
    [InlineData(21, true)]   // rot, drei Wochen
    [InlineData(22, false)]  // rot, vier Wochen
    public void Verschieben_NurBeiRotUndHoechstensDreiWochen(int tage, bool erwartet)
    {
        var r = Pruefungsreife.Berechne([Offen("A")], Heute, Heute.AddDays(tage), KeinePause)!;

        Assert.Equal(Reifeampel.Rot, r.Ampel);
        Assert.Equal(erwartet, r.ZieldatumVerschieben);
    }

    [Fact]
    public void Verschieben_NichtBeiGelbOderGruen()
    {
        var gruen = Pruefungsreife.Berechne([Sicher("A")], Heute, Heute.AddDays(7), KeinePause)!;
        var gelb = Pruefungsreife.Berechne([Sicher("A"), Trainiert("B", box: 2, bewertung: 3.0)], Heute, Heute.AddDays(7), KeinePause)!;

        Assert.False(gruen.ZieldatumVerschieben);
        Assert.False(gelb.ZieldatumVerschieben);
    }

    [Fact]
    public void Verschieben_PausenVerkuerzenDieTrainingswochen()
    {
        // 35 Tage, davon 14 Pause: 21 Trainingstage = drei Wochen -> Hinweis trotz fünf Kalenderwochen.
        var pause = new PausenZeitraum(Heute.AddDays(3), Heute.AddDays(16));

        var r = Pruefungsreife.Berechne([Offen("A")], Heute, Heute.AddDays(35), [pause])!;

        Assert.Equal(3, r.Wochen);
        Assert.True(r.ZieldatumVerschieben);
    }

    [Fact]
    public void Pausiert_ZeigtAnHeuteLaufendePause()
    {
        var laeuft = Pruefungsreife.Berechne([Offen("A")], Heute, Ziel, [new PausenZeitraum(Heute.AddDays(-2), Heute.AddDays(5))])!;
        var vorbei = Pruefungsreife.Berechne([Offen("A")], Heute, Ziel, [new PausenZeitraum(Heute.AddDays(-9), Heute.AddDays(-2))])!;

        Assert.True(laeuft.Pausiert);
        Assert.False(vorbei.Pausiert);
    }

    // ---- Wann es eine Prüfungsreife gibt ----

    [Fact]
    public void Gilt_NurFuerLaufendeZieleMitPruefungsordnung()
    {
        Goal Ziel(GoalStatus status = GoalStatus.Active, bool eigenes = false, Guid? ordnung = null) =>
            new() { Status = status, IsCustom = eigenes, RegulationId = ordnung ?? Guid.NewGuid() };

        Assert.True(Pruefungsreife.Gilt(Ziel()));
        Assert.False(Pruefungsreife.Gilt(Ziel(eigenes: true)));
        Assert.False(Pruefungsreife.Gilt(new Goal { Status = GoalStatus.Active, RegulationId = null }));
        Assert.False(Pruefungsreife.Gilt(Ziel(GoalStatus.Achieved)));
        Assert.False(Pruefungsreife.Gilt(Ziel(GoalStatus.Cancelled)));
    }
}
