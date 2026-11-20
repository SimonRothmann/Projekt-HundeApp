using Dogity.Infrastructure.Identity;

namespace Dogity.Api.Hosting;

/// <summary>
/// Zweite Linie neben SameSite und der CORS-Liste: Hängt an einer ändernden Anfrage ein Anmelde-Cookie
/// und kein Authorization-Kopf, muss der Kopf <c>X-Dogity</c> gesetzt sein, sonst 403. Das Frontend
/// schickt ihn bei jedem Aufruf; eine Seite, die nur ein Formular abschickt oder ein Bild einbindet,
/// kann keinen eigenen Kopf setzen. Lesende Anfragen (GET, HEAD, OPTIONS) bleiben frei.
/// </summary>
public static class KopfBeiCookies
{
    public static bool Verlangt(HttpRequest anfrage) =>
        !HttpMethods.IsGet(anfrage.Method) && !HttpMethods.IsHead(anfrage.Method) && !HttpMethods.IsOptions(anfrage.Method)
        && !anfrage.Headers.ContainsKey("Authorization")
        && (anfrage.Cookies.ContainsKey(AnmeldeCookies.Zugriff) || anfrage.Cookies.ContainsKey(AnmeldeCookies.Erneuerung))
        && !anfrage.Headers.ContainsKey(AnmeldeCookies.Kopf);

    public static IApplicationBuilder UseKopfBeiCookies(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (!Verlangt(context.Request))
            {
                await next(context);
                return;
            }
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { errors = new[] { "Anfrage abgelehnt. Bitte die App neu laden." } });
        });
}
