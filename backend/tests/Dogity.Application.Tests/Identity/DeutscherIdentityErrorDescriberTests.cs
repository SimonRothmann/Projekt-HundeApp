using Dogity.Infrastructure;
using Dogity.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Application.Tests.Identity;

/// <summary>
/// Der Describer ersetzt nur Texte. Geprüft wird, dass jede Meldung deutsch ist,
/// den Identity-Code behält (daran hängt die Fehlerauswertung) und dass die
/// Passwortsätze zu den Regeln aus AddIdentity passen (8 Zeichen, Groß- und
/// Kleinbuchstabe, Ziffer).
/// </summary>
public class DeutscherIdentityErrorDescriberTests
{
    private readonly DeutscherIdentityErrorDescriber _describer = new();

    [Fact]
    public void PasswordTooShort_NenntDieMindestlaenge()
    {
        var fehler = _describer.PasswordTooShort(8);

        Assert.Equal("PasswordTooShort", fehler.Code);
        Assert.Equal("Das Passwort muss mindestens 8 Zeichen lang sein.", fehler.Description);
    }

    [Fact]
    public void PasswortRegeln_SindDeutschUndBehaltenIhrenCode()
    {
        Assert.Equal("PasswordRequiresDigit", _describer.PasswordRequiresDigit().Code);
        Assert.Contains("Ziffer", _describer.PasswordRequiresDigit().Description);
        Assert.Equal("PasswordRequiresUpper", _describer.PasswordRequiresUpper().Code);
        Assert.Contains("Großbuchstaben", _describer.PasswordRequiresUpper().Description);
        Assert.Equal("PasswordRequiresLower", _describer.PasswordRequiresLower().Code);
        Assert.Contains("Kleinbuchstaben", _describer.PasswordRequiresLower().Description);
        Assert.Equal("PasswordRequiresNonAlphanumeric", _describer.PasswordRequiresNonAlphanumeric().Code);
        Assert.Contains("Sonderzeichen", _describer.PasswordRequiresNonAlphanumeric().Description);
        Assert.Equal("PasswordRequiresUniqueChars", _describer.PasswordRequiresUniqueChars(3).Code);
        Assert.Contains("3 verschiedene Zeichen", _describer.PasswordRequiresUniqueChars(3).Description);
    }

    [Fact]
    public void DuplicateEmail_SagtDasselbeWieDieRegistrierung()
    {
        // AuthController.Register antwortet bei bekannter Adresse mit genau
        // diesem Satz; der Describer darf nichts Aufschlussreicheres sagen.
        Assert.Equal("E-Mail wird bereits verwendet.", _describer.DuplicateEmail("a@b.de").Description);
        Assert.Equal("E-Mail wird bereits verwendet.", _describer.DuplicateUserName("a@b.de").Description);
        Assert.Equal("DuplicateEmail", _describer.DuplicateEmail("a@b.de").Code);
        Assert.Equal("DuplicateUserName", _describer.DuplicateUserName("a@b.de").Code);
    }

    [Fact]
    public void InvalidEmail_NenntDieAdresse()
    {
        var fehler = _describer.InvalidEmail("kaputt");

        Assert.Equal("InvalidEmail", fehler.Code);
        Assert.Contains("kaputt", fehler.Description);
        Assert.Contains("ungültig", fehler.Description);
    }

    [Fact]
    public void PasswordMismatch_PasstZumTextDerKontoseite()
    {
        // ProfileController.ChangePassword prüft das alte Passwort vorab mit
        // demselben Satz.
        var fehler = _describer.PasswordMismatch();

        Assert.Equal("PasswordMismatch", fehler.Code);
        Assert.Equal("Aktuelles Passwort ist falsch.", fehler.Description);
    }

    [Fact]
    public void InvalidToken_SagtDasselbeWieDasZuruecksetzen()
    {
        Assert.Equal("Link ist ungültig oder abgelaufen.", _describer.InvalidToken().Description);
    }

    [Fact]
    public void KeineMeldungIstMehrEnglisch()
    {
        // Gegenprobe zum Basis-Describer: jede überschriebene Meldung weicht
        // vom englischen Original ab.
        var englisch = new IdentityErrorDescriber();
        var paare = new (IdentityError Deutsch, IdentityError Original)[]
        {
            (_describer.DefaultError(), englisch.DefaultError()),
            (_describer.ConcurrencyFailure(), englisch.ConcurrencyFailure()),
            (_describer.PasswordMismatch(), englisch.PasswordMismatch()),
            (_describer.InvalidToken(), englisch.InvalidToken()),
            (_describer.InvalidUserName("x"), englisch.InvalidUserName("x")),
            (_describer.InvalidEmail("x"), englisch.InvalidEmail("x")),
            (_describer.DuplicateUserName("x"), englisch.DuplicateUserName("x")),
            (_describer.DuplicateEmail("x"), englisch.DuplicateEmail("x")),
            (_describer.PasswordTooShort(8), englisch.PasswordTooShort(8)),
            (_describer.PasswordRequiresNonAlphanumeric(), englisch.PasswordRequiresNonAlphanumeric()),
            (_describer.PasswordRequiresDigit(), englisch.PasswordRequiresDigit()),
            (_describer.PasswordRequiresLower(), englisch.PasswordRequiresLower()),
            (_describer.PasswordRequiresUpper(), englisch.PasswordRequiresUpper()),
            (_describer.PasswordRequiresUniqueChars(2), englisch.PasswordRequiresUniqueChars(2)),
        };

        foreach (var (deutsch, original) in paare)
        {
            Assert.Equal(original.Code, deutsch.Code);
            Assert.NotEqual(original.Description, deutsch.Description);
        }
    }

    [Fact]
    public void AddInfrastructure_RegistriertDenDeutschenDescriber()
    {
        // Ohne AddErrorDescriber in AddIdentity blieben alle Meldungen
        // englisch, ohne dass einer der Tests oben es merkt.
        var konfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Database=test",
                ["Jwt:Secret"] = new string('x', 48),
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(konfiguration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<DeutscherIdentityErrorDescriber>(
            scope.ServiceProvider.GetRequiredService<IdentityErrorDescriber>());
    }
}
