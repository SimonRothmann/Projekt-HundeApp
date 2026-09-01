using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dogity.Application.Abstractions;
using Dogity.Domain.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Infrastructure.Persistence.Seed;

/// <summary>
/// Legt die Fragenkataloge zur Sachkundeprüfung an (Data/sachkunde-swhv.json).
///
/// Herkunft: die Kataloge zur BH/VT-Sachkundeprüfung des Südwestdeutschen
/// Hundesportverbands e.V. (swhv), Fassungen für Erwachsene und für Jugend,
/// vom swhv öffentlich zum Download bereitgestellt. Die Übernahme in die App
/// erfolgt auf ausdrückliche Entscheidung des Auftraggebers (2026-09-01:
/// "die sind öffentlich und damit zugänglich, jedes Mitglied der App darf die
/// nutzen") - vergleichbar mit der Freigabe der FCI-Prüfungsordnung im
/// <see cref="SportCatalogSeeder"/>. Herausgeber, Quelle und Stand stehen am
/// Katalog und werden in der Oberfläche genannt.
///
/// Die JSON-Datei wird nicht von Hand gepflegt, sondern von
/// scripts/import-sachkunde.py aus den Original-PDFs erzeugt. Erscheint eine
/// neue Fassung: Skript erneut laufen lassen, "stand" hochsetzen, fertig -
/// dieser Seeder gleicht anhand der Fragennummer ab.
///
/// Idempotent und in beide Richtungen: was im Katalog fehlt, wird weich
/// entfernt; was wiederkommt, wird wiederbelebt statt doppelt angelegt (der
/// eindeutige Index auf (CatalogId, Number) kennt DeletedAt nicht).
/// Der Lernstand der Nutzer hängt an der Frage-Id und überlebt jeden Reimport,
/// solange die Fragennummer gleich bleibt.
/// </summary>
public static class SachkundeSeeder
{
    private const string ResourceName = "Dogity.Infrastructure.Persistence.Seed.Data.sachkunde-swhv.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();

        var datei = Lesen();

        foreach (var (katalogDaten, index) in datei.Kataloge.Select((k, i) => (k, i)))
        {
            var katalog = await SeedKatalogAsync(db, datei, katalogDaten, index, ct);
            await SeedFragenAsync(db, katalog, katalogDaten, datei.Komplexe, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private static SeedDatei Lesen()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Eingebettete Ressource {ResourceName} fehlt. Erzeugen mit scripts/import-sachkunde.py.");

        return JsonSerializer.Deserialize<SeedDatei>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"{ResourceName} ist leer oder unlesbar.");
    }

    private static async Task<QuizCatalog> SeedKatalogAsync(
        ApplicationDbContext db, SeedDatei datei, SeedKatalog daten, int index, CancellationToken ct)
    {
        var (vorhanden, _) = await db.QuizCatalogs.FindIncludingRemovedAsync(c => c.Code == daten.Code, ct);

        var katalog = vorhanden;
        if (katalog is null)
        {
            katalog = new QuizCatalog { Code = daten.Code };
            db.QuizCatalogs.Add(katalog);
        }

        katalog.DeletedAt = null;
        katalog.Name = daten.Name;
        katalog.Description = daten.Beschreibung;
        katalog.Publisher = datei.Herausgeber;
        katalog.SourceUrl = datei.Quelle;
        katalog.Edition = datei.Stand;
        katalog.Audience = Enum.Parse<QuizAudience>(daten.Zielgruppe);
        katalog.SortOrder = index + 1;
        if (vorhanden is not null) katalog.UpdatedAt = DateTimeOffset.UtcNow;

        // Die Frage-Zuordnung unten braucht die Katalog-Id.
        await db.SaveChangesAsync(ct);
        return katalog;
    }

