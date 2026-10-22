using Dogity.Application.Common;
using Dogity.Application.Reminders;
using Dogity.Domain.Notifications;

namespace Dogity.Application.Tests.Reminders;

/// <summary>
/// Wann welche Erinnerung fällig ist - ohne Datenbank, nur Zeitpunkt und Daten.
/// Die Uhrzeiten im Test sind UTC; deutsche Zeit ist im Oktober 2026 bis zum 25.10.
/// 03:00 Uhr (Zeitumstellung) UTC+2, danach UTC+1.
/// </summary>
public class ErinnerungsregelnTests
{
    private static readonly IReadOnlySet<ErinnerungsSchluessel> NichtsGesendet = new HashSet<ErinnerungsSchluessel>();

    private static DateTimeOffset Utc(int tag, int stunde, int minute = 0, int monat = 10) =>
        new(2026, monat, tag, stunde, minute, 0, TimeSpan.Zero);

    private static string De(FaelligeErinnerung e) => Uebersetzung.Rendere(e.Meldung, null);
    private static string En(FaelligeErinnerung e) => Uebersetzung.Rendere(e.Meldung, "en");

    // ---------------------------------------------------------------- Versandfenster

    [Theory]
    [InlineData(22, 5, 59, false)] // 07:59 Uhr
    [InlineData(22, 6, 0, true)]   // 08:00 Uhr
    [InlineData(22, 19, 59, true)] // 21:59 Uhr
    [InlineData(22, 20, 0, false)] // 22:00 Uhr
    [InlineData(22, 22, 30, false)] // 00:30 Uhr am Folgetag
    public void Versandfenster_Ist_Acht_Bis_Einundzwanzig_Uhr_Vereinszeit(int tag, int stunde, int minute, bool erwartet) =>
        Assert.Equal(erwartet, Erinnerungsregeln.ImVersandfenster(Utc(tag, stunde, minute)));

    [Fact]
    public void Versandfenster_Folgt_Der_Winterzeit()
    {
        // Am 26.10. gilt UTC+1: 07:30 UTC sind 08:30 Uhr, 20:30 UTC schon 21:30 Uhr.
        Assert.True(Erinnerungsregeln.ImVersandfenster(Utc(26, 7, 30)));
        Assert.False(Erinnerungsregeln.ImVersandfenster(Utc(26, 6, 59)));
        Assert.True(Erinnerungsregeln.ImVersandfenster(Utc(26, 20, 30)));
        Assert.False(Erinnerungsregeln.ImVersandfenster(Utc(26, 21, 0)));
    }

