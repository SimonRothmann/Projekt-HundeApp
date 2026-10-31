using Dogity.Application.Preferences;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Geography;
using Dogity.Domain.Sports;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Preferences;

/// <summary>
/// Die Sportarten-Einstellungen: welche Sportarten eine Person trainiert und
/// welche ihr Hund (eigene oder geerbte), dazu das Land. Zwei Regeln sind
/// sicherheitsrelevant und darum nicht dem Zufall überlassen: Über geratene
/// Ids dürfen keine vereinseigenen Sportarten fremder Vereine sichtbar werden,
/// und die Sportarten eines Hundes ändert nur, wer ihn besitzt.
/// </summary>
public class PreferenceSportartenTests
{
    private sealed record Welt(ApplicationDbContext Db, PreferenceService Dienst, Sport Global, Sport MeinVerein, Sport FremderVerein, Guid Ich, Guid MeinVereinId);

    private static async Task<Welt> AufbauenAsync()
    {
        var db = InMemoryDbContext.Create();
        var mein = new Club { Name = "Mein Verein" };
        var fremd = new Club { Name = "Fremder Verein" };
        var global = new Sport { Code = "BH", Name = "BH" };
        var meinSport = new Sport { Code = "MEIN", Name = "Vereinssport", ClubId = mein.Id };
        var fremdSport = new Sport { Code = "FREMD", Name = "Fremder Vereinssport", ClubId = fremd.Id };
        var ich = Guid.NewGuid();
        db.Clubs.AddRange(mein, fremd);
        db.Sports.AddRange(global, meinSport, fremdSport);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = mein.Id, UserId = ich, Role = ClubRole.Training });
        await db.SaveChangesAsync();
        return new Welt(db, new PreferenceService(db), global, meinSport, fremdSport, ich, mein.Id);
    }

    private static async Task<Guid> HundAsync(Welt w, Guid besitzer, bool eigeneSportarten = false)
    {
        var hund = new Dog { Name = "Bello", UsesOwnSports = eigeneSportarten };
        w.Db.Dogs.Add(hund);
        w.Db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = besitzer });
        await w.Db.SaveChangesAsync();
        return hund.Id;
    }

    private static async Task<List<Guid>> MeineSportartenAsync(Welt w, Guid nutzer) =>
        (await w.Dienst.GetAsync(nutzer)).Value!.SportIds.ToList();

    // ------------------------------------------------------- eigene Sportarten

    [Fact]
    public async Task EigeneSportarten_FremdeVereinssportartWirdVerworfen_EigeneUndGlobaleBleiben()
    {
        var w = await AufbauenAsync();

        var ergebnis = await w.Dienst.UpdateSportsAsync(w.Ich, new UpdateSportsRequest([w.Global.Id, w.MeinVerein.Id, w.FremderVerein.Id, Guid.NewGuid()]));

        Assert.True(ergebnis.Succeeded);
        var gespeichert = await MeineSportartenAsync(w, w.Ich);
        Assert.Equivalent(new[] { w.Global.Id, w.MeinVerein.Id }, gespeichert);
        Assert.DoesNotContain(w.FremderVerein.Id, gespeichert);
    }

    [Fact]
    public async Task EigeneSportarten_OhneVereinszugehoerigkeit_BleibenNurGlobale()
    {
        var w = await AufbauenAsync();
        var fremder = Guid.NewGuid();

        await w.Dienst.UpdateSportsAsync(fremder, new UpdateSportsRequest([w.Global.Id, w.MeinVerein.Id]));

        Assert.Equal([w.Global.Id], await MeineSportartenAsync(w, fremder));
    }

    [Fact]
    public async Task EigeneSportarten_NeueAuswahlErsetztDieAlte_LeereAuswahlHeisstKeineEinschraenkung()
    {
        var w = await AufbauenAsync();
        await w.Dienst.UpdateSportsAsync(w.Ich, new UpdateSportsRequest([w.Global.Id]));

        await w.Dienst.UpdateSportsAsync(w.Ich, new UpdateSportsRequest([w.MeinVerein.Id]));
        Assert.Equal([w.MeinVerein.Id], await MeineSportartenAsync(w, w.Ich));

        await w.Dienst.UpdateSportsAsync(w.Ich, new UpdateSportsRequest([]));
        Assert.Empty(await MeineSportartenAsync(w, w.Ich));
    }

    // ------------------------------------------------------ Sportarten des Hundes

    [Fact]
    public async Task HundeSportarten_DerBesitzerAendert_FremdeVereinssportartenWerdenVerworfen()
    {
        var w = await AufbauenAsync();
        var hund = await HundAsync(w, w.Ich);

        var ergebnis = await w.Dienst.UpdateDogSportsAsync(w.Ich, hund, new UpdateDogSportsRequest(true, [w.Global.Id, w.FremderVerein.Id]));

        Assert.True(ergebnis.Succeeded);
        Assert.Equal([w.Global.Id], (await w.Dienst.GetEffectiveDogSportsAsync(w.Ich, hund)).Value);
        Assert.True((await w.Db.Dogs.SingleAsync()).UsesOwnSports);
    }

    [Fact]
    public async Task HundeSportarten_EinMitbesitzerDarfAendern()
    {
        var w = await AufbauenAsync();
        var hund = await HundAsync(w, Guid.NewGuid());
        var mitbesitzer = Guid.NewGuid();
        w.Db.DogOwners.Add(new DogOwner { DogId = hund, UserId = mitbesitzer });
        await w.Db.SaveChangesAsync();

        var ergebnis = await w.Dienst.UpdateDogSportsAsync(mitbesitzer, hund, new UpdateDogSportsRequest(true, [w.Global.Id]));

        Assert.True(ergebnis.Succeeded);
    }

    [Fact]
    public async Task HundeSportarten_EinBetreuenderTrainerDarfNichtAendern_UndErfaehrtNichtsDaruber()
    {
        var w = await AufbauenAsync();
        var besitzer = Guid.NewGuid();
        var trainer = Guid.NewGuid();
        var hund = await HundAsync(w, besitzer);
        w.Db.TrainerAssignments.Add(new TrainerAssignment { TrainerId = trainer, MemberId = besitzer, DogId = hund });
        await w.Db.SaveChangesAsync();
        // Wer den Hund betreut, sieht ihn - verwaltet ihn aber nicht (Zugriff ja, Besitz nein).
        Assert.True((await w.Dienst.GetEffectiveDogSportsAsync(trainer, hund)).Succeeded);

        var ergebnis = await w.Dienst.UpdateDogSportsAsync(trainer, hund, new UpdateDogSportsRequest(true, [w.Global.Id]));

        // Dieselbe Antwort wie bei einer fremden Person: nicht gefunden.
        Assert.False(ergebnis.Succeeded);
        Assert.True(ergebnis.IsNotFound);
        var dog = await w.Db.Dogs.SingleAsync();
        Assert.False(dog.UsesOwnSports);
        Assert.Empty(w.Db.DogSportSelections);
    }

    [Fact]
    public async Task HundeSportarten_EineFremdePersonDarfNichtAendern()
    {
        var w = await AufbauenAsync();
        var hund = await HundAsync(w, Guid.NewGuid());

        var ergebnis = await w.Dienst.UpdateDogSportsAsync(Guid.NewGuid(), hund, new UpdateDogSportsRequest(true, [w.Global.Id]));

        Assert.True(ergebnis.IsNotFound);
        Assert.Empty(w.Db.DogSportSelections);
    }

    // ------------------------------------------------------------- Vererbung

    [Fact]
    public async Task WirksameSportarten_HundMitEigenerAuswahlZaehltSeineEigenen_NichtDieDesMenschen()
    {
        var w = await AufbauenAsync();
        await w.Dienst.UpdateSportsAsync(w.Ich, new UpdateSportsRequest([w.Global.Id]));
        var hund = await HundAsync(w, w.Ich, eigeneSportarten: true);
        await w.Dienst.UpdateDogSportsAsync(w.Ich, hund, new UpdateDogSportsRequest(true, [w.MeinVerein.Id]));

        Assert.Equal([w.MeinVerein.Id], (await w.Dienst.GetEffectiveDogSportsAsync(w.Ich, hund)).Value);
    }

    [Fact]
    public async Task WirksameSportarten_HundOhneEigeneAuswahlErbtVomMenschen()
    {
        var w = await AufbauenAsync();
        await w.Dienst.UpdateSportsAsync(w.Ich, new UpdateSportsRequest([w.Global.Id, w.MeinVerein.Id]));
        var hund = await HundAsync(w, w.Ich);

        Assert.Equivalent(new[] { w.Global.Id, w.MeinVerein.Id }, (await w.Dienst.GetEffectiveDogSportsAsync(w.Ich, hund)).Value!);
    }

    [Fact]
    public async Task WirksameSportarten_EigeneAuswahlOhneEintraege_IstLeerUndErbtNicht()
    {
        var w = await AufbauenAsync();
        await w.Dienst.UpdateSportsAsync(w.Ich, new UpdateSportsRequest([w.Global.Id]));
        var hund = await HundAsync(w, w.Ich, eigeneSportarten: true);

        // Leer heißt "keine Einschränkung" - es fällt nicht auf die Auswahl des Menschen zurück.
        Assert.Empty((await w.Dienst.GetEffectiveDogSportsAsync(w.Ich, hund)).Value!);
    }

    [Fact]
    public async Task WirksameSportarten_FremdePersonOderUnbekannterHund_NichtGefunden()
    {
        var w = await AufbauenAsync();
        var hund = await HundAsync(w, w.Ich);

        Assert.True((await w.Dienst.GetEffectiveDogSportsAsync(Guid.NewGuid(), hund)).IsNotFound);
        Assert.True((await w.Dienst.GetEffectiveDogSportsAsync(w.Ich, Guid.NewGuid())).IsNotFound);
    }

    // ------------------------------------------------------------------ Land

    [Theory]
    [InlineData("de", "DE")]
    [InlineData("  at  ", "AT")]
    [InlineData("CH", "CH")]
    public async Task Land_EinBekanntesKuerzelWirdGrossGespeichert(string eingabe, string gespeichert)
    {
        var w = await AufbauenAsync();
        w.Db.Countries.AddRange(new Country { Code = "DE" }, new Country { Code = "AT" }, new Country { Code = "CH" });
        await w.Db.SaveChangesAsync();

        var ergebnis = await w.Dienst.UpdateCountryAsync(w.Ich, new UpdateCountryRequest(eingabe));

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(gespeichert, (await w.Dienst.GetAsync(w.Ich)).Value!.Country);
    }

    [Theory]
    [InlineData("DEU")]
    [InlineData("D")]
    [InlineData("ZZ")]
    public async Task Land_UngueltigesOderUnbekanntesKuerzel_WirdAbgelehntUndNichtGespeichert(string eingabe)
    {
        var w = await AufbauenAsync();
        w.Db.Countries.Add(new Country { Code = "DE" });
        await w.Db.SaveChangesAsync();
        await w.Dienst.UpdateCountryAsync(w.Ich, new UpdateCountryRequest("DE"));

        var ergebnis = await w.Dienst.UpdateCountryAsync(w.Ich, new UpdateCountryRequest(eingabe));

        Assert.False(ergebnis.Succeeded);
        Assert.Equal("DE", (await w.Dienst.GetAsync(w.Ich)).Value!.Country);
    }

    [Fact]
    public async Task Land_LeerSetztAufDieVorgabeZurueck()
    {
        var w = await AufbauenAsync();
        w.Db.Countries.Add(new Country { Code = "DE" });
        await w.Db.SaveChangesAsync();
        await w.Dienst.UpdateCountryAsync(w.Ich, new UpdateCountryRequest("DE"));

        Assert.True((await w.Dienst.UpdateCountryAsync(w.Ich, new UpdateCountryRequest("  "))).Succeeded);

        Assert.Null((await w.Dienst.GetAsync(w.Ich)).Value!.Country);
    }
}