    private static async Task SeedFragenAsync(
        ApplicationDbContext db, QuizCatalog katalog, SeedKatalog daten,
        Dictionary<string, string> komplexe, CancellationToken ct)
    {
        // Bewusst mit IgnoreQueryFilters: auch die weich entfernten Fragen
        // müssen sichtbar sein, sonst legt der Seeder eine zweite Zeile mit
        // derselben Nummer an und der eindeutige Index weist sie zurück.
        var bestand = await db.QuizQuestions
            .IgnoreQueryFilters()
            .Include(q => q.Options)
            .Where(q => q.CatalogId == katalog.Id)
            .ToListAsync(ct);

        var nachNummer = bestand.ToDictionary(q => q.Number);
        var imKatalog = new HashSet<string>();

        foreach (var fragenDaten in daten.Fragen)
        {
            imKatalog.Add(fragenDaten.Nummer);

            if (!nachNummer.TryGetValue(fragenDaten.Nummer, out var frage))
            {
                frage = new QuizQuestion { CatalogId = katalog.Id, Number = fragenDaten.Nummer };
                db.QuizQuestions.Add(frage);
                nachNummer[fragenDaten.Nummer] = frage;
            }
            else
            {
                frage.UpdatedAt = DateTimeOffset.UtcNow;
            }

            frage.DeletedAt = null;
            frage.Section = fragenDaten.Komplex;
            frage.SectionName = komplexe.GetValueOrDefault(fragenDaten.Komplex, fragenDaten.Komplex);
            frage.SortOrder = fragenDaten.Reihenfolge;
            frage.Text = fragenDaten.Text;
            frage.Kind = Enum.Parse<QuizQuestionKind>(fragenDaten.Art);
            frage.SampleSolution = fragenDaten.Musterloesung;
            frage.ImageName = fragenDaten.Bild;

            SeedAntworten(db, frage, fragenDaten);
        }

        // Was der Herausgeber gestrichen hat, verschwindet aus der App - der
        // Lernstand dazu bleibt in der Datenbank, wird aber nicht mehr
        // abgefragt (Query-Filter über die Frage).
        foreach (var frage in bestand.Where(q => !imKatalog.Contains(q.Number) && q.DeletedAt is null))
        {
            frage.DeletedAt = DateTimeOffset.UtcNow;
            foreach (var option in frage.Options.Where(o => o.DeletedAt is null))
                option.DeletedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>Reihenfolge-Versatz, damit Begriffe und Beschriftungen einer
    /// Zuordnung nicht mit den Antwortzeilen kollidieren - die Reihenfolge ist
    /// der Abgleichschlüssel innerhalb einer Frage.</summary>
    private const int TermOffset = 100;
    private const int LabelOffset = 200;

    /// <summary>
    /// Gleicht die Zeilen unter der Frage über die Reihenfolge ab und ändert
    /// sie an Ort und Stelle: Antwortmöglichkeiten bei Auswahlfragen, Begriffe
    /// und Beschriftungen bei Zuordnungen. Löschen-und-neu-Anlegen wäre
    /// einfacher, würde aber bei jedem Reimport neue Ids erzeugen - unnötige
    /// Schreiblast und unbrauchbare Verläufe.
    /// </summary>
    private static void SeedAntworten(ApplicationDbContext db, QuizQuestion frage, SeedFrage daten)
    {
        var bestand = frage.Options.OrderBy(o => o.SortOrder).ToList();
        var behalten = new HashSet<int>();

        QuizOption Zeile(int reihenfolge, QuizOptionKind art, string text, bool richtig, string? schluessel)
        {
            var option = bestand.FirstOrDefault(o => o.SortOrder == reihenfolge);
            if (option is null)
            {
                option = new QuizOption { Question = frage, SortOrder = reihenfolge };
                frage.Options.Add(option);
                db.QuizOptions.Add(option);
            }
            else
            {
                option.UpdatedAt = DateTimeOffset.UtcNow;
            }

            option.DeletedAt = null;
            option.Kind = art;
            option.Text = text;
            option.IsCorrect = richtig;
            option.MatchKey = schluessel;
            behalten.Add(reihenfolge);
            return option;
        }

        foreach (var antwort in daten.Antworten)
            Zeile(antwort.Reihenfolge, QuizOptionKind.Answer, antwort.Text, antwort.Richtig, null);

        if (daten.Zuordnung is { } zuordnung)
        {
            foreach (var begriff in zuordnung.Begriffe)
                Zeile(TermOffset + begriff.Reihenfolge, QuizOptionKind.Term, begriff.Text, false, begriff.Schluessel);

            foreach (var beschriftung in zuordnung.Schluessel)
                Zeile(LabelOffset + beschriftung.Reihenfolge, QuizOptionKind.Label,
                      beschriftung.Text, false, beschriftung.Schluessel);
        }

        foreach (var ueberzaehlig in bestand.Where(o => !behalten.Contains(o.SortOrder) && o.DeletedAt is null))
            ueberzaehlig.DeletedAt = DateTimeOffset.UtcNow;
    }

    // ---- Abbild der JSON-Datei ----

    private sealed record SeedDatei(
        [property: JsonPropertyName("herausgeber")] string Herausgeber,
        [property: JsonPropertyName("quelle")] string Quelle,
        [property: JsonPropertyName("stand")] string Stand,
        [property: JsonPropertyName("komplexe")] Dictionary<string, string> Komplexe,
        [property: JsonPropertyName("kataloge")] List<SeedKatalog> Kataloge);

    private sealed record SeedKatalog(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("zielgruppe")] string Zielgruppe,
        [property: JsonPropertyName("beschreibung")] string Beschreibung,
        [property: JsonPropertyName("fragen")] List<SeedFrage> Fragen);

    private sealed record SeedFrage(
        [property: JsonPropertyName("nummer")] string Nummer,
        [property: JsonPropertyName("komplex")] string Komplex,
        [property: JsonPropertyName("reihenfolge")] int Reihenfolge,
        [property: JsonPropertyName("art")] string Art,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("bild")] string? Bild,
        [property: JsonPropertyName("musterloesung")] string? Musterloesung,
        [property: JsonPropertyName("antworten")] List<SeedAntwort> Antworten,
        [property: JsonPropertyName("zuordnung")] SeedZuordnung? Zuordnung);

    private sealed record SeedZuordnung(
        [property: JsonPropertyName("begriffe")] List<SeedBegriff> Begriffe,
        [property: JsonPropertyName("schluessel")] List<SeedSchluessel> Schluessel);

    private sealed record SeedBegriff(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("schluessel")] string Schluessel,
        [property: JsonPropertyName("reihenfolge")] int Reihenfolge);

    private sealed record SeedSchluessel(
        [property: JsonPropertyName("schluessel")] string Schluessel,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("reihenfolge")] int Reihenfolge);

    private sealed record SeedAntwort(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("richtig")] bool Richtig,
        [property: JsonPropertyName("reihenfolge")] int Reihenfolge);
}
