using System.Reflection;
using Dogity.Api.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace Dogity.Application.Tests.Betrieb;

/// <summary>
/// Setzen und Ausschalten des Wartungshinweises dürfen nur Admins - die Prüfung sitzt als
/// Attribut am AdminController (siehe AdminControllerRechteTests für die Klasse selbst).
/// Der öffentliche Abruf ist bewusst anonym (Drosselung: AnonymeEndpunkteTests).
/// </summary>
public class WartungshinweisRechteTests
{
    [Theory]
    [InlineData(nameof(AdminController.GetWartungshinweis))]
    [InlineData(nameof(AdminController.SetWartungshinweis))]
    [InlineData(nameof(AdminController.DisableWartungshinweis))]
    public void AdminEndpunkte_LockernDieAdminBeschraenkungNicht(string methode)
    {
        var m = typeof(AdminController).GetMethod(methode)!;

        Assert.Null(m.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(m.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void OeffentlicherController_HatNurDenLesendenAbruf()
    {
        // Schreibende Endpunkte gehören in den AdminController - hier wäre Anonymität zu weit.
        var methoden = typeof(WartungshinweisController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.Equal([nameof(WartungshinweisController.Get)], methoden.Select(m => m.Name));
    }
}
