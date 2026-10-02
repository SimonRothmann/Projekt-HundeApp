using Dogity.Application.Community;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Das öffentliche Anmeldeformular: Es ist ohne Konto erreichbar und darf
/// deshalb nichts verraten, nichts Doppeltes anlegen und sich nicht
/// vollschreiben lassen.
/// </summary>
public class GroupRegistrationFormServiceTests
{
    [Fact]
    public async Task Vorschau_LiefertVereinsUndGruppenname()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);

        var ergebnis = await a.Formular.GetFormAsync(a.Gruppe.RegistrationCode!);

        Assert.True(ergebnis.Succeeded);
        Assert.Equal("Hundesportverein Nord", ergebnis.Value!.ClubName);
        Assert.Equal("Welpengruppe", ergebnis.Value.GroupName);
    }

    [Fact]
    public async Task Vorschau_GruppeOhneVerein_HatKeinenVereinsnamen()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);
        a.Gruppe.ClubId = null;
        await a.Db.SaveChangesAsync();

        var ergebnis = await a.Formular.GetFormAsync(a.Gruppe.RegistrationCode!);

        Assert.True(ergebnis.Succeeded);
        Assert.Null(ergebnis.Value!.ClubName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("zu-kurz")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA")] // richtige Form, aber kein Code einer Gruppe
    public async Task Vorschau_UnbekannterCode_Ist404(string? code)
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);

        var ergebnis = await a.Formular.GetFormAsync(code!);

        Assert.False(ergebnis.Succeeded);
        Assert.True(ergebnis.IsNotFound);
    }

    [Fact]
    public async Task GeschlossenerCode_UndUnbekannterCode_AntwortenGleich()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);
        var code = a.Gruppe.RegistrationCode!;
        a.Gruppe.RegistrationCode = null;
        await a.Db.SaveChangesAsync();

        var geschlossen = await a.Formular.GetFormAsync(code);
        var unbekannt = await a.Formular.GetFormAsync("AAAAAAAAAAAAAAAAAAAAAA");

        Assert.True(geschlossen.IsNotFound);
        Assert.Equal(unbekannt.Errors, geschlossen.Errors);
    }

    [Fact]
    public async Task GeloeschteGruppe_Ist404()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);
        a.Gruppe.DeletedAt = DateTimeOffset.UtcNow;
        await a.Db.SaveChangesAsync();

        Assert.True((await a.Formular.GetFormAsync(a.Gruppe.RegistrationCode!)).IsNotFound);
        Assert.True((await a.Formular.SubmitAsync(a.Gruppe.RegistrationCode!, AnmeldeAufbau.Formulareingabe())).IsNotFound);
    }

    [Fact]
    public async Task Anmelden_LegtZeileAn_UndBenachrichtigtDieTrainerinnenDerGruppe()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);

        var ergebnis = await a.Formular.SubmitAsync(a.Gruppe.RegistrationCode!, AnmeldeAufbau.Formulareingabe(rufname: "  Bella ", rasse: "Labrador"));

        Assert.True(ergebnis.Succeeded);
        var zeile = await a.Db.GroupRegistrations.SingleAsync();
        Assert.Equal(GroupRegistrationSource.Form, zeile.Source);
        Assert.Equal("Bella", zeile.DogName);
        Assert.Equal(a.Gruppe.Id, zeile.GroupId);
        Assert.Null(zeile.PaidAt);

        // Leitung und Co-Trainerin, nicht die Vereins-Trainerin und nicht die Fremde.
        Assert.Equal(
            new[] { a.Leitung, a.CoTrainerin }.OrderBy(x => x),
            a.Meldungen.Created.Select(m => m.UserId).OrderBy(x => x));
        Assert.All(a.Meldungen.Created, m =>
        {
            Assert.Equal("Neue Anmeldung für Welpengruppe: Bella (Labrador).", m.Message);
            Assert.Equal($"/trainer/{a.Gruppe.Id}?ansicht=anmeldungen", m.LinkPath);
        });
    }

    [Fact]
    public async Task Anmelden_EingeladeneCoTrainerin_WirdNichtBenachrichtigt()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);
        var eingeladen = Guid.NewGuid();
        a.Db.GroupTrainers.Add(new GroupTrainer { GroupId = a.Gruppe.Id, UserId = eingeladen, Status = GroupTrainerStatus.Invited });
        await a.Db.SaveChangesAsync();

        await a.Formular.SubmitAsync(a.Gruppe.RegistrationCode!, AnmeldeAufbau.Formulareingabe());

        Assert.DoesNotContain(a.Meldungen.Created, m => m.UserId == eingeladen);
    }

    [Fact]
    public async Task Anmelden_Koederfeld_GefuelltTutSoAlsOb_UndSpeichertNichts()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);

        var ergebnis = await a.Formular.SubmitAsync(a.Gruppe.RegistrationCode!, AnmeldeAufbau.Formulareingabe(koeder: "http://spam.example"));

        Assert.True(ergebnis.Succeeded);
        Assert.Empty(a.Db.GroupRegistrations);
        Assert.Empty(a.Meldungen.Created);
    }

    [Fact]
    public async Task Anmelden_OhneEinwilligung_WirdAbgewiesen()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);

        var ergebnis = await a.Formular.SubmitAsync(a.Gruppe.RegistrationCode!, AnmeldeAufbau.Formulareingabe(einwilligung: false));

        Assert.False(ergebnis.Succeeded);
        Assert.False(ergebnis.IsNotFound);
        Assert.Empty(a.Db.GroupRegistrations);
    }

    [Fact]
    public async Task Anmelden_Pflichtfelder_SindPflicht()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);
        var code = a.Gruppe.RegistrationCode!;

        Assert.False((await a.Formular.SubmitAsync(code, AnmeldeAufbau.Formulareingabe(vorname: " "))).Succeeded);
        Assert.False((await a.Formular.SubmitAsync(code, AnmeldeAufbau.Formulareingabe(nachname: ""))).Succeeded);
        Assert.False((await a.Formular.SubmitAsync(code, AnmeldeAufbau.Formulareingabe(rufname: ""))).Succeeded);
        Assert.False((await a.Formular.SubmitAsync(code, AnmeldeAufbau.Formulareingabe(rasse: ""))).Succeeded);
        Assert.False((await a.Formular.SubmitAsync(code, AnmeldeAufbau.Formulareingabe(telefon: ""))).Succeeded);
        var ohneWurftag = new PublicRegistrationRequest("Anna", "Muster", "Bella", "Labrador", null, "0721 123456", true, null);
        Assert.False((await a.Formular.SubmitAsync(code, ohneWurftag)).Succeeded);
        Assert.Empty(a.Db.GroupRegistrations);
    }

    [Fact]
    public async Task Anmelden_LaengenGrenzen_GeltenServerseitig()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);
        var code = a.Gruppe.RegistrationCode!;
        var zuLang = new string('x', 101);

        var vorname = await a.Formular.SubmitAsync(code, AnmeldeAufbau.Formulareingabe(vorname: zuLang));
        var rasse = await a.Formular.SubmitAsync(code, AnmeldeAufbau.Formulareingabe(rasse: zuLang));
        var telefon = await a.Formular.SubmitAsync(code, AnmeldeAufbau.Formulareingabe(telefon: new string('1', 31)));

        Assert.False(vorname.Succeeded);
        Assert.Contains("zu lang", vorname.Errors[0]);
        Assert.False(rasse.Succeeded);
        Assert.False(telefon.Succeeded);
        Assert.Empty(a.Db.GroupRegistrations);

        // Genau an der Grenze geht es.
        var amLimit = new string('x', 100);
        Assert.True((await a.Formular.SubmitAsync(code, AnmeldeAufbau.Formulareingabe(vorname: amLimit))).Succeeded);
    }

    [Theory]
    [InlineData("0721 123456", true)]
    [InlineData("+49 (721) 12 34-56", true)]
    [InlineData("0721/123456", true)]
    [InlineData("12345", false)]          // zu kurz
    [InlineData("abc 123456", false)]     // Buchstaben
    [InlineData("0721 123456 ext", false)]
    [InlineData("( ) - / +", false)]      // genug Zeichen, aber keine Ziffern
    public async Task Anmelden_Telefonformat(string telefon, bool erlaubt)
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);

        var ergebnis = await a.Formular.SubmitAsync(a.Gruppe.RegistrationCode!, AnmeldeAufbau.Formulareingabe(telefon: telefon));

        Assert.Equal(erlaubt, ergebnis.Succeeded);
    }

    [Fact]
    public async Task Anmelden_Wurftag_NichtInDerZukunft_UndNichtAelterAlsZweiJahre()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);
        var code = a.Gruppe.RegistrationCode!;
        var heute = Vereinszeit.Heute();

        var zukunft = await a.Formular.SubmitAsync(code, AnmeldeAufbau.Formulareingabe(wurftag: heute.AddDays(1)));
        var zuAlt = await a.Formular.SubmitAsync(code, AnmeldeAufbau.Formulareingabe(wurftag: heute.AddYears(-2).AddDays(-1)));
        var heuteGeboren = await a.Formular.SubmitAsync(code, AnmeldeAufbau.Formulareingabe(rufname: "Neu", wurftag: heute));
        var genauZweiJahre = await a.Formular.SubmitAsync(code, AnmeldeAufbau.Formulareingabe(rufname: "Alt", wurftag: heute.AddYears(-2)));

        Assert.False(zukunft.Succeeded);
        Assert.False(zuAlt.Succeeded);
        Assert.True(heuteGeboren.Succeeded);
        Assert.True(genauZweiJahre.Succeeded);
    }

    [Fact]
    public async Task Anmelden_Doppelt_LegtNichtsNeuesAn_AntwortetAberErfolgreich()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);
        var code = a.Gruppe.RegistrationCode!;
        await a.Formular.SubmitAsync(code, AnmeldeAufbau.Formulareingabe(telefon: "0721 123456"));
        a.Meldungen.Created.Clear();

        // Dieselbe Nummer in anderer Schreibweise, Rufname in anderer Groß-/Kleinschreibung.
        var nochmal = await a.Formular.SubmitAsync(code, AnmeldeAufbau.Formulareingabe(telefon: "+49 721 123456", rufname: "BELLA"));

        Assert.True(nochmal.Succeeded);
        Assert.Single(a.Db.GroupRegistrations);
        // Und keine zweite Benachrichtigung für dieselbe Anmeldung.
        Assert.Empty(a.Meldungen.Created);
    }

    [Fact]
    public async Task Anmelden_AndererHundMitGleicherNummer_IstKeinDuplikat()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);
        var code = a.Gruppe.RegistrationCode!;
        await a.Formular.SubmitAsync(code, AnmeldeAufbau.Formulareingabe(rufname: "Bella"));

        await a.Formular.SubmitAsync(code, AnmeldeAufbau.Formulareingabe(rufname: "Luna"));

        // Geschwister aus demselben Haushalt sind zwei Anmeldungen.
        Assert.Equal(2, await a.Db.GroupRegistrations.CountAsync());
    }

    [Fact]
    public async Task Anmelden_Duplikat_NachDreissigTagenZaehltAlsNeueAnmeldung()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);
        await a.AnmeldungAsync("Bella", "0721 123456", angemeldet: DateTimeOffset.UtcNow.AddDays(-31));

        var ergebnis = await a.Formular.SubmitAsync(a.Gruppe.RegistrationCode!, AnmeldeAufbau.Formulareingabe());

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(2, await a.Db.GroupRegistrations.CountAsync());
    }

    [Fact]
    public async Task Anmelden_DieDuplikatPruefungGiltJeGruppe()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);
        var andere = new Group { ClubId = a.Verein.Id, TrainerId = a.Leitung, Name = "Junghunde", RegistrationCode = ClubInviteCode.Erzeugen() };
        a.Db.Groups.Add(andere);
        await a.Db.SaveChangesAsync();
        await a.Formular.SubmitAsync(a.Gruppe.RegistrationCode!, AnmeldeAufbau.Formulareingabe());

        await a.Formular.SubmitAsync(andere.RegistrationCode!, AnmeldeAufbau.Formulareingabe());

        Assert.Equal(2, await a.Db.GroupRegistrations.CountAsync());
    }

    [Fact]
    public async Task Anmelden_ObergrenzeJeGruppe_SchuetztVorFluten()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);
        for (var i = 0; i < GroupRegistrationRules.MaxProGruppe; i++)
            a.Db.GroupRegistrations.Add(new GroupRegistration
            {
                GroupId = a.Gruppe.Id, FirstName = "A", LastName = "B", DogName = $"Hund{i}", DogBreed = "Mix",
                DogBirthDate = Vereinszeit.Heute().AddMonths(-2), Phone = $"0721 {100000 + i}",
            });
        await a.Db.SaveChangesAsync();

        var ergebnis = await a.Formular.SubmitAsync(a.Gruppe.RegistrationCode!, AnmeldeAufbau.Formulareingabe(rufname: "Zuviel", telefon: "0160 999999"));

        Assert.False(ergebnis.Succeeded);
        Assert.False(ergebnis.IsNotFound);
        Assert.Equal(GroupRegistrationRules.MaxProGruppe, await a.Db.GroupRegistrations.CountAsync());
    }

    [Fact]
    public async Task Anmelden_BereitsGeloeschteAnmeldungen_ZaehlenNichtMehrZurObergrenze()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);
        var weg = await a.AnmeldungAsync("Weg", "0160 111111");
        weg.DeletedAt = DateTimeOffset.UtcNow;
        await a.Db.SaveChangesAsync();

        Assert.True((await a.Formular.SubmitAsync(a.Gruppe.RegistrationCode!, AnmeldeAufbau.Formulareingabe(rufname: "Weg", telefon: "0160 111111"))).Succeeded);
    }
}
