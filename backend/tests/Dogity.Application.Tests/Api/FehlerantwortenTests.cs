using System.Data.Common;
using System.Text.Json;
using Dogity.Api.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dogity.Application.Tests.Api;

/// <summary>
/// Die Netze für die Fehlerantworten: Ein zu langer Text und ein Formfehler sind 400
/// (<c>{ errors: [...] }</c>), nie ein 500. Das Frontend verwirft 4xx aus der Offline-Warteschlange
/// und versucht 5xx später erneut - ein 500 hielt die Warteschlange samt allen Fährten
/// dahinter für immer an.
/// </summary>
public class FehlerantwortenTests
{
    /// <summary>Wie die Datenbank eine zu lange Spalte meldet: eine DbException mit SQLSTATE.</summary>
    private sealed class SqlFehler(string sqlState) : DbException("Datenbankfehler")
    {
        public override string? SqlState { get; } = sqlState;
    }

    /// <summary>Eine Antwort, die schon an den Client unterwegs ist (HasStarted).</summary>
    private sealed class BegonneneAntwort : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }

    private static async Task<(int Status, string Rumpf)> LaufenLassenAsync(Exception? wirft, bool antwortBegonnen = false)
    {
        var app = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
        app.UseTextZuLangAls400();
        app.Run(async ctx =>
        {
            if (wirft is not null) throw wirft;
        });
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        if (antwortBegonnen) ctx.Features.Set<IHttpResponseFeature>(new BegonneneAntwort());

        await app.Build()(ctx);

        ctx.Response.Body.Position = 0;
        return (ctx.Response.StatusCode, await new StreamReader(ctx.Response.Body).ReadToEndAsync());
    }

    [Fact]
    public async Task EinZuLangerText_WirdZu400MitDemFehlerformat()
    {
        var (status, rumpf) = await LaufenLassenAsync(new DbUpdateException("x", new SqlFehler("22001")));

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        var fehler = JsonDocument.Parse(rumpf).RootElement.GetProperty("errors");
        Assert.Equal("Ein Text ist zu lang.", Assert.Single(fehler.EnumerateArray()).GetString());
    }

    [Fact]
    public async Task EinAndererDatenbankfehler_BleibtEinFehlerUndWirdNichtVerschluckt()
    {
        // Etwa eine verletzte Eindeutigkeit (23505): Das ist kein "zu langer Text".
        await Assert.ThrowsAsync<DbUpdateException>(() => LaufenLassenAsync(new DbUpdateException("x", new SqlFehler("23505"))));
    }

    [Fact]
    public async Task EineFremdeAusnahme_LaeuftDurch()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => LaufenLassenAsync(new InvalidOperationException("kaputt")));
    }

    [Fact]
    public async Task BeiBereitsBegonnenerAntwort_WirdNichtMehrUmgeschrieben()
    {
        // Ist die Antwort schon unterwegs, lässt sich der Status nicht mehr ändern - die Ausnahme zählt.
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            LaufenLassenAsync(new DbUpdateException("x", new SqlFehler("22001")), antwortBegonnen: true));
    }

    [Fact]
    public async Task OhneFehler_BleibtDieAntwortUnveraendert()
    {
        var (status, rumpf) = await LaufenLassenAsync(null);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("", rumpf);
    }

    // --------------------------------------------------------------- Formfehler

    private static string[] Formfehler(params string[] felderMitFehler)
    {
        var dienste = new ServiceCollection();
        dienste.AddLogging();
        dienste.AddControllers().MitEigenenFormfehlern();
        var optionen = dienste.BuildServiceProvider().GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;

        var kontext = new ActionContext(new DefaultHttpContext(), new RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        foreach (var feld in felderMitFehler) kontext.ModelState.AddModelError(feld, "intern");

        var ergebnis = Assert.IsType<BadRequestObjectResult>(optionen.InvalidModelStateResponseFactory(kontext));
        var errors = ergebnis.Value!.GetType().GetProperty("errors")!.GetValue(ergebnis.Value);
        return ((IEnumerable<string>)errors!).ToArray();
    }

    [Theory]
    [InlineData("$.durationMinutes", "durationMinutes")]
    [InlineData("request.Notes", "Notes")]
    [InlineData("Request.Notes", "Notes")]
    [InlineData("Name", "Name")]
    [InlineData("$.exercises[0].rating", "exercises[0].rating")]
    public void EinFormfehler_NenntDasFeldOhnePfadPraefix(string schluessel, string erwartetesFeld)
    {
        var meldungen = Formfehler(schluessel);

        Assert.Equal($"Ungültiger Wert für \"{erwartetesFeld}\".", Assert.Single(meldungen));
    }

    [Theory]
    [InlineData("request")]
    [InlineData("$")]
    [InlineData("")]
    public void OhneBenennbaresFeld_KommtDieAllgemeineMeldung(string schluessel)
    {
        // "request" allein heißt: Der ganze Rumpf fehlt oder ist kein JSON.
        var meldungen = Formfehler(schluessel);

        Assert.Equal("Die Anfrage ist unvollständig oder fehlerhaft.", Assert.Single(meldungen));
    }

    [Fact]
    public void MehrereFelder_JedesEinmal_UndNurDieMitFehler()
    {
        var meldungen = Formfehler("$.a", "$.b", "$.a");

        Assert.Equal(["Ungültiger Wert für \"a\".", "Ungültiger Wert für \"b\"."], meldungen);
    }
}
