using Dogity.Application.Dogs;
using Dogity.Domain.Dogs;

namespace Dogity.Application.Tests.Dogs;

/// <summary>
/// Die reinen Regeln der Gesundheitseinträge: wann eine Frist "bald fällig" oder "überfällig" ist,
/// welcher Eintrag je Art und Titel "aktuell" ist und was der Stand je Hund meldet. "Heute" kommt von außen.
/// </summary>
public class GesundheitsstandTests
{
    private static readonly DateOnly Heute = new(2026, 11, 4);

    private static DogHealthRecord Eintrag(
        Guid hund, DogHealthKind art, string titel, DateOnly datum, DateOnly? faellig = null, TimeOnly? uhrzeit = null, int angelegtVorTagen = 0) =>
        new()
        {
            DogId = hund, Kind = art, Title = titel, Date = datum, DueDate = faellig, Time = uhrzeit,
            CreatedAt = new DateTimeOffset(2026, 11, 4, 8, 0, 0, TimeSpan.Zero).AddDays(-angelegtVorTagen),
        };

    // --- Zustand ----------------------------------------------------------------

    [Theory]
    [InlineData(-1, HealthState.Overdue)]    // gestern abgelaufen
    [InlineData(0, HealthState.DueSoon)]     // läuft heute ab: noch gültig, aber dringend
    [InlineData(1, HealthState.DueSoon)]
    [InlineData(30, HealthState.DueSoon)]    // die Grenze gehört zu "bald"
    [InlineData(31, HealthState.Ok)]
    [InlineData(900, HealthState.Ok)]
    public void Zustand_RechnetMitGanzenTagenAbHeute(int tageBisFrist, HealthState erwartet) =>
        Assert.Equal(erwartet, Gesundheitsstand.Zustand(Heute.AddDays(tageBisFrist), Heute));

    [Fact]
    public void Zustand_OhneFrist_IstOk() =>
        Assert.Equal(HealthState.Ok, Gesundheitsstand.Zustand(null, Heute));

    [Fact]
    public void Zustand_UeberDenJahreswechsel()
    {
        var silvester = new DateOnly(2026, 12, 31);
        Assert.Equal(HealthState.DueSoon, Gesundheitsstand.Zustand(new DateOnly(2027, 1, 20), silvester));
        Assert.Equal(HealthState.Ok, Gesundheitsstand.Zustand(new DateOnly(2027, 2, 1), silvester));
    }

    // --- Aktuell ----------------------------------------------------------------

    [Fact]
    public void Aktuell_JeArtUndTitelDerJuengste_DieAelterenSindVerlauf()
    {
        var hund = Guid.NewGuid();
        var alt = Eintrag(hund, DogHealthKind.Vaccination, "Tollwut", Heute.AddYears(-3), Heute.AddDays(-5));
        var neu = Eintrag(hund, DogHealthKind.Vaccination, "Tollwut", Heute.AddDays(-2), Heute.AddYears(3));
        var andere = Eintrag(hund, DogHealthKind.Vaccination, "SHPPi", Heute.AddYears(-1), Heute.AddYears(2));

        var aktuell = Gesundheitsstand.Aktuelle([alt, neu, andere]);

        Assert.Equal(2, aktuell.Count);
        Assert.Contains(neu, aktuell);
        Assert.Contains(andere, aktuell);
        Assert.DoesNotContain(alt, aktuell);
    }

    [Fact]
    public void Aktuell_TitelOhneRandUndGrossschreibung_IstDerselbe()
    {
        var hund = Guid.NewGuid();
        var alt = Eintrag(hund, DogHealthKind.Vaccination, "tollwut ", Heute.AddYears(-1));
        var neu = Eintrag(hund, DogHealthKind.Vaccination, "Tollwut", Heute);

        Assert.Same(neu, Assert.Single(Gesundheitsstand.Aktuelle([alt, neu])));
    }

    [Fact]
    public void Aktuell_GleichesTitelwortBeiAndererArtOderAnderemHund_IstNichtDasselbe()
    {
        var hund = Guid.NewGuid();
        var anderer = Guid.NewGuid();
        var eintraege = new[]
        {
            Eintrag(hund, DogHealthKind.Deworming, "Spot-on", Heute.AddDays(-10)),
            Eintrag(hund, DogHealthKind.ParasiteProtection, "Spot-on", Heute.AddDays(-20)),
            Eintrag(anderer, DogHealthKind.Deworming, "Spot-on", Heute.AddDays(-30)),
        };

        Assert.Equal(3, Gesundheitsstand.Aktuelle(eintraege).Count);
    }

