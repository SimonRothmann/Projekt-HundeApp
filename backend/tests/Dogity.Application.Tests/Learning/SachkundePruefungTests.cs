using Dogity.Application.Learning;
using Dogity.Domain.Learning;

namespace Dogity.Application.Tests.Learning;

/// <summary>
/// Die Regeln der Übungsprüfung ohne Datenbank: welche Fragen drankommen und
/// wie gewertet wird. Die Wertung ist eine Festlegung des Betreibers (die
/// verbreitete Vereinsregel, nicht aus den swhv-PDFs) - diese Tests halten sie
/// fest, damit sie nicht unbemerkt kippt.
/// </summary>
public class SachkundePruefungTests
{
    // ---- Bausteine ----

    private static QuizQuestion Auswahl(string abschnitt, int nr, int richtige = 1, int falsche = 3)
    {
        var frage = new QuizQuestion
        {
            Section = abschnitt, SectionName = abschnitt, Number = $"{abschnitt}{nr}", SortOrder = nr,
            Kind = richtige > 1 ? QuizQuestionKind.MultipleChoice : QuizQuestionKind.SingleChoice,
        };
        var reihenfolge = 1;
        for (var i = 0; i < richtige; i++)
            frage.Options.Add(new QuizOption { Kind = QuizOptionKind.Answer, Text = "richtig", IsCorrect = true, SortOrder = reihenfolge++ });
        for (var i = 0; i < falsche; i++)
            frage.Options.Add(new QuizOption { Kind = QuizOptionKind.Answer, Text = "falsch", IsCorrect = false, SortOrder = reihenfolge++ });
        return frage;
    }

    private static QuizQuestion Zuordnung(string abschnitt, int nr)
    {
        var frage = new QuizQuestion
        {
            Section = abschnitt, SectionName = abschnitt, Number = $"{abschnitt}{nr}", SortOrder = nr,
            Kind = QuizQuestionKind.Assignment,
        };
        frage.Options.Add(new QuizOption { Kind = QuizOptionKind.Term, Text = "Boxer", MatchKey = "E", SortOrder = 101 });
        frage.Options.Add(new QuizOption { Kind = QuizOptionKind.Term, Text = "Basset", MatchKey = "C", SortOrder = 102 });
        return frage;
    }

    private static QuizQuestion Freitext(string abschnitt, int nr) => new()
    {
        Section = abschnitt, SectionName = abschnitt, Number = $"{abschnitt}{nr}", SortOrder = nr,
        Kind = QuizQuestionKind.FreeText, SampleSolution = "Lösung",
    };

    /// <summary>Der Erwachsenenkatalog: A 23 (davon 3 Zuordnungen) · B 19 · C 10 · D 13 (davon 3 Freitext) · E 7.</summary>
    private static List<QuizQuestion> Erwachsenenkatalog()
    {
        var fragen = new List<QuizQuestion>();
        for (var i = 1; i <= 20; i++) fragen.Add(Auswahl("A", i));
        for (var i = 21; i <= 23; i++) fragen.Add(Zuordnung("A", i));
        for (var i = 1; i <= 19; i++) fragen.Add(Auswahl("B", i));
        for (var i = 1; i <= 10; i++) fragen.Add(Auswahl("C", i));
        for (var i = 1; i <= 10; i++) fragen.Add(Auswahl("D", i));
        for (var i = 11; i <= 13; i++) fragen.Add(Freitext("D", i));
        for (var i = 1; i <= 7; i++) fragen.Add(Auswahl("E", i));
        return fragen;
    }

    private static Dictionary<string, int> JeKomplex(IEnumerable<QuizQuestion> fragen) =>
        fragen.GroupBy(f => f.Section).ToDictionary(g => g.Key, g => g.Count());

    // ---- Auswahl ----

