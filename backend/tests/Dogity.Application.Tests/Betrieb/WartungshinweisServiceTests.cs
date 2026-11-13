using Dogity.Application.Betrieb;
using Dogity.Application.Common;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Betrieb;
using Dogity.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Memory;

namespace Dogity.Application.Tests.Betrieb;

/// <summary>
/// Der Wartungshinweis: Eingabeprüfung beim Setzen und die Frage, wann der öffentliche
/// Abruf etwas liefert ("läuft" erst ab Beginn, nichts mehr nach Ende oder wenn aus).
/// Wer setzen darf, regelt der AdminController (siehe WartungshinweisRechteTests).
/// </summary>
public class WartungshinweisServiceTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private sealed class VerstellbareUhr(DateTimeOffset jetzt) : TimeProvider
    {
        public DateTimeOffset Jetzt { get; set; } = jetzt;
        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    private static (WartungshinweisService Service, ApplicationDbContext Db, VerstellbareUhr Uhr) Aufbau()
    {
        var db = InMemoryDbContext.Create();
        var uhr = new VerstellbareUhr(Jetzt);
        return (new WartungshinweisService(db, new MemoryCache(new MemoryCacheOptions()), uhr), db, uhr);
    }

    private static SetWartungshinweisRequest Anfrage(
        TimeSpan beginnIn, TimeSpan? dauer = null, string? text = null, bool aktiv = true) =>
        new(text, Jetzt + beginnIn, dauer is null ? null : Jetzt + beginnIn + dauer, aktiv);

    [Fact]
    public async Task Abruf_OhneHinweis_IstLeer()
    {
        var (service, _, _) = Aufbau();

        var result = await service.GetAktuellenAsync();

        Assert.True(result.Succeeded);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Abruf_VorBeginn_LaeuftNicht()
    {
        var (service, _, _) = Aufbau();
        await service.SetzenAsync(Anfrage(TimeSpan.FromHours(2), TimeSpan.FromHours(1), "Update am Abend"));

        var hinweis = (await service.GetAktuellenAsync()).Value;

        Assert.NotNull(hinweis);
        Assert.False(hinweis.Laeuft);
        Assert.Equal("Update am Abend", hinweis.Text);
        Assert.Equal(Jetzt.AddHours(2), hinweis.Beginn);
        Assert.Equal(Jetzt.AddHours(3), hinweis.Ende);
    }

    [Fact]
    public async Task Abruf_WaehrendDerWartung_Laeuft()
    {
        var (service, _, _) = Aufbau();
        await service.SetzenAsync(Anfrage(TimeSpan.FromMinutes(-5), TimeSpan.FromHours(1)));

        var hinweis = (await service.GetAktuellenAsync()).Value;

        Assert.NotNull(hinweis);
        Assert.True(hinweis.Laeuft);
        Assert.Null(hinweis.Text);
    }

    [Fact]
    public async Task Abruf_NachEnde_IstLeer()
    {
        var (service, _, uhr) = Aufbau();
        await service.SetzenAsync(Anfrage(TimeSpan.FromMinutes(-5), TimeSpan.FromHours(1)));
        Assert.NotNull((await service.GetAktuellenAsync()).Value);

        // Innerhalb der 30 Sekunden Zwischenspeicher: das Ende gilt trotzdem sofort.
        uhr.Jetzt = Jetzt.AddMinutes(55).AddSeconds(5);
        Assert.Null((await service.GetAktuellenAsync()).Value);
    }

    [Fact]
    public async Task Abruf_OhneEnde_BleibtBisZumAusschalten()
    {
        var (service, _, uhr) = Aufbau();
        await service.SetzenAsync(Anfrage(TimeSpan.FromMinutes(-5)));
        uhr.Jetzt = Jetzt.AddDays(3);

        var hinweis = (await service.GetAktuellenAsync()).Value;

        Assert.NotNull(hinweis);
        Assert.Null(hinweis.Ende);
        Assert.True(hinweis.Laeuft);
    }

    [Fact]
    public async Task Abruf_Ausgeschaltet_IstLeer_ObwohlGespeichert()
    {
        var (service, _, _) = Aufbau();
        await service.SetzenAsync(Anfrage(TimeSpan.FromHours(1), TimeSpan.FromHours(1), aktiv: false));

        Assert.Null((await service.GetAktuellenAsync()).Value);
        Assert.NotNull((await service.GetGespeicherteAsync()).Value);
    }

    [Fact]
    public async Task Ausschalten_NimmtDenHinweisSofortWeg_UndBehaeltDieDaten()
    {
        var (service, _, _) = Aufbau();
        await service.SetzenAsync(Anfrage(TimeSpan.FromHours(1), TimeSpan.FromHours(1), "Update"));
        Assert.NotNull((await service.GetAktuellenAsync()).Value); // füllt den Zwischenspeicher

        var result = await service.AusschaltenAsync();

        Assert.True(result.Succeeded);
        Assert.Null((await service.GetAktuellenAsync()).Value);
        var gespeichert = (await service.GetGespeicherteAsync()).Value!;
        Assert.False(gespeichert.Aktiv);
        Assert.Equal("Update", gespeichert.Text);
    }

    [Fact]
    public async Task Ausschalten_OhneHinweis_IstKeinFehler()
    {
        var (service, db, _) = Aufbau();

        var result = await service.AusschaltenAsync();

        Assert.True(result.Succeeded);
        Assert.Empty(db.Wartungshinweise);
    }

    [Fact]
    public async Task Setzen_ErneutUeberschreibtDieEineZeile_UndMeldetDieAenderung()
    {
        var (service, db, uhr) = Aufbau();
        var erster = (await service.SetzenAsync(Anfrage(TimeSpan.FromHours(1), TimeSpan.FromHours(1), "Erst"))).Value!;
        uhr.Jetzt = Jetzt.AddMinutes(10);

        var zweiter = (await service.SetzenAsync(Anfrage(TimeSpan.FromHours(1), TimeSpan.FromHours(1), "Dann"))).Value!;

        Assert.Single(db.Wartungshinweise);
        Assert.Equal("Dann", (await service.GetAktuellenAsync()).Value!.Text);
        // Ein geänderter Hinweis bekommt einen neuen Schlüssel, damit er wieder erscheint.
        Assert.True(zweiter.GeaendertAm > erster.GeaendertAm);
    }

    [Fact]
    public async Task Setzen_LeererText_WirdZuKeinemText()
    {
        var (service, _, _) = Aufbau();

        var result = await service.SetzenAsync(Anfrage(TimeSpan.FromHours(1), text: "   "));

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.Text);
    }

    [Fact]
    public async Task Setzen_EndeVorBeginn_WirdAbgelehnt()
    {
        var (service, db, _) = Aufbau();

        var result = await service.SetzenAsync(Anfrage(TimeSpan.FromHours(2), TimeSpan.FromHours(-1)));

        Assert.False(result.Succeeded);
        Assert.False(result.IsNotFound);
        Assert.Empty(db.Wartungshinweise);
    }

    [Fact]
    public async Task Setzen_EndeGleichBeginn_WirdAbgelehnt()
    {
        var (service, _, _) = Aufbau();

        var result = await service.SetzenAsync(Anfrage(TimeSpan.FromHours(2), TimeSpan.Zero));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Setzen_TextGenauAnDerGrenze_Geht_DarueberNicht()
    {
        var (service, db, _) = Aufbau();

        var passt = await service.SetzenAsync(Anfrage(TimeSpan.FromHours(1), text: new string('a', Textlaengen.WartungshinweisText)));
        var zuLang = await service.SetzenAsync(Anfrage(TimeSpan.FromHours(1), text: new string('a', Textlaengen.WartungshinweisText + 1)));

        Assert.True(passt.Succeeded);
        Assert.False(zuLang.Succeeded);
        Assert.Equal(Textlaengen.WartungshinweisText, db.Wartungshinweise.Single().Text!.Length);
    }

    [Fact]
    public async Task Gespeicherte_OhneHinweis_IstLeerUndAus()
    {
        var (service, _, _) = Aufbau();

        var dto = (await service.GetGespeicherteAsync()).Value!;

        Assert.False(dto.Aktiv);
        Assert.Null(dto.Beginn);
        Assert.Null(dto.GeaendertAm);
    }

    [Fact]
    public async Task GeloeschteZeile_ZaehltNichtAlsHinweis()
    {
        var (service, db, _) = Aufbau();
        db.Wartungshinweise.Add(new Wartungshinweis { Aktiv = true, Beginn = Jetzt.AddHours(-1), DeletedAt = Jetzt.AddMinutes(-1) });
        await db.SaveChangesAsync();

        Assert.Null((await service.GetAktuellenAsync()).Value);
    }
}
