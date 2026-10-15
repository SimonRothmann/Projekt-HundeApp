using Dogity.Application.Tests.TestSupport;
using Dogity.Infrastructure.Identity;

namespace Dogity.Application.Tests.Identity;

/// <summary>
/// Die echte Suche der Admin-Nutzerliste (gegen den In-Memory-Provider, der
/// dieselbe LINQ-Übersetzung durchläuft wie Npgsql bis auf das SQL). Der
/// FakeUserLookupService der Service-Tests ahmt genau diese Semantik nach.
/// </summary>
public class NutzerSucheTests
{
    private static async Task<List<string?>> Suche(string? suche, params (string Email, string Vor, string Nach)[] nutzer)
    {
        var db = InMemoryDbContext.Create();
        foreach (var (email, vor, nach) in nutzer)
            db.Users.Add(new ApplicationUser { Id = Guid.NewGuid(), Email = email, UserName = email, FirstName = vor, LastName = nach });
        await db.SaveChangesAsync();

        return NutzerSuche.Filtern(db.Users, suche).OrderBy(u => u.Email).Select(u => u.Email).ToList();
    }

    private static readonly (string, string, string)[] Drei =
    [
        ("anna@test.de", "Anna", "Schmidt"),
        ("bert@beispiel.de", "Bert", "Müller"),
        ("carla@test.de", "Carla", "Annabauer"),
    ];

    [Theory]
    [InlineData("BEISPIEL", "bert@beispiel.de")]
    [InlineData("bERT", "bert@beispiel.de")]
    [InlineData("schmidt", "anna@test.de")]
    [InlineData("Anna Schmidt", "anna@test.de")]
    [InlineData("  müller  ", "bert@beispiel.de")]
    public async Task Trifft_EmailVornameNachnameUndBeideNamen(string suche, string erwartet)
    {
        Assert.Equal([erwartet], await Suche(suche, Drei));
    }

    [Fact]
    public async Task Teilstueck_TrifftMehrere()
    {
        Assert.Equal(["anna@test.de", "carla@test.de"], await Suche("anna", Drei));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task LeereSuche_LiefertAlle(string? suche)
    {
        Assert.Equal(3, (await Suche(suche, Drei)).Count);
    }

    [Fact]
    public async Task Guid_FindetGenauDiesesKonto()
    {
        var db = InMemoryDbContext.Create();
        var gesucht = new ApplicationUser { Id = Guid.NewGuid(), Email = "a@test.de", UserName = "a", FirstName = "A", LastName = "A" };
        db.Users.Add(gesucht);
        db.Users.Add(new ApplicationUser { Id = Guid.NewGuid(), Email = "b@test.de", UserName = "b", FirstName = "B", LastName = "B" });
        await db.SaveChangesAsync();

        var treffer = NutzerSuche.Filtern(db.Users, $" {gesucht.Id.ToString().ToUpperInvariant()} ").ToList();

        Assert.Equal([gesucht.Id], treffer.Select(u => u.Id));
    }

    [Fact]
    public async Task Platzhalterzeichen_SindWoertlich()
    {
        // "%" und "_" dürfen nicht als Joker wirken: Gefunden wird nur, wer das
        // Zeichen wirklich im Namen oder in der Adresse trägt.
        (string, string, string)[] mitZeichen =
        [
            ("max_mustermann@test.de", "Max", "Mustermann"),
            ("maxim@test.de", "Maxim", "Muster"),
            ("prozent@test.de", "100%", "Hund"),
        ];

        Assert.Equal(["max_mustermann@test.de"], await Suche("max_", mitZeichen));
        Assert.Equal(["prozent@test.de"], await Suche("%", mitZeichen));
        Assert.Empty(await Suche("m%r", mitZeichen));
    }
}
