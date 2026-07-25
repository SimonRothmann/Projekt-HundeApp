using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Infrastructure.Persistence.Seed;

/// <summary>
/// Legt vorgefertigte, komplette Trainingseinheiten für Gruppen an (Welpen und
/// Junghunde). Diese System-Vorlagen (CreatedByUserId == null, GroupId == null)
/// sind für alle Trainer sichtbar und dienen als fachlicher Startpunkt: ein
/// Trainer kann sie in seine eigene Gruppe kopieren und anschließend anpassen.
///
/// Die Inhalte sind eigene, an gängige Grundausbildungs-/Welpengruppen-Praxis
/// angelehnte Beschreibungen - keine Übernahme aus urheberrechtlich geschützten
/// Quellen (analog zur Vorsichtsregel in <see cref="SportCatalogSeeder"/>).
///
/// Idempotent auf Ebene der einzelnen Einheit (Schlüssel: Titel unter den
/// System-Vorlagen), damit der Katalog später ergänzt werden kann, ohne
/// Duplikate zu erzeugen. Bestehende (auch angepasste) Einheiten werden nie
/// überschrieben.
/// </summary>
public static class GroupTrainingSeeder
{
    private sealed record ItemSeed(string Title, string Focus, int DurationMinutes, string Description);

    private sealed record UnitSeed(GroupTrainingCategory Category, string Title, string Description, ItemSeed[] Items);

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();

        // Titel der bereits vorhandenen System-Vorlagen (für Idempotenz).
        var existingTitles = await db.GroupTrainingUnits
            .Where(u => u.CreatedByUserId == null && u.GroupId == null)
            .Select(u => u.Title)
            .ToListAsync();
        var existing = existingTitles.ToHashSet();

        var order = 0;
        foreach (var seed in Catalog)
        {
            order++;
            if (existing.Contains(seed.Title))
                continue;

            var unit = new GroupTrainingUnit
            {
                Title = seed.Title,
                Description = seed.Description,
                Category = seed.Category,
                CreatedByUserId = null,
                GroupId = null,
                SortOrder = order
            };

            var itemOrder = 0;
            foreach (var item in seed.Items)
            {
                unit.Items.Add(new GroupTrainingUnitItem
                {
                    Title = item.Title,
                    Focus = item.Focus,
                    DurationMinutes = item.DurationMinutes,
                    Description = item.Description,
                    SortOrder = itemOrder++
                });
            }

            db.GroupTrainingUnits.Add(unit);
        }

