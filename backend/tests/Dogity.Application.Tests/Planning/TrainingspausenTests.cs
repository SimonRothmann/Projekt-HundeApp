using Dogity.Application.Planning;

namespace Dogity.Application.Tests.Planning;

/// <summary>
/// Die Regel, nach der eine Trainingspause den Plan anhält (docs/SMART_TRAINING_PLAN.md,
/// Abschnitt "Pausen"): Die Planwoche zählt nur Tage ohne Pause.
/// Reine Funktionen - ohne Datenbank, mit festen Daten.
/// </summary>
public class TrainingspausenTests
{
    // Plan beginnt am Montag, 6. Juli 2026 (Planwoche 1 = 6.-12.7.).
    private static readonly DateOnly Beginn = new(2026, 7, 6);

    private static DateOnly Tag(int tageNachBeginn) => Beginn.AddDays(tageNachBeginn);

    private static PausenZeitraum Pause(int vonTag, int? bisTag) =>
        new(Tag(vonTag), bisTag is { } b ? Tag(b) : null);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(20, 3)]
    public void Planwoche_OhnePause_ZaehltKalenderwochen(int tag, int erwartet) =>
        Assert.Equal(erwartet, Trainingspausen.Planwoche(Beginn, Tag(tag), []));

    [Fact]
    public void Planwoche_VorPlanbeginn_IstDieErste() =>
        Assert.Equal(1, Trainingspausen.Planwoche(Beginn, Tag(-10), []));

    [Fact]
    public void Planwoche_PauseMitteImPlan_WocheStehtStill()
    {
        // Pause von Tag 9 (Mi der 2. Woche) bis Tag 22: 14 Tage.
        var pausen = new[] { Pause(9, 22) };

        // Vor der Pause: Woche 2.
        Assert.Equal(2, Trainingspausen.Planwoche(Beginn, Tag(9), pausen));
        // Mitten in der Pause und am letzten Pausentag: unverändert Woche 2.
        Assert.Equal(2, Trainingspausen.Planwoche(Beginn, Tag(15), pausen));
        Assert.Equal(2, Trainingspausen.Planwoche(Beginn, Tag(22), pausen));
    }

    [Fact]
    public void Planwoche_NachDerPause_GehtEsDortWeiterWoDerPlanStand()
    {
        var pausen = new[] { Pause(9, 22) };

        // Tag 23 ist der erste Tag danach: Tag 9 von 14 gezählt - der Plan steht
        // bei Tag 3 der Woche 2 (9 aktive Tage), und genau da geht es weiter.
        Assert.Equal(2, Trainingspausen.Planwoche(Beginn, Tag(23), pausen));
        // Fünf Tage später (Tag 28): 9 + 5 = 14 aktive Tage = Beginn der Woche 3.
        Assert.Equal(2, Trainingspausen.Planwoche(Beginn, Tag(27), pausen));
        Assert.Equal(3, Trainingspausen.Planwoche(Beginn, Tag(28), pausen));
    }

    [Fact]
    public void Planwoche_OffenePause_HaeltBisHeuteAn()
    {
        var pausen = new[] { Pause(3, null) };

        Assert.Equal(1, Trainingspausen.Planwoche(Beginn, Tag(4), pausen));
        Assert.Equal(1, Trainingspausen.Planwoche(Beginn, Tag(60), pausen));
    }

    [Fact]
    public void Planwoche_PauseVorPlanbeginn_HatKeineWirkung()
    {
        var pausen = new[] { new PausenZeitraum(Beginn.AddDays(-30), Beginn.AddDays(-2)) };

        Assert.Equal(2, Trainingspausen.Planwoche(Beginn, Tag(7), pausen));
    }

    [Fact]
    public void Planwoche_PauseNachHeute_HatKeineWirkung()
    {
        var pausen = new[] { Pause(10, 20) };

        Assert.Equal(1, Trainingspausen.Planwoche(Beginn, Tag(6), pausen));
        Assert.Equal(2, Trainingspausen.Planwoche(Beginn, Tag(7), pausen));
    }

    [Fact]
    public void Planwoche_PauseDieVorDemPlanbeginnEndetUndImPlanWeitergeht_ZaehltNurDenTeilImPlan()
    {
        // Pause beginnt zwei Tage vor dem Plan und dauert bis Tag 3: nur Tag 0 bis 3 zählen.
        var pausen = new[] { new PausenZeitraum(Beginn.AddDays(-2), Tag(3)) };

        // Tag 11: 11 Tage vergangen - 4 Pausentage (0..3) = 7 aktiv -> Woche 2.
        Assert.Equal(2, Trainingspausen.Planwoche(Beginn, Tag(11), pausen));
        Assert.Equal(1, Trainingspausen.Planwoche(Beginn, Tag(10), pausen));
    }

    [Fact]
    public void PausentageZwischen_UeberschneidendePausen_ZaehlenJedenTagEinmal()
    {
        var pausen = new[] { Pause(2, 8), Pause(6, 12) };

        Assert.Equal(11, Trainingspausen.PausentageZwischen(pausen, Tag(0), Tag(30)));
    }

    [Fact]
    public void PausentageZwischen_BisIstAusgeschlossen()
    {
        var pausen = new[] { Pause(2, 5) };

        Assert.Equal(2, Trainingspausen.PausentageZwischen(pausen, Tag(0), Tag(4)));
        Assert.Equal(0, Trainingspausen.PausentageZwischen(pausen, Tag(0), Tag(2)));
        Assert.Equal(0, Trainingspausen.PausentageZwischen(pausen, Tag(5), Tag(5)));
    }

    [Fact]
    public void Pausiert_BeideGrenzenZaehlen()
    {
        var pausen = new[] { Pause(2, 4) };

        Assert.False(Trainingspausen.Pausiert(pausen, Tag(1)));
        Assert.True(Trainingspausen.Pausiert(pausen, Tag(2)));
        Assert.True(Trainingspausen.Pausiert(pausen, Tag(4)));
        Assert.False(Trainingspausen.Pausiert(pausen, Tag(5)));
        Assert.True(Trainingspausen.Pausiert([Pause(2, null)], Tag(400)));
    }

    // --- Hinweis "Zieldatum verschieben?" ----------------------------------
    // Plan mit 6 Wochen: Zieldatum Tag 41 (6 Wochen minus ein Tag), letzte Planwoche 6.

    [Fact]
    public void ZieldatumZuKnapp_OhnePause_NieEinHinweis() =>
        Assert.False(Trainingspausen.ZieldatumZuKnapp(Beginn, Tag(10), Tag(41), 6, []));

    [Fact]
    public void ZieldatumZuKnapp_KurzePause_ReichtNochAus()
    {
        // Drei Pausentage: Am Zieldatum (Tag 41) sind 38 aktive Tage vergangen = Woche 6. Reicht.
        Assert.False(Trainingspausen.ZieldatumZuKnapp(Beginn, Tag(10), Tag(41), 6, [Pause(12, 14)]));
    }

    [Fact]
    public void ZieldatumZuKnapp_LangePause_Hinweis()
    {
        // Zwei Wochen Pause: Am Zieldatum stünde der Plan erst in Woche 4.
        Assert.True(Trainingspausen.ZieldatumZuKnapp(Beginn, Tag(10), Tag(41), 6, [Pause(12, 25)]));
    }

    [Fact]
    public void ZieldatumZuKnapp_OffenePause_MahntNochNicht()
    {
        // Ohne bekanntes Ende kann niemand sagen, dass die Zeit nicht reicht.
        Assert.False(Trainingspausen.ZieldatumZuKnapp(Beginn, Tag(14), Tag(41), 6, [Pause(12, null)]));
    }

    [Fact]
    public void ZieldatumZuKnapp_PauseNachDemZieldatum_KeinHinweis() =>
        Assert.False(Trainingspausen.ZieldatumZuKnapp(Beginn, Tag(10), Tag(41), 6, [Pause(50, 90)]));

    // --- Wiedervorlage ------------------------------------------------------

    [Fact]
    public void FaelligkeitNachPausen_RuecktUmDiePausentageSeitherNachHinten()
    {
        // Fällig an Tag 5, Pause Tag 8 bis 14 (7 Tage), heute Tag 20: 7 Tage später fällig.
        Assert.Equal(Tag(12), Trainingspausen.FaelligkeitNachPausen(Tag(5), Tag(20), [Pause(8, 14)]));
    }

    [Fact]
    public void FaelligkeitNachPausen_PauseVorDerFaelligkeit_AendertNichts() =>
        Assert.Equal(Tag(10), Trainingspausen.FaelligkeitNachPausen(Tag(10), Tag(20), [Pause(1, 5)]));

    [Fact]
    public void FaelligkeitNachPausen_NochNichtFaellig_AendertNichts() =>
        Assert.Equal(Tag(30), Trainingspausen.FaelligkeitNachPausen(Tag(30), Tag(20), [Pause(8, 14)]));
}
