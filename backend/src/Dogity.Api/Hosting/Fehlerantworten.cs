using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Api.Hosting;

/// <summary>
/// Fehlerantworten im Format der App (<c>{ errors: ["..."] }</c>, siehe
/// CODING_GUIDELINES.md) auch dort, wo ASP.NET und die Datenbank sonst
/// eigene Wege gehen.
///
/// Beides ist mehr als Kosmetik: Das Frontend reicht Fehler an die
/// Offline-Warteschlange weiter, und die unterscheidet danach, ob sie einen
/// Eintrag verwerfen (4xx) oder später erneut versuchen soll (5xx). Ein zu
/// langer Text war bis 2026-09-28 ein 500er - "später nochmal" - und hielt
/// die Warteschlange samt allen Fährten dahinter für immer an.
/// </summary>
public static class Fehlerantworten
{
    /// <summary>SQLSTATE "string_data_right_truncation": Text länger als die Spalte.</summary>
    private const string TextZuLang = "22001";

    /// <summary>
    /// Formfehler (etwa Text statt Zahl) als <c>{ errors: [...] }</c> auf Deutsch statt
    /// als englische ProblemDetails, deren <c>errors</c> ein Objekt ist.
    /// </summary>
    public static IMvcBuilder MitEigenenFormfehlern(this IMvcBuilder mvc) =>
        mvc.ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var felder = context.ModelState
                    .Where(e => e.Value is { Errors.Count: > 0 })
                    .Select(e => Feldname(e.Key))
                    .Distinct()
                    .ToList();
                var meldungen = felder.Count == 0 || felder.All(string.IsNullOrEmpty)
                    ? ["Die Anfrage ist unvollständig oder fehlerhaft."]
                    : felder.Where(f => !string.IsNullOrEmpty(f)).Select(f => $"Ungültiger Wert für \"{f}\".").ToArray();
                return new BadRequestObjectResult(new { errors = meldungen });
            };
        });

    /// <summary>
    /// Fängt das Überschreiten einer Spaltenlänge ab und meldet es als 400.
    /// Ein Netz für alle Felder, auch künftige - die Services prüfen die
    /// Freitexte, die Menschen tatsächlich lang füllen, zusätzlich selbst.
    /// </summary>
    public static IApplicationBuilder UseTextZuLangAls400(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (DbUpdateException ex) when (ex.InnerException is DbException { SqlState: TextZuLang } && !context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { errors = new[] { "Ein Text ist zu lang." } });
            }
        });

    /// <summary>"$.durationMinutes" oder "request.Notes" wird zu "durationMinutes" bzw. "Notes".</summary>
    private static string Feldname(string schluessel)
    {
        var name = schluessel.TrimStart('$', '.');
        // "request" allein heißt: Der ganze Rumpf fehlt oder ist kein JSON -
        // das ist kein Feld, das man benennen könnte.
        if (name.Equals("request", StringComparison.OrdinalIgnoreCase)) return "";
        return name.StartsWith("request.", StringComparison.OrdinalIgnoreCase) ? name["request.".Length..] : name;
    }
}