    [Fact]
    public void Erwachsene_25Fragen_AnteiligNachKomplex()
    {
        var auswahl = SachkundePruefung.WaehleFragen(Erwachsenenkatalog(), 25, new Random(1));

        Assert.Equal(25, auswahl.Count);
        // Wertbar: A 23 · B 19 · C 10 · D 10 · E 7 -> größte Reste ergeben 8 · 7 · 4 · 4 · 2.
        var jeKomplex = JeKomplex(auswahl);
        Assert.Equal(8, jeKomplex["A"]);
        Assert.Equal(7, jeKomplex["B"]);
        Assert.Equal(4, jeKomplex["C"]);
        Assert.Equal(4, jeKomplex["D"]);
        Assert.Equal(2, jeKomplex["E"]);
    }

    [Fact]
    public void Verteilung_haengtNichtVomZufallAb()
    {
        foreach (var startwert in Enumerable.Range(0, 20))
        {
            var jeKomplex = JeKomplex(SachkundePruefung.WaehleFragen(Erwachsenenkatalog(), 25, new Random(startwert)));
            Assert.Equal([8, 7, 4, 4, 2], new[] { jeKomplex["A"], jeKomplex["B"], jeKomplex["C"], jeKomplex["D"], jeKomplex["E"] });
        }
    }

    [Fact]
    public void Freitext_KommtNieVor()
    {
        foreach (var startwert in Enumerable.Range(0, 20))
            Assert.DoesNotContain(
                SachkundePruefung.WaehleFragen(Erwachsenenkatalog(), 25, new Random(startwert)),
                f => f.Kind == QuizQuestionKind.FreeText);
    }

    [Fact]
    public void WeichGeloeschteFragen_KommenNichtVor()
    {
        var fragen = Erwachsenenkatalog();
        foreach (var f in fragen.Where(f => f.Section == "E")) f.DeletedAt = DateTimeOffset.UtcNow;

        var auswahl = SachkundePruefung.WaehleFragen(fragen, 25, new Random(3));

        Assert.Equal(25, auswahl.Count);
        Assert.DoesNotContain(auswahl, f => f.Section == "E");
    }

    [Fact]
    public void KeineFrageKommtDoppeltVor()
    {
        var auswahl = SachkundePruefung.WaehleFragen(Erwachsenenkatalog(), 25, new Random(7));

        Assert.Equal(auswahl.Count, auswahl.Select(f => f.Id).Distinct().Count());
    }

    [Fact]
    public void VerlangtDerKatalogMehrAlsDa_KommenAlleWertbaren()
    {
        // Genau so viele wertbare wie verlangt: alle kommen dran.
        var jugend = Enumerable.Range(1, 40).Select(i => Auswahl("J", i)).ToList();

        var auswahl = SachkundePruefung.WaehleFragen(jugend, 40, new Random(5));
        Assert.Equal(40, auswahl.Count);

        // Auch bei Überschuss: nie mehr, als wertbar sind.
        Assert.Equal(40, SachkundePruefung.WaehleFragen(jugend, 99, new Random(5)).Count);
    }

    [Fact]
    public void Jugend_FuenfzehnAusVierzig_KeineDopplung_UndZufallsabhaengig()
    {
        var jugend = Enumerable.Range(1, 40).Select(i => Auswahl("J", i)).ToList();

        var eins = SachkundePruefung.WaehleFragen(jugend, 15, new Random(1));
        var zwei = SachkundePruefung.WaehleFragen(jugend, 15, new Random(2));

        Assert.Equal(15, eins.Count);
        Assert.Equal(15, eins.Select(f => f.Id).Distinct().Count());
        Assert.Equal(15, zwei.Count);
        Assert.NotEqual(eins.Select(f => f.Id).Order(), zwei.Select(f => f.Id).Order());
    }

    [Fact]
    public void Reihenfolge_IstGemischt_NichtDieDesKatalogs()
    {
        var jugend = Enumerable.Range(1, 40).Select(i => Auswahl("J", i)).ToList();

        var auswahl = SachkundePruefung.WaehleFragen(jugend, 40, new Random(11));

        Assert.NotEqual(jugend.Select(f => f.Id), auswahl.Select(f => f.Id));
    }

    [Fact]
    public void FesterZufall_ErgibtDieselbePruefung()
    {
        var katalog = Erwachsenenkatalog();

        var erste = SachkundePruefung.WaehleFragen(katalog, 25, new Random(42)).Select(f => f.Id);
        var zweite = SachkundePruefung.WaehleFragen(katalog, 25, new Random(42)).Select(f => f.Id);

        Assert.Equal(erste, zweite);
    }

