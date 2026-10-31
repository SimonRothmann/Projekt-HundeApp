using Dogity.Application.Account;
using Dogity.Application.Common;

namespace Dogity.Application.Tests.Account;

/// <summary>
/// Vor- und Nachname: eine Regel für Registrierung und Profil (vorher prüfte die
/// Registrierung nur die Länge, ein Name aus Leerzeichen kam durch).
/// </summary>
public class PersonennamenTests
{
    [Theory]
    [InlineData("Anna", "Muster")]
    [InlineData("  Anna  ", "  Muster  ")]
    public void GueltigeNamen_Gehen_Durch(string vorname, string nachname)
    {
        Assert.Null(Personennamen.Pruefe(vorname, nachname));
    }

    [Theory]
    [InlineData(null, "Muster")]
    [InlineData("Anna", null)]
    [InlineData("", "Muster")]
    [InlineData("Anna", "   ")]
    [InlineData("\t", "\n")]
    public void FehlendeOderLeereNamen_SindPflicht(string? vorname, string? nachname)
    {
        Assert.Equal("Vor- und Nachname sind erforderlich.", Personennamen.Pruefe(vorname, nachname));
    }

    [Fact]
    public void DieLaengeZaehltOhneRandLeerzeichen_BisZurGrenze()
    {
        var genau = new string('a', Textlaengen.PersonenName);
        var zuLang = new string('a', Textlaengen.PersonenName + 1);

        Assert.Null(Personennamen.Pruefe(genau, " " + genau + " "));
        Assert.Equal("Vor- und Nachname dürfen höchstens 100 Zeichen lang sein.", Personennamen.Pruefe(zuLang, "Muster"));
        Assert.NotNull(Personennamen.Pruefe("Anna", zuLang));
    }
}
