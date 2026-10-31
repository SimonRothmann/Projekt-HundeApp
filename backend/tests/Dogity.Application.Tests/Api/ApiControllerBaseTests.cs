using Dogity.Api.Controllers;
using Dogity.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Dogity.Application.Tests.Api;

/// <summary>
/// Wie ein <see cref="Result"/> zur HTTP-Antwort wird. Zwei Wege, die beide gewollt sind:
/// <c>FromResult</c> unterscheidet "nicht gefunden" (404) von einer abgelehnten Eingabe (400);
/// <c>FromResultAlsBadRequest</c> meldet jeden Fehlschlag als 400 - so antworten die Katalog-,
/// Übungs- und Aufzeichnungsaktionen seit jeher, und daran hängt die Antwortform.
/// </summary>
public class ApiControllerBaseTests
{
    private sealed class Probe : ApiControllerBase
    {
        public ActionResult<int> Mit(Result<int> r) => FromResult(r);
        public ActionResult<int> AlsBadRequest(Result<int> r) => FromResultAlsBadRequest(r);
        public ActionResult MitOhneWert(Result r) => FromResult(r);
        public ActionResult AlsBadRequestOhneWert(Result r) => FromResultAlsBadRequest(r);
    }

    private static int? Status(ActionResult<int> antwort) => (antwort.Result as IStatusCodeActionResult)?.StatusCode;
    private static int? Status(ActionResult antwort) => (antwort as IStatusCodeActionResult)?.StatusCode;

    [Fact]
    public void FromResult_UnterscheidetNichtGefundenVonEinerAbgelehntenEingabe()
    {
        var probe = new Probe();

        Assert.Equal(1, Assert.IsType<OkObjectResult>(probe.Mit(Result<int>.Success(1)).Result).Value);
        Assert.Equal(404, Status(probe.Mit(Result<int>.NotFound("Hund nicht gefunden."))));
        Assert.Equal(400, Status(probe.Mit(Result<int>.Failure("Bewertung muss zwischen 1 und 5 liegen."))));
        Assert.Equal(204, Status(probe.MitOhneWert(Result.Success())));
        Assert.Equal(404, Status(probe.MitOhneWert(Result.NotFound("x"))));
        Assert.Equal(400, Status(probe.MitOhneWert(Result.Failure("x"))));
    }

    [Fact]
    public void FromResultAlsBadRequest_MeldetJedenFehlschlagAls400_AuchNichtGefunden()
    {
        var probe = new Probe();

        Assert.Equal(1, Assert.IsType<OkObjectResult>(probe.AlsBadRequest(Result<int>.Success(1)).Result).Value);
        Assert.Equal(400, Status(probe.AlsBadRequest(Result<int>.NotFound("Sportart nicht gefunden."))));
        Assert.Equal(400, Status(probe.AlsBadRequest(Result<int>.Failure("x"))));
        Assert.Equal(204, Status(probe.AlsBadRequestOhneWert(Result.Success())));
        Assert.Equal(400, Status(probe.AlsBadRequestOhneWert(Result.NotFound("Prüfungsordnung nicht gefunden."))));
    }

    [Fact]
    public void DieFehlerstehenImFormat_ErrorsListe()
    {
        var antwort = new Probe().AlsBadRequest(Result<int>.NotFound("Sportart nicht gefunden."));

        var rumpf = Assert.IsType<BadRequestObjectResult>(antwort.Result).Value!;
        var errors = (IEnumerable<string>)rumpf.GetType().GetProperty("errors")!.GetValue(rumpf)!;
        Assert.Equal(["Sportart nicht gefunden."], errors);
    }
}
