using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Dogity.Application.Common;

namespace Dogity.Application.Tests.Common;

/// <summary>
/// Der Wächter über die Vollständigkeit der englischen Benachrichtigungstexte -
/// das Gegenstück zu frontend/src/lib/i18n/i18n.test.ts.
///
/// Weil der Schlüssel der deutsche Satz selbst ist, merkt kein Compiler, wenn zu
/// einer neuen Vorlage die englische Fassung fehlt: Der Rückfall auf Deutsch ist
/// gutmütig, und englische Nutzer:innen läsen still deutsche Meldungen. Deshalb
/// liest dieser Test alle <c>Meldung.Von("...")</c>-Aufrufe aus dem Quelltext.
/// </summary>
public partial class UebersetzungTests
{
    private static readonly string Quelle = BackendQuelle();

    private static string BackendQuelle([CallerFilePath] string datei = "")
    {
        // .../backend/tests/Dogity.Application.Tests/Common/UebersetzungTests.cs -> .../backend/src
        var backend = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(datei)!, "..", "..", ".."));
        return Path.Combine(backend, "src");
    }

    private sealed record Aufruf(string Datei, string Vorlage, IReadOnlyList<string> Namen);

    [GeneratedRegex(@"^\s*//.*$", RegexOptions.Multiline)]
    private static partial Regex Zeilenkommentar();

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Platzhalter();

    [GeneratedRegex(@"Meldung\.Von\(")]
    private static partial Regex Aufrufanfang();

    [GeneratedRegex(@"\(\s*""(\w+)""\s*,")]
    private static partial Regex WertName();

    private static List<string> Platzhalterliste(string text) =>
        Platzhalter().Matches(text).Select(m => m.Groups[1].Value).Distinct().Order().ToList();

    /// <summary>Position der schließenden Klammer; Zeichenketten (auch leere) werden übersprungen.</summary>
    private static int KlammerEnde(string code, int auf)
    {
        var tiefe = 0;
        for (var i = auf; i < code.Length; i++)
        {
            var c = code[i];
            if (c == '"')
            {
                i++;
                while (i < code.Length && code[i] != '"') i += code[i] == '\\' ? 2 : 1;
            }
            else if (c == '(') tiefe++;
            else if (c == ')' && --tiefe == 0) return i;
        }
        return code.Length;
    }

    private static (List<Aufruf> Aufrufe, List<string> Unlesbar) SammleAufrufe()
    {
        var aufrufe = new List<Aufruf>();
        var unlesbar = new List<string>();
        foreach (var pfad in Directory.EnumerateFiles(Quelle, "*.cs", SearchOption.AllDirectories))
        {
            var name = Path.GetRelativePath(Quelle, pfad);
            if (name.Split(Path.DirectorySeparatorChar).Any(t => t is "obj" or "bin")) continue;

            var code = Zeilenkommentar().Replace(File.ReadAllText(pfad), "");
            foreach (Match anfang in Aufrufanfang().Matches(code))
            {
                var auf = anfang.Index + anfang.Length - 1;
                var ende = KlammerEnde(code, auf);
                var argumente = code[(auf + 1)..ende];
                var literal = Regex.Match(argumente, @"^\s*""((?:[^""\\]|\\.)*)""");
                if (!literal.Success)
                {
                    // Eine Variable oder ein $"..." als Vorlage: nicht prüfbar, und genau das, was die Vorlage verhindern soll.
                    unlesbar.Add($"{name}: Meldung.Von({argumente.Trim()[..Math.Min(60, argumente.Trim().Length)]}");
                    continue;
                }
                var vorlage = literal.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");
                var namen = WertName().Matches(argumente[literal.Length..]).Select(m => m.Groups[1].Value).Distinct().Order().ToList();
                aufrufe.Add(new Aufruf(name, vorlage, namen));
            }
        }
        return (aufrufe, unlesbar);
    }

    [Fact]
    public void FindetDieVorlagenImQuelltext_SonstPruefteDerTestNichts()
    {
        var (aufrufe, _) = SammleAufrufe();

        Assert.True(aufrufe.Count > 30, $"Nur {aufrufe.Count} Vorlagen gefunden - hat sich das Muster geändert?");
    }

    [Fact]
    public void JedeVorlageIstEinZeichenkettenLiteral()
    {
        var (_, unlesbar) = SammleAufrufe();

        Assert.True(unlesbar.Count == 0, "Die Vorlage muss ein Literal sein (sonst kann sie niemand übersetzen):\n" + string.Join("\n", unlesbar));
    }

    [Fact]
    public void ProduktivcodeBautMeldungenNurUeberVon()
    {
        // Der öffentliche Konstruktor bleibt für Tests. Im Produktivcode ginge eine
        // "new Meldung(...)" an diesem Wächter vorbei - der Satz bliebe still deutsch.
        var umgehend = Directory.EnumerateFiles(Quelle, "*.cs", SearchOption.AllDirectories)
            .Where(pfad => File.ReadAllText(pfad).Contains("new Meldung("))
            .Select(pfad => Path.GetRelativePath(Quelle, pfad))
            .ToList();

        Assert.True(umgehend.Count == 0, "Meldung.Von(\"...\") statt new Meldung(...):\n" + string.Join("\n", umgehend));
    }

    [Fact]
    public void JedeVorlageHatEineEnglischeFassung()
    {
        var (aufrufe, _) = SammleAufrufe();

        var fehlend = aufrufe.Where(a => !Uebersetzung.Englisch.ContainsKey(a.Vorlage)).Select(a => $"{a.Datei}: {a.Vorlage}").Distinct().ToList();

        Assert.True(fehlend.Count == 0, "Ohne englische Fassung:\n" + string.Join("\n", fehlend));
    }

    [Fact]
    public void EnglischeFassungHatDieselbenPlatzhalter()
    {
        // Ein verlorenes {name} fiele sonst erst auf, wenn jemand "Hi , ..." in der Glocke sieht.
        var abweichend = Uebersetzung.Englisch
            .Where(e => !Platzhalterliste(e.Key).SequenceEqual(Platzhalterliste(e.Value)))
            .Select(e => e.Key)
            .ToList();

        Assert.True(abweichend.Count == 0, "Platzhalter weichen ab:\n" + string.Join("\n", abweichend));
    }

    [Fact]
    public void DieAufrufeLiefernGenauDieWerteDerVorlage()
    {
        // Ein vergessener oder falsch benannter Wert bliebe als "{hund}" im Satz stehen.
        var (aufrufe, _) = SammleAufrufe();

        var falsch = aufrufe
            .Where(a => !Platzhalterliste(a.Vorlage).SequenceEqual(a.Namen))
            .Select(a => $"{a.Datei}: {a.Vorlage} (Werte: {string.Join(", ", a.Namen)})")
            .ToList();

        Assert.True(falsch.Count == 0, "Werte und Platzhalter passen nicht zusammen:\n" + string.Join("\n", falsch));
    }

    [Fact]
    public void KeinEnglischerSatzOhneVerwendung()
    {
        // Wer den deutschen Satz ändert, verliert die Übersetzung - der alte Eintrag bliebe sonst als Leiche stehen.
        var (aufrufe, _) = SammleAufrufe();
        var verwendet = aufrufe.Select(a => a.Vorlage).ToHashSet();

        var verwaist = Uebersetzung.Englisch.Keys.Where(k => !verwendet.Contains(k)).ToList();

        Assert.True(verwaist.Count == 0, "Nicht mehr verwendet:\n" + string.Join("\n", verwaist));
    }

    [Fact]
    public void KeineEnglischeFassungIstLeerOderGleichDemDeutschen()
    {
        Assert.All(Uebersetzung.Englisch, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Value), e.Key);
            Assert.NotEqual(e.Key, e.Value);
        });
    }

    [Fact]
    public void Rendere_Deutsch_SetztDieWerteEin()
    {
        var text = Uebersetzung.Rendere(Meldung.Von("Du bist jetzt Trainer bei \"{verein}\".", ("verein", "Nord")), "de");

        Assert.Equal("Du bist jetzt Trainer bei \"Nord\".", text);
    }

    [Fact]
    public void Rendere_Englisch_NimmtDieEnglischeFassung()
    {
        var text = Uebersetzung.Rendere(Meldung.Von("Du bist jetzt Trainer bei \"{verein}\".", ("verein", "Nord")), "en");

        Assert.Equal("You are now a trainer at \"Nord\".", text);
    }

    [Theory]
    [InlineData("en-GB")]
    [InlineData("EN")]
    public void Rendere_EnglischMitRegionOderGrossgeschrieben_IstEnglisch(string sprache)
    {
        var text = Uebersetzung.Rendere(Meldung.Von("Du bist jetzt Trainer bei \"{verein}\".", ("verein", "Nord")), sprache);

        Assert.Equal("You are now a trainer at \"Nord\".", text);
    }

    [Fact]
    public void Rendere_ZeitpunktAlsWert_WirdAlsTagGeschrieben()
    {
        var meldung = Meldung.Von(
            "Dein Trainer hat Feedback zu deinem Training vom {datum} hinterlassen.",
            ("datum", new DateTimeOffset(2026, 9, 5, 18, 30, 0, TimeSpan.Zero)));

        Assert.Equal("Dein Trainer hat Feedback zu deinem Training vom 05.09.2026 hinterlassen.", Uebersetzung.Rendere(meldung, "de"));
        Assert.Equal("Your trainer left feedback on your training session from 5 Sep 2026.", Uebersetzung.Rendere(meldung, "en"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr")]
    public void Rendere_OhneOderMitUnbekannterSprache_IstDeutsch(string? sprache)
    {
        var text = Uebersetzung.Rendere(Meldung.Von("Du bist jetzt Trainer bei \"{verein}\".", ("verein", "Nord")), sprache);

        Assert.Equal("Du bist jetzt Trainer bei \"Nord\".", text);
    }

    [Fact]
    public void Rendere_VorlageOhneEnglischeFassung_FaelltAufDeutschZurueck()
    {
        var text = Uebersetzung.Rendere(Meldung.Von("Diesen Satz gibt es nicht {x}.", ("x", "1")), "en");

        Assert.Equal("Diesen Satz gibt es nicht 1.", text);
    }

    [Fact]
    public void Rendere_UnbekannterPlatzhalter_BleibtStehen()
    {
        var text = Uebersetzung.Rendere(Meldung.Von("Hallo {name}.", ("andere", "x")), "de");

        Assert.Equal("Hallo {name}.", text);
    }

    [Fact]
    public void Rendere_Datum_WirdJeSpracheGeschrieben()
    {
        var meldung = Meldung.Von("Dein Trainer hat Feedback zu deinem Training vom {datum} hinterlassen.", ("datum", new DateOnly(2026, 9, 5)));

        Assert.Equal("Dein Trainer hat Feedback zu deinem Training vom 05.09.2026 hinterlassen.", Uebersetzung.Rendere(meldung, "de"));
        Assert.Equal("Your trainer left feedback on your training session from 5 Sep 2026.", Uebersetzung.Rendere(meldung, "en"));
    }

    [Fact]
    public void Rendere_Zahl_OhneTausenderzeichenUndUnabhaengigVonDerServerkultur()
    {
        var text = Uebersetzung.Rendere(
            Meldung.Von("Der Trainingsplan für deinen Hund wurde für Woche {woche} automatisch angepasst.", ("woche", 12)), "en");

        Assert.Equal("The training plan for your dog was adjusted automatically for week 12.", text);
    }
}