    [Fact]
    public void FaehrteAngeboten_LeereAuswahlJa_SonstNurMitFaehrtenSportart()
    {
        var faehrte = Guid.NewGuid();
        var bh = Guid.NewGuid();
        var faehrtenSportarten = new HashSet<Guid> { faehrte };

        Assert.True(Erinnerungsregeln.FaehrteAngeboten([], faehrtenSportarten));
        Assert.True(Erinnerungsregeln.FaehrteAngeboten([bh, faehrte], faehrtenSportarten));
        Assert.False(Erinnerungsregeln.FaehrteAngeboten([bh], faehrtenSportarten));
        // Keine Fährten-Sportart im Katalog: eine Auswahl ohne Treffer bleibt ein Nein.
        Assert.False(Erinnerungsregeln.FaehrteAngeboten([bh], new HashSet<Guid>()));
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, 1)]
    [InlineData(DayOfWeek.Tuesday, 2)]
    [InlineData(DayOfWeek.Wednesday, 4)]
    [InlineData(DayOfWeek.Saturday, 32)]
    [InlineData(DayOfWeek.Sunday, 64)]
    public void Wochentag_Bit_Montag_Eins_Sonntag_Vierundsechzig(DayOfWeek tag, int erwartet) =>
        Assert.Equal(erwartet, Erinnerungsregeln.TagesBit(tag));

    // ---------------------------------------------------------------- Termin am Vortag

    private static TerminAdressat Adressat(
        DateTimeOffset beginn, TerminAntwort antwort = TerminAntwort.Offen, Guid? user = null, Guid? termin = null,
        bool abgesagt = false, bool erlaubt = true) =>
        new(termin ?? Guid.NewGuid(), "Welpengruppe", beginn, abgesagt, user ?? Guid.NewGuid(), antwort, erlaubt);

    [Fact]
    public void Termin_MitZusage_SagtDuHastZugesagt()
    {
        // Morgen (23.10.) 18:00 Uhr = 16:00 UTC; jetzt 18:30 Uhr am 22.10.
        var erg = Erinnerungsregeln.TerminErinnerungen(Utc(22, 16, 30), [Adressat(Utc(23, 16), TerminAntwort.Zugesagt)], NichtsGesendet);

        var e = Assert.Single(erg);
        Assert.Equal("Morgen 18:00: Welpengruppe - du hast zugesagt.", De(e));
        Assert.Equal("Tomorrow 18:00: Welpengruppe - you have confirmed.", En(e));
        Assert.Equal("/dashboard", e.Link);
    }

    [Fact]
    public void Termin_OhneAntwort_FragtKommstDu()
    {
        var e = Assert.Single(Erinnerungsregeln.TerminErinnerungen(Utc(22, 16, 30), [Adressat(Utc(23, 16))], NichtsGesendet));

        Assert.Equal("Morgen 18:00: Welpengruppe - kommst du?", De(e));
        Assert.Equal("Tomorrow 18:00: Welpengruppe - are you coming?", En(e));
    }

    [Fact]
    public void Termin_NachAbsageDerPerson_KommtNichts() =>
        Assert.Empty(Erinnerungsregeln.TerminErinnerungen(Utc(22, 16, 30), [Adressat(Utc(23, 16), TerminAntwort.Abgesagt)], NichtsGesendet));

    [Fact]
    public void Termin_AbgesagterTermin_KommtNichts() =>
        Assert.Empty(Erinnerungsregeln.TerminErinnerungen(Utc(22, 16, 30), [Adressat(Utc(23, 16), TerminAntwort.Zugesagt, abgesagt: true)], NichtsGesendet));

    [Fact]
    public void Termin_SchalterAus_KommtNichts() =>
        Assert.Empty(Erinnerungsregeln.TerminErinnerungen(Utc(22, 16, 30), [Adressat(Utc(23, 16), erlaubt: false)], NichtsGesendet));

    [Theory]
    [InlineData(15, 59, false)] // 17:59 Uhr - noch zu früh
    [InlineData(16, 0, true)]   // 18:00 Uhr
    [InlineData(19, 59, true)]  // 21:59 Uhr
    [InlineData(20, 0, false)]  // 22:00 Uhr - Ruhezeit
    public void Termin_KommtErstAbAchtzehnUhrUndNurImFenster(int stunde, int minute, bool erwartet)
    {
        var erg = Erinnerungsregeln.TerminErinnerungen(Utc(22, stunde, minute), [Adressat(Utc(23, 16))], NichtsGesendet);

        Assert.Equal(erwartet, erg.Count == 1);
    }

    [Fact]
    public void Termin_NurDerFolgetag_NichtHeuteNichtUebermorgen()
    {
        var adressaten = new[]
        {
            Adressat(Utc(22, 17)),  // heute 19:00 Uhr
            Adressat(Utc(24, 7)),   // übermorgen
        };

        Assert.Empty(Erinnerungsregeln.TerminErinnerungen(Utc(22, 16, 30), adressaten, NichtsGesendet));
    }

    [Fact]
    public void Termin_ZaehltNachVereinszeit_NichtNachUtc()
    {
        // 00:30 Uhr in der Nacht auf den 23.10. = 22:30 UTC am 22.10.: in UTC noch "heute",
        // in Vereinszeit schon der Folgetag - und umgekehrt 23:30 Uhr heute (21:30 UTC) nicht.
        var nachMitternacht = Adressat(Utc(22, 22, 30));
        var vorMitternacht = Adressat(Utc(22, 21, 30));

        var erg = Erinnerungsregeln.TerminErinnerungen(Utc(22, 16, 30), [nachMitternacht, vorMitternacht], NichtsGesendet);

        var e = Assert.Single(erg);
        Assert.Equal("Morgen 00:30: Welpengruppe - kommst du?", De(e));
    }

    [Fact]
    public void Termin_BereitsGesendet_KommtNichtNochmal()
    {
        var termin = Guid.NewGuid();
        var person = Guid.NewGuid();
        var gesendet = new HashSet<ErinnerungsSchluessel> { new(ReminderType.SessionDayBefore, person, termin, new DateOnly(2026, 10, 23)) };

        var erg = Erinnerungsregeln.TerminErinnerungen(Utc(22, 16, 30), [Adressat(Utc(23, 16), user: person, termin: termin)], gesendet);

        Assert.Empty(erg);
    }

    [Fact]
    public void Termin_AufAnderenTagVerschoben_ErinnertErneut()
    {
        var termin = Guid.NewGuid();
        var person = Guid.NewGuid();
        // Erinnerung kam am Vorabend des ursprünglichen Tags (23.), der Termin liegt jetzt am 24.
        var gesendet = new HashSet<ErinnerungsSchluessel> { new(ReminderType.SessionDayBefore, person, termin, new DateOnly(2026, 10, 23)) };

        var erg = Erinnerungsregeln.TerminErinnerungen(Utc(23, 16, 30), [Adressat(Utc(24, 16), user: person, termin: termin)], gesendet);

        Assert.Single(erg);
    }

    [Fact]
    public void Termin_EinePersonMehrfachInDerListe_BekommtEineErinnerung()
    {
        // Zwei Hunde derselben Person beim selben Termin: die Antwort gilt je Person, die Erinnerung auch.
        var person = Guid.NewGuid();
        var termin = Guid.NewGuid();

        var erg = Erinnerungsregeln.TerminErinnerungen(
            Utc(22, 16, 30), [Adressat(Utc(23, 16), user: person, termin: termin), Adressat(Utc(23, 16), user: person, termin: termin)], NichtsGesendet);

        Assert.Single(erg);
    }

    [Fact]
    public void Termin_ZeitumstellungAm25Oktober_Folgetag24h_Und_25h()
    {
        // Samstag 24.10., 18:30 Uhr (CEST): Folgetag ist Sonntag, der 25-Stunden-Tag. Der Termin
        // um 23:30 Uhr (CET, 22:30 UTC) gehört noch dazu, der um 00:30 Uhr am Montag nicht.
        var erg = Erinnerungsregeln.TerminErinnerungen(
            Utc(24, 16, 30), [Adressat(Utc(25, 22, 30)), Adressat(Utc(25, 23, 30))], NichtsGesendet);

        var e = Assert.Single(erg);
        Assert.Equal("Morgen 23:30: Welpengruppe - kommst du?", De(e));
    }

    [Fact]
    public void Termin_NachDerZeitumstellung_Montag0800()
    {
        // Sonntag 25.10., 18:30 Uhr (CET = 17:30 UTC); Montag 08:00 Uhr CET = 07:00 UTC.
        var e = Assert.Single(Erinnerungsregeln.TerminErinnerungen(Utc(25, 17, 30), [Adressat(Utc(26, 7))], NichtsGesendet));

        Assert.Equal("Morgen 08:00: Welpengruppe - kommst du?", De(e));
    }

    // ---------------------------------------------------------------- Fährte nicht abgelaufen

    private static FaehrteKandidat Faehrte(
        DateTimeOffset? ende, bool abgelaufen = false, bool pausiert = false, bool erlaubt = true, Guid? id = null, Guid? user = null) =>
        new(id ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Bello", user ?? Guid.NewGuid(), ende, abgelaufen, pausiert, erlaubt);

    [Fact]
    public void Faehrte_NachDreiStunden_KommtEineErinnerungMitFrist()
    {
        // Ende des Legens 09:00 Uhr (07:00 UTC); jetzt 12:00 Uhr (10:00 UTC): genau drei Stunden.
        var kandidat = Faehrte(Utc(22, 7));

        var e = Assert.Single(Erinnerungsregeln.FaehrtenErinnerungen(Utc(22, 10), [kandidat], NichtsGesendet));

        Assert.Equal("Die Fährte von Bello ist noch nicht abgelaufen. Du kannst sie noch bis 23.10.2026, 09:00 Uhr ablaufen.", De(e));
        Assert.Equal("The track for Bello has not been walked yet. You can still walk it until 09:00 on 23 Oct 2026.", En(e));
        Assert.Equal($"/dogs/{kandidat.HundId}?eintrag={kandidat.TrainingId}", e.Link);
        Assert.Equal(ReminderType.TrackNotWalked, e.Schluessel.Typ);
        Assert.Equal(kandidat.FaehrteId, e.Schluessel.BezugId);
        Assert.Equal(SentReminder.KeinTag, e.Schluessel.Tag);
    }

    [Fact]
    public void Faehrte_VorDreiStunden_NochNicht() =>
        Assert.Empty(Erinnerungsregeln.FaehrtenErinnerungen(Utc(22, 9, 59), [Faehrte(Utc(22, 7))], NichtsGesendet));

    [Fact]
    public void Faehrte_BisZumFristende_DannNichtMehr()
    {
        var kandidat = Faehrte(Utc(22, 7));

        Assert.Single(Erinnerungsregeln.FaehrtenErinnerungen(Utc(23, 7), [kandidat], NichtsGesendet)); // genau 24 Stunden
        Assert.Empty(Erinnerungsregeln.FaehrtenErinnerungen(Utc(23, 7, 1), [kandidat], NichtsGesendet));
    }

    [Fact]
    public void Faehrte_InDerRuhezeitFaellig_KommtAmNaechstenMorgenWennDieFristLaeuft()
    {
        // Ende 19:00 Uhr (17:00 UTC), fällig ab 22:00 Uhr - Ruhezeit. Um 22:30 Uhr nichts,
        // am nächsten Morgen um 08:10 Uhr (06:10 UTC) sind es 13 Stunden: die Frist läuft noch.
        var kandidat = Faehrte(Utc(22, 17));

        Assert.Empty(Erinnerungsregeln.FaehrtenErinnerungen(Utc(22, 20, 30), [kandidat], NichtsGesendet));
        Assert.Single(Erinnerungsregeln.FaehrtenErinnerungen(Utc(23, 6, 10), [kandidat], NichtsGesendet));
    }

    [Fact]
    public void Faehrte_InDerRuhezeitFaellig_WirdNichtNachgeholtWennDieFristVorbeiIst()
    {
        // Ende 07:00 Uhr: fällig 10:00 Uhr (im Fenster). Wäre der Lauf ausgefallen, käme sie am
        // nächsten Morgen um 08:10 Uhr nach 25 Stunden - zu spät, die Fährte lässt sich nicht mehr ablaufen.
        Assert.Empty(Erinnerungsregeln.FaehrtenErinnerungen(Utc(23, 6, 10), [Faehrte(Utc(22, 5))], NichtsGesendet));
    }

    [Fact]
    public void Faehrte_AblaufSchonGespeichert_HundPausiert_SchalterAus_OhneEnde_KommtNichts()
    {
        var jetzt = Utc(22, 10);
        var ende = Utc(22, 7);

        Assert.Empty(Erinnerungsregeln.FaehrtenErinnerungen(jetzt, [Faehrte(ende, abgelaufen: true)], NichtsGesendet));
        Assert.Empty(Erinnerungsregeln.FaehrtenErinnerungen(jetzt, [Faehrte(ende, pausiert: true)], NichtsGesendet));
        Assert.Empty(Erinnerungsregeln.FaehrtenErinnerungen(jetzt, [Faehrte(ende, erlaubt: false)], NichtsGesendet));
        Assert.Empty(Erinnerungsregeln.FaehrtenErinnerungen(jetzt, [Faehrte(null)], NichtsGesendet));
    }

    [Fact]
    public void Faehrte_BereitsGesendet_KommtNichtNochmal()
    {
        var kandidat = Faehrte(Utc(22, 7));
        var gesendet = new HashSet<ErinnerungsSchluessel>
        {
            new(ReminderType.TrackNotWalked, kandidat.UserId, kandidat.FaehrteId, SentReminder.KeinTag)
        };

        Assert.Empty(Erinnerungsregeln.FaehrtenErinnerungen(Utc(22, 10), [kandidat], gesendet));
    }

    // ---------------------------------------------------------------- Rückmeldungen der Trainer:innen

    private static TrainerKandidat Trainer(int tage = 64, int stunde = 18, bool erlaubt = true, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), erlaubt, tage, stunde);

    // Sonntag 25.10.2026 ist der Tag der Zeitumstellung: 18:00 Uhr ist dort 17:00 UTC (CET).
    private static readonly DateTimeOffset SonntagAbends = Utc(25, 17, 10);

    [Fact]
    public void Trainer_Vorgabe_SonntagAchtzehnUhr()
    {
        var k = Trainer();

        Assert.Single(Erinnerungsregeln.FaelligeTrainer(SonntagAbends, [k], NichtsGesendet));
        Assert.Empty(Erinnerungsregeln.FaelligeTrainer(Utc(25, 16, 50), [k], NichtsGesendet)); // 17:50 Uhr
        Assert.Empty(Erinnerungsregeln.FaelligeTrainer(Utc(24, 16, 10), [k], NichtsGesendet)); // Samstag 18:10 Uhr
    }

    [Fact]
    public void Trainer_MehrereTage_NurGewaehlteTage()
    {
        // Montag (1) und Mittwoch (4), 9 Uhr.
        var k = Trainer(tage: 1 | 4, stunde: 9);

        Assert.Single(Erinnerungsregeln.FaelligeTrainer(Utc(26, 8, 10), [k], NichtsGesendet)); // Montag 09:10 Uhr (CET)
        Assert.Empty(Erinnerungsregeln.FaelligeTrainer(Utc(27, 8, 10), [k], NichtsGesendet));  // Dienstag
        Assert.Single(Erinnerungsregeln.FaelligeTrainer(Utc(28, 8, 10), [k], NichtsGesendet)); // Mittwoch
    }

    [Fact]
    public void Trainer_FaelligSobaldDieStundeErreichtIst_AuchSpaeterAmTag()
    {
        var k = Trainer(stunde: 9);

        Assert.Empty(Erinnerungsregeln.FaelligeTrainer(Utc(25, 7, 50), [k], NichtsGesendet)); // 08:50 Uhr
        Assert.Single(Erinnerungsregeln.FaelligeTrainer(Utc(25, 14, 0), [k], NichtsGesendet)); // 15:00 Uhr, z. B. nach einem Ausfall
    }

    [Fact]
    public void Trainer_EinundzwanzigUhr_KommtNochAn_NachDemFensterNicht()
    {
        var k = Trainer(stunde: 21);

        Assert.Single(Erinnerungsregeln.FaelligeTrainer(Utc(25, 20, 10), [k], NichtsGesendet)); // 21:10 Uhr
        Assert.Empty(Erinnerungsregeln.FaelligeTrainer(Utc(25, 21, 0), [k], NichtsGesendet));   // 22:00 Uhr
    }

    [Fact]
    public void Trainer_SchalterAus_KommtNichts() =>
        Assert.Empty(Erinnerungsregeln.FaelligeTrainer(SonntagAbends, [Trainer(erlaubt: false)], NichtsGesendet));

    [Fact]
    public void Trainer_SchonHeuteGesendet_AuchBeiGeaenderterUhrzeitKeinZweites()
    {
        // Um 18 Uhr gesendet; danach stellt die Person 19 Uhr ein (oder 17 Uhr): am selben Tag nichts mehr.
        var id = Guid.NewGuid();
        var gesendet = new HashSet<ErinnerungsSchluessel> { Erinnerungsregeln.TrainerSchluessel(id, new DateOnly(2026, 10, 25)) };

        Assert.Empty(Erinnerungsregeln.FaelligeTrainer(Utc(25, 18, 10), [Trainer(stunde: 19, id: id)], gesendet));
        Assert.Empty(Erinnerungsregeln.FaelligeTrainer(Utc(25, 18, 10), [Trainer(stunde: 17, id: id)], gesendet));
    }

    [Fact]
    public void Trainer_GesternGesendet_HeuteWiederFaellig()
    {
        var id = Guid.NewGuid();
        var gesendet = new HashSet<ErinnerungsSchluessel> { Erinnerungsregeln.TrainerSchluessel(id, new DateOnly(2026, 10, 24)) };

        Assert.Single(Erinnerungsregeln.FaelligeTrainer(SonntagAbends, [Trainer(id: id)], gesendet));
    }

    [Fact]
    public void Trainer_DerTagZaehltInVereinszeit()
    {
        // Samstag 24.10., 23:30 UTC = Sonntag 00:30 Uhr (CEST): Ruhezeit, nichts. Um 18:10 Uhr
        // an dem 25-Stunden-Sonntag (17:10 UTC) dagegen ja - mit dem Schlüssel des 25.10.
        var k = Trainer();
        Assert.Empty(Erinnerungsregeln.FaelligeTrainer(Utc(24, 22, 30), [k], NichtsGesendet));

        var faellig = Assert.Single(Erinnerungsregeln.FaelligeTrainer(SonntagAbends, [k], NichtsGesendet));
        var erinnerung = Erinnerungsregeln.RueckmeldungsErinnerung(SonntagAbends, faellig.UserId, 2)!;
        Assert.Equal(new DateOnly(2026, 10, 25), erinnerung.Schluessel.Tag);
    }

    [Fact]
    public void Rueckmeldungen_NullOffen_KommtNichts() =>
        Assert.Null(Erinnerungsregeln.RueckmeldungsErinnerung(SonntagAbends, Guid.NewGuid(), 0));

    [Fact]
    public void Rueckmeldungen_Einzahl()
    {
        var e = Erinnerungsregeln.RueckmeldungsErinnerung(SonntagAbends, Guid.NewGuid(), 1)!;

        Assert.Equal("Ein Training wartet auf deine Rückmeldung.", De(e));
        Assert.Equal("One training session is waiting for your feedback.", En(e));
        Assert.Equal("/trainer/bewerten", e.Link);
    }

    [Fact]
    public void Rueckmeldungen_Mehrzahl()
    {
        var e = Erinnerungsregeln.RueckmeldungsErinnerung(SonntagAbends, Guid.NewGuid(), 7)!;

        Assert.Equal("7 Trainings warten auf deine Rückmeldung.", De(e));
        Assert.Equal("7 training sessions are waiting for your feedback.", En(e));
    }

    // ---------------------------------------------------------------- Einstellungen

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(127, true)]
    [InlineData(128, false)]
    [InlineData(-1, false)]
    public void GueltigeTage_EinsBisHundertsiebenundzwanzig(int maske, bool erwartet) =>
        Assert.Equal(erwartet, Erinnerungsregeln.GueltigeTage(maske));

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(21, true)]
    [InlineData(22, false)]
    public void GueltigeStunde_AchtBisEinundzwanzig(int stunde, bool erwartet) =>
        Assert.Equal(erwartet, Erinnerungsregeln.GueltigeStunde(stunde));
}
