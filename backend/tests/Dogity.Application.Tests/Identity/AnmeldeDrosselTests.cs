using Dogity.Api.Hosting;

namespace Dogity.Application.Tests.Identity;

/// <summary>
/// Die Grenze je E-Mail-Adresse beim Anmelden (zusätzlich zur weiten Grenze je IP): Ein einzelnes Konto
/// bleibt geschützt, auch wenn viele hinter einer Adresse (Vereins-WLAN) sich anmelden.
/// </summary>
public class AnmeldeDrosselTests
{
    [Fact]
    public void ZehnVersucheJeMinute_DerElfteWirdAbgelehnt()
    {
        using var drossel = new AnmeldeDrossel();

        for (var i = 0; i < AnmeldeDrossel.VersucheJeMinute; i++)
            Assert.True(drossel.Darf("lena@example.org"));

        Assert.False(drossel.Darf("lena@example.org"));
    }

    [Fact]
    public void JedeAdresseHatIhrenEigenenTopf_VieleKontenHinterEinerIpBleibenFrei()
    {
        using var drossel = new AnmeldeDrossel(grenze: 2);
        Assert.True(drossel.Darf("a@example.org"));
        Assert.True(drossel.Darf("a@example.org"));
        Assert.False(drossel.Darf("a@example.org"));

        // Dutzende andere Konten im selben WLAN sind davon nicht betroffen.
        for (var i = 0; i < 50; i++)
            Assert.True(drossel.Darf($"mitglied{i}@example.org"));
    }

    [Theory]
    [InlineData("LENA@EXAMPLE.ORG")]
    [InlineData("  lena@example.org ")]
    public void SchreibweiseUndLeerzeichenUmgehenDieGrenzeNicht(string andereSchreibweise)
    {
        using var drossel = new AnmeldeDrossel(grenze: 1);
        Assert.True(drossel.Darf("lena@example.org"));

        Assert.False(drossel.Darf(andereSchreibweise));
    }

    [Fact]
    public void FehlendeAdresse_WirdAuchGezaehlt_UndWirftNicht()
    {
        using var drossel = new AnmeldeDrossel(grenze: 1);

        Assert.True(drossel.Darf(null));
        Assert.False(drossel.Darf(""));
    }

    [Fact]
    public async Task NachAblaufDesFenstersIstDieAdresseWiederFrei()
    {
        using var drossel = new AnmeldeDrossel(grenze: 1, fenster: TimeSpan.FromMilliseconds(200));
        Assert.True(drossel.Darf("lena@example.org"));
        Assert.False(drossel.Darf("lena@example.org"));

        await Task.Delay(2500);

        Assert.True(drossel.Darf("lena@example.org"));
    }
}
