using Dogity.Api.Hosting;
using Dogity.Application.Community;

namespace Dogity.Application.Tests.Api;

/// <summary>
/// Die nächtliche Plan-Neuberechnung läuft um 03:00 Uhr deutscher Zeit, nicht
/// 24 Stunden nach dem Start. Erwartet wird die Wanduhr in der Vereinszeit.
/// </summary>
public class PlanRegenerationLaufzeitTests
{
    private static DateTime Lokal(DateTimeOffset utc) => Vereinszeit.Lokal(utc);

    private static DateTimeOffset Utc(int jahr, int monat, int tag, int std, int min) =>
        new(jahr, monat, tag, std, min, 0, TimeSpan.Zero);

    [Fact]
    public void AmAbend_IstDerNaechsteLaufDieNachtDanach()
    {
        // 15.01. 19:00 Uhr (UTC+1) -> 16.01. 03:00 Uhr
        var lauf = PlanRegenerationBackgroundService.NaechsterLauf(Utc(2026, 1, 15, 18, 0));

        Assert.Equal(new DateTime(2026, 1, 16, 3, 0, 0), Lokal(lauf));
        Assert.Equal(Utc(2026, 1, 16, 2, 0), lauf);
    }

    [Fact]
    public void KurzVor3Uhr_LaeuftNochAmSelbenTag()
    {
        // 02:59 Uhr (UTC+1)
        var lauf = PlanRegenerationBackgroundService.NaechsterLauf(Utc(2026, 1, 16, 1, 59));

        Assert.Equal(Utc(2026, 1, 16, 2, 0), lauf);
    }

    [Theory]
    [InlineData(2026, 1, 16, 2, 0)] // genau 03:00 Uhr
    [InlineData(2026, 1, 16, 2, 1)] // kurz danach
    public void AbDreiUhr_IstDerNaechsteLaufDerFolgetag(int jahr, int monat, int tag, int std, int min)
    {
        var lauf = PlanRegenerationBackgroundService.NaechsterLauf(Utc(jahr, monat, tag, std, min));

        Assert.Equal(new DateTime(2026, 1, 17, 3, 0, 0), Lokal(lauf));
    }

    [Fact]
    public void Sommerzeit_03UhrIstUtcPlus2()
    {
        var lauf = PlanRegenerationBackgroundService.NaechsterLauf(Utc(2026, 7, 10, 12, 0));

        Assert.Equal(Utc(2026, 7, 11, 1, 0), lauf);
    }

    [Fact]
    public void Umstellung_Maerz_VorUndNachDerUmstellung()
    {
        // 29.03.2026: 02:00 CET -> 03:00 CEST. Am Vorabend um 22:00 Uhr (UTC+1):
        // 03:00 CEST = 01:00 UTC; der Abstand beträgt nur 4 Stunden.
        var vorher = Utc(2026, 3, 28, 21, 0);
        var lauf = PlanRegenerationBackgroundService.NaechsterLauf(vorher);

        Assert.Equal(Utc(2026, 3, 29, 1, 0), lauf);
        Assert.Equal(new DateTime(2026, 3, 29, 3, 0, 0), Lokal(lauf));

        // Kurz nach dem Lauf: der nächste ist 03:00 Uhr CEST des Folgetags.
        var danach = PlanRegenerationBackgroundService.NaechsterLauf(Utc(2026, 3, 29, 1, 1));
        Assert.Equal(Utc(2026, 3, 30, 1, 0), danach);
    }

    [Fact]
    public void Umstellung_Oktober_VorUndNachDerUmstellung()
    {
        // 25.10.2026: 03:00 CEST -> 02:00 CET. Am Vorabend 22:00 Uhr (UTC+2):
        // 03:00 gibt es einmal, als CET = 02:00 UTC; der Abstand beträgt 5 Stunden.
        var lauf = PlanRegenerationBackgroundService.NaechsterLauf(Utc(2026, 10, 24, 20, 0));

        Assert.Equal(Utc(2026, 10, 25, 2, 0), lauf);
        Assert.Equal(new DateTime(2026, 10, 25, 3, 0, 0), Lokal(lauf));

        // 02:30 Uhr in der ersten (CEST = 00:30 UTC) und zweiten (CET = 01:30 UTC) Stunde: beide vor dem Lauf.
        Assert.Equal(Utc(2026, 10, 25, 2, 0), PlanRegenerationBackgroundService.NaechsterLauf(Utc(2026, 10, 25, 0, 30)));
        Assert.Equal(Utc(2026, 10, 25, 2, 0), PlanRegenerationBackgroundService.NaechsterLauf(Utc(2026, 10, 25, 1, 30)));

        // Nach dem Lauf: nächster Tag, jetzt UTC+1.
        Assert.Equal(Utc(2026, 10, 26, 2, 0), PlanRegenerationBackgroundService.NaechsterLauf(Utc(2026, 10, 25, 2, 1)));
    }
}