    [Fact]
    public void VerschiedeneZufaelle_ZiehenVerschiedeneFragen()
    {
        var katalog = Erwachsenenkatalog();

        var erste = SachkundePruefung.WaehleFragen(katalog, 25, new Random(1)).Select(f => f.Id).ToHashSet();
        var zweite = SachkundePruefung.WaehleFragen(katalog, 25, new Random(2)).Select(f => f.Id).ToHashSet();

        Assert.False(erste.SetEquals(zweite));
    }

    [Fact]
    public void Antworten_BleibenInKatalogreihenfolge()
    {
        // Die Auswahl fasst die Antworten nicht an: sie behalten ihre SortOrder.
        var frage = Auswahl("A", 1, falsche: 3);

        var auswahl = SachkundePruefung.WaehleFragen([frage], 1, new Random(0));

        Assert.Equal([1, 2, 3, 4], auswahl[0].Options.Select(o => o.SortOrder));
    }

    [Theory]
    [InlineData(new[] { 23, 19, 10, 10, 7 }, 25, new[] { 8, 7, 4, 4, 2 })]
    [InlineData(new[] { 10, 10 }, 5, new[] { 3, 2 })]    // Gleichstand: die frühere Gruppe gewinnt
    [InlineData(new[] { 3, 3, 3 }, 3, new[] { 1, 1, 1 })]
    [InlineData(new[] { 5, 1 }, 6, new[] { 5, 1 })]      // alles verlangt
    [InlineData(new[] { 5, 1 }, 0, new[] { 0, 0 })]
    public void Verteilen_NachGroesstenResten(int[] groessen, int anzahl, int[] erwartet)
    {
        Assert.Equal(erwartet, SachkundePruefung.Verteilen(groessen, anzahl));
    }

    [Fact]
    public void Verteilen_VergibtNieMehrAlsEineGruppeHat()
    {
        var groessen = new[] { 1, 1, 1, 30 };
        var anteile = SachkundePruefung.Verteilen(groessen, 10);

        Assert.Equal(10, anteile.Sum());
        for (var i = 0; i < groessen.Length; i++) Assert.InRange(anteile[i], 0, groessen[i]);
    }

    // ---- Wertung: Auswahlfragen ----

    private static readonly Guid R = Guid.NewGuid();
    private static readonly Guid F1 = Guid.NewGuid();
    private static readonly Guid F2 = Guid.NewGuid();
    private static readonly Guid F3 = Guid.NewGuid();

    [Fact]
    public void NurDieRichtigeAngekreuzt_ZweiPunkte() =>
        Assert.Equal(2, SachkundePruefung.PunkteAuswahl([R], [R]));

    [Fact]
    public void NurEineFalscheAngekreuzt_NullPunkte_NichtMinusZwei() =>
        Assert.Equal(0, SachkundePruefung.PunkteAuswahl([R], [F1]));

    [Fact]
    public void RichtigUndFalschAngekreuzt_NullPunkte() =>
        Assert.Equal(0, SachkundePruefung.PunkteAuswahl([R], [R, F1]));

    [Fact]
    public void AllesAngekreuzt_NullPunkte() =>
        Assert.Equal(0, SachkundePruefung.PunkteAuswahl([R], [R, F1, F2, F3]));

    [Fact]
    public void NichtsAngekreuzt_NullPunkte() =>
        Assert.Equal(0, SachkundePruefung.PunkteAuswahl([R], []));

    [Fact]
    public void DoppeltAngekreuzt_ZaehltEinmal() =>
        Assert.Equal(2, SachkundePruefung.PunkteAuswahl([R], [R, R]));

