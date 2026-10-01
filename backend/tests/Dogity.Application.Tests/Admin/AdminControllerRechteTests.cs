using System.Reflection;
using Dogity.Api.Controllers;
using Dogity.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Dogity.Application.Tests.Admin;

/// <summary>
/// Die Rechteprüfung der Admin-Endpunkte sitzt als Attribut am Controller, nicht
/// im Service - ohne diesen Test fiele ein versehentlich entferntes oder
/// aufgeweichtes Attribut erst auf, wenn jemand die Endpunkte aufruft.
/// Besonders wichtig für "Daten gelöschter Konten": Der Endpunkt löscht endgültig.
/// </summary>
public class AdminControllerRechteTests
{
    [Fact]
    public void AdminController_IstAufAdminsBeschraenkt()
    {
        // Direkt am Controller (nicht geerbt): Die Basisklasse verlangt nur Anmeldung.
        var attribut = Assert.Single(typeof(AdminController).GetCustomAttributes<AuthorizeAttribute>(inherit: false));

        Assert.Equal(Roles.Admin, attribut.Roles);
    }

    [Theory]
    [InlineData(nameof(AdminController.GetOrphanedData))]
    [InlineData(nameof(AdminController.PurgeOrphanedData))]
    public void DatenGeloeschterKonten_Endpunkte_LockernDieBeschraenkungNicht(string methode)
    {
        var m = typeof(AdminController).GetMethod(methode)!;

        Assert.Null(m.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(m.GetCustomAttribute<AuthorizeAttribute>()); // erbt die Admin-Beschränkung unverändert
    }
}