        await db.SaveChangesAsync();
    }

    private static readonly UnitSeed[] Catalog =
    [
        // ---------------------------------------------------------------- Welpen
        new(GroupTrainingCategory.Puppy,
            "Welpen – Einheit 1: Ankommen & Sozialisierung",
            "Erste Gruppenstunde für Welpen: ruhiges Ankommen, positive Sozialkontakte und ein sanfter Einstieg in Handling und Aufmerksamkeit.",
            [
                new("Begrüßungsrunde & Kennenlernen", "Sozialisierung", 10,
                    "Welpen und Halter kommen an der lockeren Leine an, wahren Abstand und lernen die Umgebung ruhig kennen. Keine wilde Begrüßung - der Trainer steuert Tempo und Distanz."),
                new("Positives Handling", "Handling", 8,
                    "Halter berührt Pfoten, Ohren und Fang, jede Berührung wird mit einem Leckerli verknüpft. Grundlage für Tierarzt und Pflege."),
                new("Sozialkontakt in Kleingruppen", "Sozialisierung", 10,
                    "Zwei bis drei gut zueinander passende Welpen spielen unter Aufsicht. Der Trainer legt bewusst Spielpausen ein, damit die Welpen nicht überdrehen."),
                new("Untergründe erkunden", "Umweltgewöhnung", 8,
                    "Verschiedene Untergründe (Plane, Gitterrost, Wackelbrett) werden freiwillig erkundet - loben, nie schieben oder zwingen."),
                new("Namensspiel & Aufmerksamkeit", "Bindung", 7,
                    "Name sagen, Welpe schaut zum Halter, markern und belohnen. Baut den freiwilligen Blickkontakt auf."),
                new("Ruheübung zum Abschluss", "Entspannung", 7,
                    "Auf einer Decke neben dem Halter zur Ruhe kommen. Die Welpen lernen, in einer Reizumgebung herunterzufahren."),
            ]),

        new(GroupTrainingCategory.Puppy,
            "Welpen – Einheit 2: Erste Signale & Impulskontrolle",
            "Spielerischer Aufbau der ersten Signale (Sitz, Rückruf) und ein erster Baustein Impulskontrolle - alles freudig und kurz.",
            [
                new("Aufmerksamkeit auf den Halter", "Bindung", 8,
                    "Blickkontakt aufbauen: Der Welpe lernt, sich freiwillig am Halter zu orientieren, bevor Signale dazukommen."),
                new("Sitz über Locken", "Grundsignale", 8,
                    "Mit Futter locken, bis der Welpe sich setzt, dann markern und belohnen. Kein Drücken auf die Hinterhand."),
                new("Rückruf-Grundlage im Spiel", "Rückruf", 10,
                    "Auf kurze Distanz freudig herrufen und üppig belohnen. Der Rückruf wird von Anfang an ausschließlich positiv verknüpft."),
                new("Futter in der geschlossenen Hand", "Impulskontrolle", 8,
                    "Der Welpe lernt, an der geschlossenen Hand mit Futter kurz zu warten; Belohnung erst, wenn er zurückweicht statt zu drängeln."),
                new("Leinen-Gewöhnung", "Leinenführigkeit", 8,
                    "Die Leine wird positiv verknüpft; ein paar Schritte gemeinsam gehen, ohne dass Zug auf die Leine kommt."),
                new("Ruhephase & Kauen", "Entspannung", 8,
                    "Mit einem Kauartikel auf der Decke herunterfahren - bewusstes Ende der aktiven Phase."),
            ]),

        new(GroupTrainingCategory.Puppy,
            "Welpen – Einheit 3: Umwelt & Alltag",
            "Umweltsicherheit aufbauen: Geräusche, Menschen und kleine Parcours-Erfahrungen, plus ein sehr kurzes erstes Warten.",
            [
                new("Geräusch-Gewöhnung", "Umweltgewöhnung", 8,
                    "Leise Alltagsgeräusche (Regenschirm, Klappern) aus Abstand präsentieren und positiv verknüpfen. Reizstärke immer unter der Schwelle halten."),
                new("Mini-Parcours spielerisch", "Koordination", 10,
                    "Niedrige Hindernisse und ein Tunnel fördern Körpergefühl und Selbstvertrauen. Alles freiwillig, mit viel Lob."),
                new("Begegnung mit Menschen", "Sozialisierung", 8,
                    "Eine ruhige fremde Person ist anwesend; der Welpe entscheidet selbst über Kontakt. Souveränes Verhalten wird belohnt."),
                new("\"Bleib\" ganz kurz", "Impulskontrolle", 7,
                    "Ein bis zwei Sekunden Warten im Sitz, sofort belohnen und auflösen. Dauer bewusst minimal halten."),
                new("Rückruf mit leichter Ablenkung", "Rückruf", 8,
                    "Herrufen mit einer kleinen Ablenkung in der Umgebung, hochwertige Belohnung. Bei Misserfolg Distanz/Ablenkung verringern."),
                new("Abschluss-Handling", "Handling", 7,
                    "Ruhiges Abtasten von Pfoten und Zähnen als positiver Abschluss der Stunde."),
            ]),

        // ------------------------------------------------------------- Junghunde
        new(GroupTrainingCategory.YoungDog,
            "Junghunde – Einheit 1: Leinenführigkeit & Aufmerksamkeit",
            "Fokus auf lockere Leinenführigkeit, Aufmerksamkeit trotz Ablenkung und einen belastbaren Rückruf auf mittlerer Distanz.",
            [
                new("Aufwärmen & Fokus", "Bindung", 8,
                    "Aufmerksamkeitsspiele und Blickkontakt an der lockeren Leine bringen den Junghund ins Arbeiten."),
                new("Leinenführigkeit an lockerer Leine", "Leinenführigkeit", 12,
                    "Gehen ohne Zug mit Richtungs- und Tempowechseln. Bei Leinenspannung stehenbleiben, Belohnung in der korrekten Position neben dem Halter."),
                new("Sitz & Platz aus der Bewegung", "Grundsignale", 10,
                    "Signale im Gehen geben und sauber ausführen lassen - Übergang von der statischen zur dynamischen Ausführung."),
                new("Impulskontrolle am Wegrand", "Impulskontrolle", 8,
                    "Futter oder Spielzeug liegt am Boden; der Hund bleibt beim Halter statt sich zu bedienen. Distanz zum Reiz nach Können wählen."),
                new("Rückruf mittlere Distanz", "Rückruf", 10,
                    "Distanz aufbauen und freudig abrufen, mit hochwertiger Belohnung. Schleppleine als Absicherung, falls nötig."),
                new("Ruheübung \"Decke\"", "Entspannung", 7,
                    "Ablegen und Entspannen auf der Decke inmitten der Gruppensituation."),
            ]),

        new(GroupTrainingCategory.YoungDog,
            "Junghunde – Einheit 2: Hinterhand & Körpergefühl",
            "Körpergefühl und Hinterhandkontrolle - wichtig für Koordination, Gesundheit und spätere Sportgrundlagen.",
            [
                new("Warm-up in Bewegung", "Koordination", 8,
                    "Slalom durch die Beine des Halters und lockere Bewegung als Aufwärmen."),
                new("Hinterhandkontrolle (Pivot)", "Hinterhandarbeit", 12,
                    "Mit den Vorderpfoten auf einem niedrigen Podest kreist der Hund mit der Hinterhand herum - Bewusstsein für die Hinterläufe."),
                new("Rückwärts gehen", "Hinterhandarbeit", 8,
                    "Wenige, saubere Schritte rückwärts fördern Körpergefühl und Koordination."),
                new("Targeting (Nase/Pfote)", "Koordination", 8,
                    "Der Hund berührt ein Zielobjekt gezielt mit Nase oder Pfote - Präzision und Konzentration."),
                new("Bleib mit Ablenkung", "Impulskontrolle", 10,
                    "Dauer und Distanz steigern, während der Trainer vorbeigeht. Kriterien einzeln erhöhen, nicht alles gleichzeitig."),
                new("Cool-down & Handling", "Handling", 6,
                    "Ruhiges Abtasten und Ausklang - der Hund fährt herunter."),
            ]),

        new(GroupTrainingCategory.YoungDog,
            "Junghunde – Einheit 3: Alltag & Umweltsicherheit",
            "Alltagstauglichkeit: kontrollierte Begegnungen, Neutralität gegenüber Umweltreizen und Rückruf aus dem Spiel.",
            [
                new("Begegnungstraining Hunde", "Sozialisierung", 10,
                    "Kontrollierte Begegnung an der Leine: ruhiges Passieren anderer Hunde, Belohnung für Orientierung zum Halter."),
                new("Umweltreize im Alltag", "Umweltreize", 10,
                    "Fahrräder und Jogger (simuliert) passieren aus passender Distanz; Neutralität aufbauen und belohnen."),
                new("Rückruf aus dem Spiel", "Rückruf", 10,
                    "Aus dem laufenden Spiel abrufen, belohnen und wieder ins Spiel entlassen - die Freigabe ist selbst die Belohnung."),
                new("Leinenführigkeit mit Ablenkung", "Leinenführigkeit", 10,
                    "An Reizen vorbeigehen, ohne dass Zug auf die Leine kommt. Bei Bedarf Abstand zum Reiz vergrößern."),
                new("Warten an der Kante", "Impulskontrolle", 6,
                    "Sitz und Warten vor Bordstein, Tür oder Auto - Sicherheit im Alltag."),
                new("Entspannung im Trubel", "Entspannung", 6,
                    "Ablegen und Herunterfahren, obwohl ringsum noch Aktivität herrscht."),
            ]),
    ];
}