    [Fact]
    public void MehrereRichtige_Allgemein()
    {
        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();
        var richtige = new[] { r1, r2 };

        Assert.Equal(4, SachkundePruefung.PunkteAuswahl(richtige, [r1, r2]));      // beide: 2 x 2
        Assert.Equal(2, SachkundePruefung.PunkteAuswahl(richtige, [r1]));          // eine: 2
        Assert.Equal(2, SachkundePruefung.PunkteAuswahl(richtige, [r1, r2, F1]));  // 4 - 2
        Assert.Equal(0, SachkundePruefung.PunkteAuswahl(richtige, [r1, F1]));      // 2 - 2
        Assert.Equal(0, SachkundePruefung.PunkteAuswahl(richtige, [r1, F1, F2]));  // 2 - 4, nicht unter 0
    }

    [Fact]
    public void HoechstPunkte_ZweiJeRichtigerAntwort()
    {
        Assert.Equal(2, SachkundePruefung.HoechstPunkte(Auswahl("A", 1)));
        Assert.Equal(6, SachkundePruefung.HoechstPunkte(Auswahl("A", 2, richtige: 3)));
        Assert.Equal(2, SachkundePruefung.HoechstPunkte(Zuordnung("A", 3)));
    }

    // ---- Wertung: Zuordnung ----

    [Fact]
    public void Zuordnung_AlleBegriffeStimmen_ZweiPunkte()
    {
        var frage = Zuordnung("A", 1);
        var begriffe = frage.Options.ToList();

        var punkte = SachkundePruefung.PunkteZuordnung(
            begriffe, new Dictionary<Guid, string> { [begriffe[0].Id] = "E", [begriffe[1].Id] = "c" });

        Assert.Equal(2, punkte); // Groß-/Kleinschreibung egal, wie im Lernmodus
    }

    [Fact]
    public void Zuordnung_EinBegriffFalsch_NullPunkte_NichtAnteilig()
    {
        var begriffe = Zuordnung("A", 1).Options.ToList();

        var punkte = SachkundePruefung.PunkteZuordnung(
            begriffe, new Dictionary<Guid, string> { [begriffe[0].Id] = "E", [begriffe[1].Id] = "A" });

        Assert.Equal(0, punkte);
    }

    [Fact]
    public void Zuordnung_FehlenderBegriffOderLeer_NullPunkte()
    {
        var begriffe = Zuordnung("A", 1).Options.ToList();

        Assert.Equal(0, SachkundePruefung.PunkteZuordnung(begriffe, new Dictionary<Guid, string> { [begriffe[0].Id] = "E" }));
        Assert.Equal(0, SachkundePruefung.PunkteZuordnung(begriffe, null));
    }

    // ---- Bestehensgrenze ----

    [Theory]
    [InlineData(35, 50, true)]   // genau 70 %
    [InlineData(34, 50, false)]
    [InlineData(56, 80, true)]   // genau 70 %
    [InlineData(55, 80, false)]
    [InlineData(50, 50, true)]
    [InlineData(0, 50, false)]
    [InlineData(0, 0, false)]    // eine Prüfung ohne mögliche Punkte besteht niemand
    public void Bestanden_AbSiebzigProzent_OhneRundung(int punkte, int hoechst, bool erwartet) =>
        Assert.Equal(erwartet, SachkundePruefung.Bestanden(punkte, hoechst));

    [Theory]
    [InlineData(50, 35)]
    [InlineData(80, 56)]
    [InlineData(10, 7)]
    [InlineData(2, 2)]           // 1,4 aufgerundet: ein Punkt reicht bei 2 möglichen nicht (50 %)
    public void Bestehensgrenze_DieKleinsteBestehendePunktzahl(int hoechst, int erwartet)
    {
        Assert.Equal(erwartet, SachkundePruefung.Bestehensgrenze(hoechst));
        Assert.True(SachkundePruefung.Bestanden(erwartet, hoechst));
        if (erwartet > 0) Assert.False(SachkundePruefung.Bestanden(erwartet - 1, hoechst));
    }

    [Theory]
    [InlineData(38, 50, 76)]
    [InlineData(34, 50, 68)]
    [InlineData(55, 80, 68)]     // 68,75 -> abgerundet: eine nicht bestandene Prüfung zeigt nie "70 %"
    [InlineData(0, 0, 0)]
    public void Prozent_RundetAb(int punkte, int hoechst, int erwartet) =>
        Assert.Equal(erwartet, SachkundePruefung.Prozent(punkte, hoechst));
}
