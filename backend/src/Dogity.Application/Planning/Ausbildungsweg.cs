namespace Dogity.Application.Planning;

/// <summary>
/// Der Ausbildungsweg: welche Prüfungsordnung auf welche folgt. Die einzige
/// Stelle dafür - das Frontend bekommt die Folgestufen über
/// <see cref="GoalDto.NextStages"/> und <see cref="Sports.RegulationDto.NextStageNames"/>
/// und kennt die Reihenfolge selbst nicht.
///
/// Geführt wird über die NAMEN der Prüfungsordnungen, so wie
/// SportCatalogSeeder sie anlegt - eine Prüfungsordnung hat keine andere
/// stabile Kennung (Ids entstehen erst beim Seeden). Ein Test
/// (AusbildungswegTests) prüft, dass jeder Name hier im Seeder existiert;
/// wird dort eine Prüfungsordnung umbenannt, schlägt er an, statt dass die
/// Folgestufe stillschweigend verschwindet.
///
/// Bewusst nur das, was sich fachlich eindeutig aufeinander aufbaut: Stufen
/// einer Reihe (IGP 1 -> 2 -> 3) und der Einstieg über die BH. Die früheren
/// "IGP x - Fährte" fehlen absichtlich: der Seeder räumt sie als Doppelung der
/// FCI-FPr weg, die Fährtenreihe läuft über FCI-FPr 1 bis 3. Hunde mit
/// anderen Zielen (z.B. Turnierhundsport jenseits VK, Jumping) bekommen
/// keinen Vorschlag.
/// </summary>
public static class Ausbildungsweg
{
    private static readonly Dictionary<string, string[]> FolgestufenJeName = Baue();

    private static Dictionary<string, string[]> Baue()
    {
        var weg = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            // Die BH ist die Eintrittskarte: danach wählt man die Richtung.
            ["BH"] = ["FCI-IGP 1", "IBGH1", "FCI-FPr 1", "FCI-UPr 1"],
        };

        // Reihen mit Stufen 1 bis 3 ("FCI-IGP 1" -> "FCI-IGP 2" -> "FCI-IGP 3").
        foreach (var reihe in new[]
        {
            new[] { "FCI-IGP 1", "FCI-IGP 2", "FCI-IGP 3" },
            new[] { "IBGH1", "IBGH2", "IBGH3" },
            new[] { "FCI-FPr 1", "FCI-FPr 2", "FCI-FPr 3" },
            new[] { "FCI-UPr 1", "FCI-UPr 2", "FCI-UPr 3" },
            new[] { "FCI-SPr 1", "FCI-SPr 2", "FCI-SPr 3" },
            new[] { "FCI-GPr 1", "FCI-GPr 2", "FCI-GPr 3" },
            new[] { "FCI-StöPr 1", "FCI-StöPr 2", "FCI-StöPr 3" },
            new[] { "FCI-IFH 1", "FCI-IFH 2", "FCI-IFH 3" },
            new[] { "Agility 0 (A0)", "Agility 1 (A1)", "Agility 2 (A2)", "Agility 3 (A3)" },
            new[] { "VDH-VK1", "VDH-VK2", "VDH-VK3" },
        })
        {
            for (var i = 0; i < reihe.Length - 1; i++)
                weg[reihe[i]] = [reihe[i + 1]];
        }

        return weg;
    }

    /// <summary>Alle Namen, die der Ausbildungsweg kennt - Vorgänger wie Folgestufen.</summary>
    public static IEnumerable<string> AlleNamen =>
        FolgestufenJeName.Keys.Concat(FolgestufenJeName.Values.SelectMany(n => n)).Distinct(StringComparer.Ordinal);

    /// <summary>Namen der Prüfungsordnungen, die nach <paramref name="name"/> kommen - leer, wenn es keine gibt.</summary>
    public static IReadOnlyList<string> FolgestufenVon(string? name) =>
        name is not null && FolgestufenJeName.TryGetValue(name, out var folgen) ? folgen : [];
}
