using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.TestSupport;

/// <summary>
/// Erzeugt einen frischen InMemory-ApplicationDbContext pro Test (über den
/// GUID-basierten Datenbanknamen) - damit kein Test-State zwischen Tests
/// versehentlich weiterlebt. Nutzt die echte ApplicationDbContext-Klasse
/// inkl. aller Soft-Delete-QueryFilter, damit getestetes Service-Verhalten
/// exakt dem Production-Pfad entspricht.
/// </summary>
public static class InMemoryDbContext
{
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