    [Fact]
    public void Aktuell_BeiGleichemDatum_GewinntDerZuletztAngelegte()
    {
        var hund = Guid.NewGuid();
        var frueher = Eintrag(hund, DogHealthKind.Deworming, "Entwurmung", Heute, Heute.AddMonths(3), angelegtVorTagen: 2);
        var spaeter = Eintrag(hund, DogHealthKind.Deworming, "Entwurmung", Heute, Heute.AddMonths(6), angelegtVorTagen: 0);

        Assert.Same(spaeter, Assert.Single(Gesundheitsstand.Aktuelle([frueher, spaeter])));
    }

    [Fact]
    public void Aktuell_TierarzttermineGehoerenNichtDazu()
    {
        var termin = Eintrag(Guid.NewGuid(), DogHealthKind.VetAppointment, "Kontrolle", Heute.AddDays(3));

        Assert.Empty(Gesundheitsstand.Aktuelle([termin]));
    }

    // --- Zusammenfassung --------------------------------------------------------

    [Fact]
    public void Zusammenfassung_ZaehltUeberfaelligeUndBaldFaelligeNurAusDenAktuellen()
    {
        var hund = Guid.NewGuid();
        var eintraege = new[]
        {
            // Die alte Tollwut-Impfung ist abgelaufen, aber erneuert - sie zählt nicht.
            Eintrag(hund, DogHealthKind.Vaccination, "Tollwut", Heute.AddYears(-4), Heute.AddYears(-1)),
            Eintrag(hund, DogHealthKind.Vaccination, "Tollwut", Heute.AddDays(-20), Heute.AddYears(3)),
            Eintrag(hund, DogHealthKind.Deworming, "Entwurmung", Heute.AddMonths(-3), Heute.AddDays(-2)),
            Eintrag(hund, DogHealthKind.ParasiteProtection, "Zeckenschutz", Heute.AddDays(-60), Heute.AddDays(12)),
            Eintrag(hund, DogHealthKind.Vaccination, "SHPPi", Heute.AddDays(-5), Heute.AddYears(2)),
        };

        var stand = Gesundheitsstand.Zusammenfassungen(eintraege, Heute)[hund];

        Assert.Equal(1, stand.Overdue);
        Assert.Equal(1, stand.DueSoon);
        // Der früheste Ablauf zuerst, auch der überfällige.
        Assert.Equal("Entwurmung", stand.NextDue!.Title);
        Assert.Equal(HealthState.Overdue, stand.NextDue.State);
        Assert.Null(stand.NextAppointment);
    }

    [Fact]
    public void Zusammenfassung_NaechsterTermin_HeuteOderSpaeter_FruehesterZuerst_OhneUhrzeitNachDenenMitUhrzeit()
    {
        var hund = Guid.NewGuid();
        var eintraege = new[]
        {
            Eintrag(hund, DogHealthKind.VetAppointment, "Gestern", Heute.AddDays(-1)),
            Eintrag(hund, DogHealthKind.VetAppointment, "Ohne Uhrzeit", Heute.AddDays(2)),
            Eintrag(hund, DogHealthKind.VetAppointment, "Um neun", Heute.AddDays(2), uhrzeit: new TimeOnly(9, 0)),
            Eintrag(hund, DogHealthKind.VetAppointment, "Später", Heute.AddDays(9)),
        };

        var termin = Gesundheitsstand.Zusammenfassungen(eintraege, Heute)[hund].NextAppointment!;

        Assert.Equal("Um neun", termin.Title);
        Assert.Equal(Heute.AddDays(2), termin.Date);
    }

    [Fact]
    public void Zusammenfassung_TerminHeute_ZaehltNochAlsNaechster()
    {
        var hund = Guid.NewGuid();
        var stand = Gesundheitsstand.Zusammenfassungen([Eintrag(hund, DogHealthKind.VetAppointment, "Heute", Heute)], Heute)[hund];

        Assert.Equal("Heute", stand.NextAppointment!.Title);
    }

    [Fact]
    public void Zusammenfassung_NichtsDatiertes_FehltImErgebnis()
    {
        var hund = Guid.NewGuid();
        var eintraege = new[]
        {
            Eintrag(hund, DogHealthKind.Other, "Röntgen", Heute.AddYears(-1)), // keine Frist
            Eintrag(hund, DogHealthKind.VetAppointment, "Vorbei", Heute.AddDays(-4)),
        };

        Assert.Empty(Gesundheitsstand.Zusammenfassungen(eintraege, Heute));
    }

    [Fact]
    public void Zusammenfassung_JeHundGetrennt()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var erg = Gesundheitsstand.Zusammenfassungen(
        [
            Eintrag(a, DogHealthKind.Vaccination, "Tollwut", Heute.AddYears(-1), Heute.AddDays(-1)),
            Eintrag(b, DogHealthKind.Vaccination, "Tollwut", Heute.AddDays(-5), Heute.AddYears(3)),
        ], Heute);

        Assert.Equal(1, erg[a].Overdue);
        Assert.Equal(0, erg[b].Overdue);
        Assert.Equal(HealthState.Ok, erg[b].NextDue!.State);
    }
}
