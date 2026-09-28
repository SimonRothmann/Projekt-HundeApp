using Dogity.Application.Abstractions;

namespace Dogity.Application.Tests.Identity;

/// <summary>
/// Die Unterscheidung der beiden Sperren in LockoutEnd - Grundlage dafür,
/// dass fünf falsche Passwörter einer fremden Person nicht mehr alle
/// Sitzungen beenden (Prüfung 2026-09-28).
/// </summary>
public class SperrenTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OhneSperre_Keine()
    {
        Assert.Equal(Sperrart.Keine, Sperren.Bestimme(null, Jetzt));
    }

    [Fact]
    public void AbgelaufeneSperre_Keine()
    {
        Assert.Equal(Sperrart.Keine, Sperren.Bestimme(Jetzt.AddSeconds(-1), Jetzt));
        Assert.Equal(Sperrart.Keine, Sperren.Bestimme(Jetzt, Jetzt));
    }

    [Fact]
    public void FuenfMinutenNachFehlversuchen_Voruebergehend()
    {
        Assert.Equal(Sperrart.Voruebergehend, Sperren.Bestimme(Jetzt.AddMinutes(5), Jetzt));
    }

    [Fact]
    public void SperreDurchDenAdmin_Dauerhaft()
    {
        // UserLookupService.LockUserAsync setzt MaxValue.
        Assert.Equal(Sperrart.Dauerhaft, Sperren.Bestimme(DateTimeOffset.MaxValue, Jetzt));
    }
}
