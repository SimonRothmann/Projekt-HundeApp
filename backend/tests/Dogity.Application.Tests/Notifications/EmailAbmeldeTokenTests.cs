using Dogity.Infrastructure.Notifications;
using Dogity.Application.Tests.TestSupport;
using Microsoft.AspNetCore.DataProtection;

namespace Dogity.Application.Tests.Notifications;

/// <summary>Das Token im Abmelde-Link: Rundweg, und alles andere ist ungültig - ohne Ausnahme.</summary>
public class EmailAbmeldeTokenTests
{
    [Fact]
    public void Rundweg_LiefertDieKontoId()
    {
        var tokens = TestAbmeldeToken.Neu();
        var id = Guid.NewGuid();

        Assert.Equal(id, tokens.Pruefe(tokens.Erzeuge(id)));
    }

    [Fact]
    public void Token_IstUrlTauglich()
    {
        var token = TestAbmeldeToken.Neu().Erzeuge(Guid.NewGuid());

        Assert.Matches("^[A-Za-z0-9_-]+$", token);
        Assert.Equal(token, Uri.EscapeDataString(token));
    }

    [Fact]
    public void Token_VerraetDieKontoIdNichtImKlartext()
    {
        var id = Guid.NewGuid();

        var token = TestAbmeldeToken.Neu().Erzeuge(id);

        Assert.DoesNotContain(id.ToString("N"), token);
        Assert.DoesNotContain(id.ToString(), token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("kein-token")]
    [InlineData("!!!")]
    public void LeerOderUnsinn_IstNull(string? token)
    {
        Assert.Null(TestAbmeldeToken.Neu().Pruefe(token));
    }

    [Fact]
    public void Manipuliert_IstNull()
    {
        var tokens = TestAbmeldeToken.Neu();
        var token = tokens.Erzeuge(Guid.NewGuid());
        var kaputt = token[..^2] + (token[^2] == 'A' ? 'B' : 'A') + token[^1];

        Assert.Null(tokens.Pruefe(kaputt));
        Assert.Null(tokens.Pruefe(token[..(token.Length / 2)]));
    }

    [Fact]
    public void ZuLang_IstNull_OhneEntschluesselungsVersuch()
    {
        Assert.Null(TestAbmeldeToken.Neu().Pruefe(new string('A', 5000)));
    }

    [Fact]
    public void FremdeSchluessel_SindUngueltig()
    {
        var token = TestAbmeldeToken.Neu().Erzeuge(Guid.NewGuid());

        // Eine andere Installation (andere Schlüssel) kann das Token nicht einlösen.
        Assert.Null(TestAbmeldeToken.Neu().Pruefe(token));
    }

    [Fact]
    public void FremderZweck_IstUngueltig()
    {
        var provider = new EphemeralDataProtectionProvider();
        var id = Guid.NewGuid();
        // Gleiche Schlüssel, anderer Zweck - etwa ein anderes Token der App.
        var fremd = provider.CreateProtector("Dogity.AndererZweck.v1").Protect(id.ToString("N"));

        Assert.Null(new EmailAbmeldeToken(provider).Pruefe(fremd));
    }

    [Fact]
    public void GueltigVerschluesselt_AberKeineGuid_IstNull()
    {
        var provider = new EphemeralDataProtectionProvider();
        var protector = provider.CreateProtector(EmailAbmeldeToken.Zweck);
        var tokens = new EmailAbmeldeToken(provider);

        Assert.Null(tokens.Pruefe(protector.Protect("irgendwas")));
        Assert.Null(tokens.Pruefe(protector.Protect(Guid.Empty.ToString("N"))));
    }
}
