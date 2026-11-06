using Dogity.Application.Account;
using Dogity.Application.Common;
using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Arbeitseinsätze des Vereins (Paket A1, siehe docs/BERECHTIGUNGEN.md): Die Verwaltung schreibt, Verwaltung und
/// Vorstand lesen, jedes Mitglied sieht nur die eigenen Einsätze. Dazu die Grenzen der Eingaben, die
/// Jahresauswertung, das Lösen von der Person bei Austritt und Kontolöschung, die automatische Löschung, der
/// Datenexport und die Monatssummen der Vereinsübersicht.
/// </summary>
public class ClubWorkTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Heute = new(2026, 10, 20);

    private sealed class FesteUhr : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    private sealed record Welt(
        ApplicationDbContext Db,
        FakeUserLookupService Nutzer,
        FakeNotificationService Meldungen,
        ClubWorkService Dienst,
        Club Verein,
        Guid Verwaltung,
        Guid Max,
        Guid Anna);

    private static async Task<Welt> AufbauenAsync()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = new FakeUserLookupService();
        var verein = new Club { Name = "Hundesportverein Nord" };
        db.Clubs.Add(verein);
        var verwaltung = Guid.NewGuid();
        nutzer.Register(verwaltung, "verwaltung@example.org", "Vera", "Verwaltung");
        db.ClubTrainers.Add(new ClubTrainer { ClubId = verein.Id, UserId = verwaltung, Role = ClubRole.Verwaltung });
        var max = Guid.NewGuid();
        nutzer.Register(max, "max@example.org", "Max", "Mustermann");
        var anna = Guid.NewGuid();
        nutzer.Register(anna, "anna@example.org", "Anna", "Abel");
        db.ClubMemberships.AddRange(
            new ClubMembership { ClubId = verein.Id, UserId = max, Status = ClubMembershipStatus.Approved },
            new ClubMembership { ClubId = verein.Id, UserId = anna, Status = ClubMembershipStatus.Approved });
        await db.SaveChangesAsync();
        return new Welt(db, nutzer, new FakeNotificationService(), new ClubWorkService(db, nutzer, new FesteUhr()), verein, verwaltung, max, anna);
    }

    private static SaveClubWorkEventRequest Einsatz(DateOnly? datum = null, string titel = "Platzpflege", int minuten = 120, string? notiz = null) =>
        new(datum ?? Heute, titel, notiz, minuten);

    private static async Task<Guid> AnlegenAsync(Welt w, DateOnly? datum = null, string titel = "Platzpflege", int minuten = 120)
    {
        var einsatz = await w.Dienst.CreateAsync(w.Verwaltung, w.Verein.Id, Einsatz(datum, titel, minuten));
        Assert.True(einsatz.Succeeded, string.Join(";", einsatz.Errors));
        return einsatz.Value!.Id;
    }

    private static async Task AbhakenAsync(Welt w, Guid einsatz, params Guid[] personen)
    {
        var ergebnis = await w.Dienst.AddParticipantsAsync(w.Verwaltung, w.Verein.Id, einsatz, new AddClubWorkParticipantsRequest(personen));
        Assert.True(ergebnis.Succeeded, string.Join(";", ergebnis.Errors));
    }

    private static async Task<Guid> TeilnahmeAsync(Welt w, Guid einsatz, Guid person) =>
        (await w.Db.ClubWorkParticipations.AsNoTracking().SingleAsync(p => p.ClubWorkEventId == einsatz && p.UserId == person)).Id;

    private static ClubService Vereine(Welt w) => new(w.Db, w.Nutzer, w.Meldungen, new TrainerRoleService(w.Db, w.Nutzer));

    // ------------------------------------------------------------ Rechte

    /// <summary>Rolle, darf lesen, darf schreiben.</summary>
    public static TheoryData<ClubRole, bool, bool> Zugriff => new()
    {
        { 0, false, false },
        { ClubRole.Training, false, false },
        { ClubRole.Ausbildungswart, false, false },
        { ClubRole.Mitgliederverwaltung, false, false },
        { ClubRole.Training | ClubRole.Ausbildungswart | ClubRole.Mitgliederverwaltung, false, false },
        { ClubRole.Vorstand, true, false },
        { ClubRole.Vorstand | ClubRole.Training, true, false },
        { ClubRole.Verwaltung, true, true },
        { ClubRole.Verwaltung | ClubRole.Vorstand, true, true },
    };

    [Theory]
    [MemberData(nameof(Zugriff))]
    public async Task Lesen_NurVerwaltungUndVorstand_Schreiben_NurVerwaltung(ClubRole rollen, bool lesen, bool schreiben)
    {
        var w = await AufbauenAsync();
        var einsatz = await AnlegenAsync(w);
        await AbhakenAsync(w, einsatz, w.Max);
        var teilnahme = await TeilnahmeAsync(w, einsatz, w.Max);

        var person = Guid.NewGuid();
        w.Nutzer.Register(person, "p@example.org", "Pia", "Person");
        if (rollen != 0) w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = w.Verein.Id, UserId = person, Role = rollen });
        await w.Db.SaveChangesAsync();
        var id = w.Verein.Id;

        var lesend = new List<Result>
        {
            await w.Dienst.GetListAsync(person, id),
            await w.Dienst.GetAsync(person, id, einsatz),
            await w.Dienst.GetSummaryAsync(person, id, 2026),
        };
        var schreibend = new List<Result>
        {
            await w.Dienst.CreateAsync(person, id, Einsatz()),
            await w.Dienst.UpdateAsync(person, id, einsatz, Einsatz()),
            await w.Dienst.GetCandidatesAsync(person, id),
            await w.Dienst.AddParticipantsAsync(person, id, einsatz, new AddClubWorkParticipantsRequest([w.Anna])),
            await w.Dienst.SetMinutesAsync(person, id, einsatz, teilnahme, new SetClubWorkMinutesRequest(30)),
            await w.Dienst.RemoveParticipantAsync(person, id, einsatz, teilnahme),
            await w.Dienst.DeleteAsync(person, id, einsatz),
        };

        foreach (var ergebnis in lesend) Assert.Equal(!lesen, ergebnis.IsNotFound);
        foreach (var ergebnis in schreibend)
        {
            Assert.Equal(!schreiben, ergebnis.IsNotFound);
            if (!schreiben) Assert.False(ergebnis.Succeeded);
        }
        // Ohne Recht ist nichts passiert: Der Einsatz mit seiner Helferin steht noch da.
        if (!schreiben)
        {
            Assert.Equal(1, await w.Db.ClubWorkEvents.CountAsync());
            Assert.Equal(1, await w.Db.ClubWorkParticipations.CountAsync());
        }
    }

    [Fact]
    public async Task FremderVerein_IstNichtGefunden_AuchMitVerwaltungDortUndMitFremderEinsatzId()
    {
        var w = await AufbauenAsync();
        var einsatz = await AnlegenAsync(w);
        var anderer = new Club { Name = "Anderer Verein" };
        w.Db.Clubs.Add(anderer);
        var fremde = Guid.NewGuid();
        w.Nutzer.Register(fremde, "f@example.org", "Fred", "Fremd");
        w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = anderer.Id, UserId = fremde, Role = ClubRole.Verwaltung | ClubRole.Vorstand });
        await w.Db.SaveChangesAsync();

        // Weder der eigene Verein noch ein Einsatz des anderen Vereins unter der eigenen Vereins-Id.
        Assert.True((await w.Dienst.GetListAsync(fremde, w.Verein.Id)).IsNotFound);
        Assert.True((await w.Dienst.GetAsync(fremde, anderer.Id, einsatz)).IsNotFound);
        Assert.True((await w.Dienst.DeleteAsync(fremde, anderer.Id, einsatz)).IsNotFound);
        Assert.True((await w.Dienst.GetMineAsync(fremde, w.Verein.Id)).IsNotFound);
        Assert.Equal(1, await w.Db.ClubWorkEvents.CountAsync());
    }

    [Fact]
    public async Task Mitglied_SiehtNurDieEigenenEinsaetzeUndDieJahressumme()
    {
        var w = await AufbauenAsync();
        var heute = await AnlegenAsync(w, Heute, "Platzpflege", 120);
        var vorjahr = await AnlegenAsync(w, new DateOnly(2025, 5, 3), "Frühjahrsputz", 60);
        await AbhakenAsync(w, heute, w.Max, w.Anna);
        await AbhakenAsync(w, vorjahr, w.Max);
        await w.Dienst.SetMinutesAsync(w.Verwaltung, w.Verein.Id, heute, await TeilnahmeAsync(w, heute, w.Max), new SetClubWorkMinutesRequest(90));

        var max = (await w.Dienst.GetMineAsync(w.Max, w.Verein.Id)).Value!;
        var anna = (await w.Dienst.GetMineAsync(w.Anna, w.Verein.Id)).Value!;

        Assert.Equal((2026, 1, 90), (max.Year, max.YearEventCount, max.YearMinutes));
        Assert.Equal([("Platzpflege", 90), ("Frühjahrsputz", 60)], max.Items.Select(i => (i.Title, i.Minutes)));
        Assert.Equal((1, 120), (anna.YearEventCount, anna.YearMinutes));
        Assert.Equal("Platzpflege", Assert.Single(anna.Items).Title);
        // Ohne Einsätze: eine leere Karte, kein Fehler.
        var ohne = Guid.NewGuid();
        w.Nutzer.Register(ohne, "o@example.org", "Olaf", "Ohne");
        w.Db.ClubMemberships.Add(new ClubMembership { ClubId = w.Verein.Id, UserId = ohne, Status = ClubMembershipStatus.Approved });
        await w.Db.SaveChangesAsync();
        Assert.Empty((await w.Dienst.GetMineAsync(ohne, w.Verein.Id)).Value!.Items);
    }

    [Fact]
    public async Task Mitglied_EinerAnderenOderOffenenMitgliedschaft_SiehtNichts()
    {
        var w = await AufbauenAsync();
        var bewerber = Guid.NewGuid();
        w.Nutzer.Register(bewerber, "b@example.org", "Bea", "Bewerberin");
        w.Db.ClubMemberships.Add(new ClubMembership { ClubId = w.Verein.Id, UserId = bewerber, Status = ClubMembershipStatus.Pending });
        await w.Db.SaveChangesAsync();

        Assert.True((await w.Dienst.GetMineAsync(bewerber, w.Verein.Id)).IsNotFound);
        Assert.True((await w.Dienst.GetMineAsync(Guid.NewGuid(), w.Verein.Id)).IsNotFound);
    }

    [Theory]
    [InlineData(ClubRole.Training, false, false)]
    [InlineData(ClubRole.Vorstand, false, true)]
    [InlineData(ClubRole.Verwaltung, true, true)]
    public async Task Vereinsliste_MeldetDieRechteFuerArbeitseinsaetze(ClubRole rollen, bool schreibt, bool liest)
    {
        var w = await AufbauenAsync();
        var person = Guid.NewGuid();
        w.Nutzer.Register(person, "p@example.org", "Pia", "Person");
        w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = w.Verein.Id, UserId = person, Role = rollen });
        await w.Db.SaveChangesAsync();

        var club = (await Vereine(w).GetMyClubRolesAsync(person)).Value!.Single();

        Assert.Equal((schreibt, liest), (club.CanManageWork, club.CanViewWork));
    }

    // ------------------------------------------------------------ Eingaben

    [Fact]
    public async Task Einsatz_TitelDauerUndNotizHabenGrenzen()
    {
        var w = await AufbauenAsync();
        var id = w.Verein.Id;
        async Task<Result> Anlegen(SaveClubWorkEventRequest r) => await w.Dienst.CreateAsync(w.Verwaltung, id, r);

        Assert.False((await Anlegen(Einsatz(titel: ""))).Succeeded);
        Assert.False((await Anlegen(Einsatz(titel: "   "))).Succeeded);
        Assert.False((await Anlegen(Einsatz(titel: new string('x', Textlaengen.ArbeitseinsatzTitel + 1)))).Succeeded);
        Assert.True((await Anlegen(Einsatz(titel: new string('x', Textlaengen.ArbeitseinsatzTitel)))).Succeeded);
        Assert.False((await Anlegen(Einsatz(notiz: new string('x', Textlaengen.ArbeitseinsatzNotiz + 1)))).Succeeded);
        Assert.True((await Anlegen(Einsatz(notiz: new string('x', Textlaengen.ArbeitseinsatzNotiz)))).Succeeded);
        Assert.False((await Anlegen(Einsatz(minuten: ClubWorkService.MinMinuten - 1))).Succeeded);
        Assert.False((await Anlegen(Einsatz(minuten: ClubWorkService.MaxMinuten + 1))).Succeeded);
        Assert.True((await Anlegen(Einsatz(minuten: ClubWorkService.MinMinuten))).Succeeded);
        Assert.True((await Anlegen(Einsatz(minuten: ClubWorkService.MaxMinuten))).Succeeded);
        Assert.False((await Anlegen(new SaveClubWorkEventRequest(default, "Titel", null, 60))).Succeeded);
        // Fachliche Fehler sind keine 404.
        Assert.False((await Anlegen(Einsatz(titel: ""))).IsNotFound);
    }

    [Fact]
    public async Task Einsatz_DatumHoechstens30TageInDerZukunft_UndNichtVorDerAufbewahrungsgrenze()
    {
        var w = await AufbauenAsync();
        var id = w.Verein.Id;
        Task<Result<ClubWorkEventDto>> Anlegen(DateOnly datum) => w.Dienst.CreateAsync(w.Verwaltung, id, Einsatz(datum));

        Assert.True((await Anlegen(Heute.AddDays(30))).Succeeded);
        Assert.False((await Anlegen(Heute.AddDays(31))).Succeeded);
        // Heute 2026: ab dem 01.01.2024 wird aufbewahrt.
        Assert.True((await Anlegen(new DateOnly(2024, 1, 1))).Succeeded);
        Assert.False((await Anlegen(new DateOnly(2023, 12, 31))).Succeeded);
    }

    [Fact]
    public async Task Einsatz_Bearbeiten_UebernimmtDieWerte_Trimmt_UndLeereNotizWirdNull()
    {
        var w = await AufbauenAsync();
        var einsatz = await AnlegenAsync(w);

        var ergebnis = await w.Dienst.UpdateAsync(w.Verwaltung, w.Verein.Id, einsatz,
            new SaveClubWorkEventRequest(Heute.AddDays(-1), "  Aufbau Prüfungstag ", "   ", 180));

        var dto = ergebnis.Value!;
        Assert.Equal((Heute.AddDays(-1), "Aufbau Prüfungstag", null, 180), (dto.Date, dto.Title, dto.Note, dto.DurationMinutes));
        Assert.Equal(Jetzt, (await w.Db.ClubWorkEvents.AsNoTracking().SingleAsync()).UpdatedAt);
    }

    [Fact]
    public async Task Einsatz_MitHelfern_LaesstSichNichtInDieZukunftVerschieben()
    {
        var w = await AufbauenAsync();
        var einsatz = await AnlegenAsync(w);
        await AbhakenAsync(w, einsatz, w.Max);

        var ergebnis = await w.Dienst.UpdateAsync(w.Verwaltung, w.Verein.Id, einsatz, Einsatz(Heute.AddDays(3)));

        Assert.False(ergebnis.Succeeded);
        Assert.False(ergebnis.IsNotFound);
        Assert.Equal(Heute, (await w.Db.ClubWorkEvents.AsNoTracking().SingleAsync()).Date);
    }

    // ------------------------------------------------------------ Abhaken

    [Fact]
    public async Task Abhaken_VorDemTagDesEinsatzes_GehtNicht_AmTagSelbstSchon()
    {
        var w = await AufbauenAsync();
        var morgen = await AnlegenAsync(w, Heute.AddDays(1));
        var heute = await AnlegenAsync(w, Heute);

        var zuFrueh = await w.Dienst.AddParticipantsAsync(w.Verwaltung, w.Verein.Id, morgen, new AddClubWorkParticipantsRequest([w.Max]));
        var rechtzeitig = await w.Dienst.AddParticipantsAsync(w.Verwaltung, w.Verein.Id, heute, new AddClubWorkParticipantsRequest([w.Max]));

        Assert.False(zuFrueh.Succeeded);
        Assert.False(zuFrueh.IsNotFound);
        Assert.True(rechtzeitig.Succeeded);
        Assert.Equal(heute, (await w.Db.ClubWorkParticipations.AsNoTracking().SingleAsync()).ClubWorkEventId);
    }

    [Fact]
    public async Task Abhaken_NurPersonenDesVereins_MitgliederUndRollen_NichtAndere()
    {
        var w = await AufbauenAsync();
        var einsatz = await AnlegenAsync(w);
        var nurRolle = Guid.NewGuid();
        w.Nutzer.Register(nurRolle, "rolle@example.org", "Rolf", "Rolle");
        w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = w.Verein.Id, UserId = nurRolle, Role = ClubRole.Training });
        var bewerber = Guid.NewGuid();
        w.Nutzer.Register(bewerber, "b@example.org", "Bea", "Bewerberin");
        w.Db.ClubMemberships.Add(new ClubMembership { ClubId = w.Verein.Id, UserId = bewerber, Status = ClubMembershipStatus.Pending });
        var fremder = Guid.NewGuid();
        w.Nutzer.Register(fremder, "f@example.org", "Fred", "Fremd");
        await w.Db.SaveChangesAsync();
        var id = w.Verein.Id;

        Assert.True((await w.Dienst.AddParticipantsAsync(w.Verwaltung, id, einsatz, new AddClubWorkParticipantsRequest([w.Max, nurRolle]))).Succeeded);
        // Eine ungeeignete Person stoppt die ganze Anfrage: nichts wird halb abgehakt.
        Assert.False((await w.Dienst.AddParticipantsAsync(w.Verwaltung, id, einsatz, new AddClubWorkParticipantsRequest([w.Anna, bewerber]))).Succeeded);
        Assert.False((await w.Dienst.AddParticipantsAsync(w.Verwaltung, id, einsatz, new AddClubWorkParticipantsRequest([fremder]))).Succeeded);
        Assert.False((await w.Dienst.AddParticipantsAsync(w.Verwaltung, id, einsatz, new AddClubWorkParticipantsRequest([]))).Succeeded);
        Assert.False((await w.Dienst.AddParticipantsAsync(w.Verwaltung, id, einsatz, new AddClubWorkParticipantsRequest(null))).Succeeded);

        Assert.Equivalent(new[] { w.Max, nurRolle }, await w.Db.ClubWorkParticipations.Select(p => p.UserId!.Value).ToListAsync());
    }

    [Fact]
    public async Task Abhaken_IstIdempotent_EineDoppelteAnfrageLegtKeineZweiteZeileAn()
    {
        var w = await AufbauenAsync();
        var einsatz = await AnlegenAsync(w);

        await AbhakenAsync(w, einsatz, w.Max, w.Max);
        await AbhakenAsync(w, einsatz, w.Max, w.Anna);

        Assert.Equal(2, await w.Db.ClubWorkParticipations.CountAsync());
        var teilnahme = await w.Db.ClubWorkParticipations.AsNoTracking().FirstAsync(p => p.UserId == w.Max);
        Assert.Equal((w.Verwaltung, null), (teilnahme.RecordedByUserId, teilnahme.Minutes));
        Assert.Equal(Jetzt, teilnahme.RecordedAt);
    }

    [Fact]
    public async Task Abhaken_BenachrichtigtNiemanden()
    {
        var w = await AufbauenAsync();
        var einsatz = await AnlegenAsync(w);

        await AbhakenAsync(w, einsatz, w.Max, w.Anna);

        Assert.Empty(w.Meldungen.Created);
    }

    [Fact]
    public async Task Minuten_AbweichendSetzen_GrenzenPruefen_GleichDerDauerHeisstWieAlle()
    {
        var w = await AufbauenAsync();
        var einsatz = await AnlegenAsync(w, Heute, minuten: 120);
        await AbhakenAsync(w, einsatz, w.Max);
        var teilnahme = await TeilnahmeAsync(w, einsatz, w.Max);
        Task<Result> Setzen(int? minuten) =>
            w.Dienst.SetMinutesAsync(w.Verwaltung, w.Verein.Id, einsatz, teilnahme, new SetClubWorkMinutesRequest(minuten));

        Assert.False((await Setzen(ClubWorkService.MinMinuten - 1)).Succeeded);
        Assert.False((await Setzen(ClubWorkService.MaxMinuten + 1)).Succeeded);
        Assert.Null((await w.Db.ClubWorkParticipations.AsNoTracking().SingleAsync()).Minutes);

        Assert.True((await Setzen(45)).Succeeded);
        var dto = (await w.Dienst.GetAsync(w.Verwaltung, w.Verein.Id, einsatz)).Value!.Participants.Single();
        Assert.Equal((45, true), (dto.Minutes, dto.Custom));

        // Wieder die Dauer des Einsatzes: Die Person folgt künftig dessen Dauer.
        Assert.True((await Setzen(120)).Succeeded);
        Assert.Null((await w.Db.ClubWorkParticipations.AsNoTracking().SingleAsync()).Minutes);
        Assert.True((await Setzen(45)).Succeeded);
        Assert.True((await Setzen(null)).Succeeded);
        dto = (await w.Dienst.GetAsync(w.Verwaltung, w.Verein.Id, einsatz)).Value!.Participants.Single();
        Assert.Equal((120, false), (dto.Minutes, dto.Custom));
    }

    [Fact]
    public async Task Minuten_FremdeTeilnahme_EinesAnderenEinsatzes_IstNichtGefunden()
    {
        var w = await AufbauenAsync();
        var a = await AnlegenAsync(w);
        var b = await AnlegenAsync(w, Heute.AddDays(-1));
        await AbhakenAsync(w, a, w.Max);
        var teilnahme = await TeilnahmeAsync(w, a, w.Max);

        Assert.True((await w.Dienst.SetMinutesAsync(w.Verwaltung, w.Verein.Id, b, teilnahme, new SetClubWorkMinutesRequest(30))).IsNotFound);
        Assert.True((await w.Dienst.RemoveParticipantAsync(w.Verwaltung, w.Verein.Id, b, teilnahme)).IsNotFound);
        Assert.Equal(1, await w.Db.ClubWorkParticipations.CountAsync());
    }

    [Fact]
    public async Task Austragen_LoeschtDieTeilnahmeEndgueltig()
    {
        var w = await AufbauenAsync();
        var einsatz = await AnlegenAsync(w);
        await AbhakenAsync(w, einsatz, w.Max, w.Anna);

        Assert.True((await w.Dienst.RemoveParticipantAsync(w.Verwaltung, w.Verein.Id, einsatz, await TeilnahmeAsync(w, einsatz, w.Max))).Succeeded);

        var uebrig = await w.Db.ClubWorkParticipations.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(w.Anna, uebrig.UserId);
    }

    [Fact]
    public async Task Kandidaten_SindMitgliederUndRollen_NachNamenSortiert()
    {
        var w = await AufbauenAsync();
        var bewerber = Guid.NewGuid();
        w.Nutzer.Register(bewerber, "b@example.org", "Bea", "Bewerberin");
        w.Db.ClubMemberships.Add(new ClubMembership { ClubId = w.Verein.Id, UserId = bewerber, Status = ClubMembershipStatus.Pending });
        await w.Db.SaveChangesAsync();

        var liste = (await w.Dienst.GetCandidatesAsync(w.Verwaltung, w.Verein.Id)).Value!;

        Assert.Equal(["Abel", "Mustermann", "Verwaltung"], liste.Select(c => c.LastName));
    }

    // ------------------------------------------------------------ Löschen

    [Fact]
    public async Task EinsatzLoeschen_NimmtDieTeilnahmenEndgueltigMit_DerEinsatzVerschwindetAusDenListen()
    {
        var w = await AufbauenAsync();
        var einsatz = await AnlegenAsync(w);
        var bleibt = await AnlegenAsync(w, Heute.AddDays(-2), "Frühjahrsputz");
        await AbhakenAsync(w, einsatz, w.Max, w.Anna);
        await AbhakenAsync(w, bleibt, w.Max);

        Assert.True((await w.Dienst.DeleteAsync(w.Verwaltung, w.Verein.Id, einsatz)).Succeeded);

        Assert.Equal(1, await w.Db.ClubWorkParticipations.IgnoreQueryFilters().CountAsync());
        Assert.NotNull((await w.Db.ClubWorkEvents.IgnoreQueryFilters().SingleAsync(e => e.Id == einsatz)).DeletedAt);
        Assert.Equal(bleibt, Assert.Single((await w.Dienst.GetListAsync(w.Verwaltung, w.Verein.Id)).Value!).Id);
        Assert.True((await w.Dienst.GetAsync(w.Verwaltung, w.Verein.Id, einsatz)).IsNotFound);
        Assert.Equal(1, (await w.Dienst.GetSummaryAsync(w.Verwaltung, w.Verein.Id, 2026)).Value!.EventCount);
    }

    // ------------------------------------------------------------ Auswertung

    [Fact]
    public async Task Liste_ZeigtDatumHelferzahlUndMinuten_NeuesteZuerst()
    {
        var w = await AufbauenAsync();
        var alt = await AnlegenAsync(w, Heute.AddDays(-10), "Frühjahrsputz", 60);
        var neu = await AnlegenAsync(w, Heute, "Platzpflege", 120);
        await AbhakenAsync(w, neu, w.Max, w.Anna);
        await w.Dienst.SetMinutesAsync(w.Verwaltung, w.Verein.Id, neu, await TeilnahmeAsync(w, neu, w.Anna), new SetClubWorkMinutesRequest(30));

        var liste = (await w.Dienst.GetListAsync(w.Verwaltung, w.Verein.Id)).Value!;

        Assert.Equal([neu, alt], liste.Select(e => e.Id));
        Assert.Equal((2, 150), (liste[0].HelperCount, liste[0].TotalMinutes));
        Assert.Equal((0, 0), (liste[1].HelperCount, liste[1].TotalMinutes));
    }

    [Fact]
    public async Task Auswertung_SummiertJePersonEinsaetzeUndMinuten_NachStundenSortiert()
    {
        var w = await AufbauenAsync();
        var a = await AnlegenAsync(w, Heute, "Platzpflege", 120);
        var b = await AnlegenAsync(w, Heute.AddDays(-30), "Aufbau Prüfungstag", 240);
        var leer = await AnlegenAsync(w, Heute.AddDays(-40), "Vereinsheim", 60);
        await AbhakenAsync(w, a, w.Max, w.Anna);
        await AbhakenAsync(w, b, w.Anna);
        await w.Dienst.SetMinutesAsync(w.Verwaltung, w.Verein.Id, b, await TeilnahmeAsync(w, b, w.Anna), new SetClubWorkMinutesRequest(300));
        Assert.NotEqual(Guid.Empty, leer);

        var s = (await w.Dienst.GetSummaryAsync(w.Verwaltung, w.Verein.Id, 2026)).Value!;

        Assert.Equal((2026, 3, 3, 120 + 120 + 300), (s.Year, s.EventCount, s.ParticipationCount, s.TotalMinutes));
        Assert.Equal([("Anna", "Abel", 2, 420), ("Max", "Mustermann", 1, 120)],
            s.Rows.Select(r => (r.FirstName, r.LastName, r.EventCount, r.Minutes)));
        Assert.Null(s.Former);
    }

    [Fact]
    public async Task Auswertung_ZaehltNurDasGewaehlteJahr_UndNenntDieJahreMitEinsaetzen()
    {
        var w = await AufbauenAsync();
        var jetzt = await AnlegenAsync(w, Heute, minuten: 60);
        var voriges = await AnlegenAsync(w, new DateOnly(2025, 12, 31), minuten: 90);
        var erstesVor = await AnlegenAsync(w, new DateOnly(2024, 1, 1), minuten: 30);
        foreach (var einsatz in new[] { jetzt, voriges, erstesVor }) await AbhakenAsync(w, einsatz, w.Max);

        var ohneAngabe = (await w.Dienst.GetSummaryAsync(w.Verwaltung, w.Verein.Id, null)).Value!;
        var vorjahr = (await w.Dienst.GetSummaryAsync(w.Verwaltung, w.Verein.Id, 2025)).Value!;
        var ohneEinsaetze = (await w.Dienst.GetSummaryAsync(w.Verwaltung, w.Verein.Id, 2023)).Value!;

        Assert.Equal([2026, 2025, 2024], ohneAngabe.Years);
        Assert.Equal((2026, 60), (ohneAngabe.Year, ohneAngabe.TotalMinutes));
        Assert.Equal((2025, 90, 1), (vorjahr.Year, vorjahr.TotalMinutes, vorjahr.EventCount));
        Assert.Equal((0, 0), (ohneEinsaetze.EventCount, ohneEinsaetze.TotalMinutes));
        Assert.Empty(ohneEinsaetze.Rows);
        Assert.False((await w.Dienst.GetSummaryAsync(w.Verwaltung, w.Verein.Id, 0)).Succeeded);
        Assert.False((await w.Dienst.GetSummaryAsync(w.Verwaltung, w.Verein.Id, 99999)).Succeeded);
    }

    [Fact]
    public async Task Auswertung_OhneEinsatzImVerein_ZeigtDasLaufendeJahr()
    {
        var w = await AufbauenAsync();

        var s = (await w.Dienst.GetSummaryAsync(w.Verwaltung, w.Verein.Id, null)).Value!;

        Assert.Equal((2026, 0), (s.Year, s.EventCount));
        Assert.Empty(s.Years);
    }

    [Fact]
    public async Task Auswertung_FasstEhemaligeMitgliederInEinerSammelzeileOhneNamenZusammen()
    {
        var w = await AufbauenAsync();
        var a = await AnlegenAsync(w, Heute, minuten: 120);
        var b = await AnlegenAsync(w, Heute.AddDays(-5), minuten: 60);
        await AbhakenAsync(w, a, w.Max, w.Anna);
        await AbhakenAsync(w, b, w.Max);
        // Zwei ehemalige Mitglieder mit gleicher Dauer - die Sammelzeile zählt beide.
        await Vereine(w).LeaveClubAsync(w.Max, w.Verein.Id);
        w.Db.ClubWorkParticipations.Add(new ClubWorkParticipation { ClubWorkEventId = a, UserId = null, Minutes = 60 });
        await w.Db.SaveChangesAsync();

        var s = (await w.Dienst.GetSummaryAsync(w.Verwaltung, w.Verein.Id, 2026)).Value!;

        Assert.Equal(("Anna", 120), (s.Rows.Single().FirstName, s.Rows.Single().Minutes));
        // Drei Teilnahmen Ehemaliger, aber nur zwei Einsätze (a und b): gezählt werden Einsätze.
        Assert.Equal(("", "", 2, 240), (s.Former!.FirstName, s.Former.LastName, s.Former.EventCount, s.Former.Minutes));
        Assert.Equal(4, s.ParticipationCount);
        Assert.Equal(120 + 120 + 60 + 60, s.TotalMinutes);
        var einsatz = (await w.Dienst.GetAsync(w.Verwaltung, w.Verein.Id, a)).Value!;
        Assert.Equal([false, true, true], einsatz.Participants.Select(p => p.Former));
        Assert.All(einsatz.Participants.Where(p => p.Former), p => Assert.Equal(("", "", null), (p.FirstName, p.LastName, p.UserId)));
    }

    // ------------------------------------------------------------ Austritt, Konto, Aufräumen

    [Fact]
    public async Task Austritt_LoestNurDieTeilnahmenDerPerson_DieMinutenBleiben()
    {
        var w = await AufbauenAsync();
        var einsatz = await AnlegenAsync(w);
        await AbhakenAsync(w, einsatz, w.Max, w.Anna);

        Assert.True((await Vereine(w).LeaveClubAsync(w.Max, w.Verein.Id)).Succeeded);

        var zeilen = await w.Db.ClubWorkParticipations.AsNoTracking().ToListAsync();
        Assert.Equal(2, zeilen.Count);
        Assert.Single(zeilen, z => z.UserId == w.Anna);
        Assert.Single(zeilen, z => z.UserId is null && z.RecordedByUserId == w.Verwaltung);
        Assert.Equal(240, (await w.Dienst.GetListAsync(w.Verwaltung, w.Verein.Id)).Value!.Single().TotalMinutes);
        Assert.Equal(120, Assert.Single((await w.Dienst.GetMineAsync(w.Anna, w.Verein.Id)).Value!.Items).Minutes);
    }

    [Fact]
    public async Task Austritt_AusEinemAnderenVerein_LaesstDieTeilnahmenUnberuehrt()
    {
        var w = await AufbauenAsync();
        var einsatz = await AnlegenAsync(w);
        await AbhakenAsync(w, einsatz, w.Max);
        var anderer = new Club { Name = "Anderer Verein" };
        w.Db.Clubs.Add(anderer);
        w.Db.ClubMemberships.Add(new ClubMembership { ClubId = anderer.Id, UserId = w.Max, Status = ClubMembershipStatus.Approved });
        await w.Db.SaveChangesAsync();

        Assert.True((await Vereine(w).LeaveClubAsync(w.Max, anderer.Id)).Succeeded);

        Assert.Equal(w.Max, (await w.Db.ClubWorkParticipations.AsNoTracking().SingleAsync()).UserId);
    }

    [Fact]
    public async Task EntfernenDurchDieVerwaltung_LoestDieTeilnahmen()
    {
        var w = await AufbauenAsync();
        var einsatz = await AnlegenAsync(w);
        await AbhakenAsync(w, einsatz, w.Max);

        var ergebnis = await Vereine(w).RemoveClubMemberAsync(w.Verwaltung, w.Verein.Id, w.Max);

        Assert.True(ergebnis.Succeeded, string.Join(";", ergebnis.Errors));
        Assert.Null((await w.Db.ClubWorkParticipations.AsNoTracking().SingleAsync()).UserId);
    }

    [Fact]
    public async Task LetzterRollenentzugOhneMitgliedschaft_LoestDieTeilnahme()
    {
        var w = await AufbauenAsync();
        var einsatz = await AnlegenAsync(w);
        var nurRolle = Guid.NewGuid();
        w.Nutzer.Register(nurRolle, "rolle@example.org", "Rolf", "Rolle");
        w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = w.Verein.Id, UserId = nurRolle, Role = ClubRole.Training });
        await w.Db.SaveChangesAsync();
        await AbhakenAsync(w, einsatz, nurRolle);

        Assert.True((await Vereine(w).RemoveTrainerAsync(w.Verwaltung, false, w.Verein.Id, nurRolle)).Succeeded);

        Assert.Null((await w.Db.ClubWorkParticipations.AsNoTracking().SingleAsync()).UserId);
    }

    [Fact]
    public async Task KontoLoeschen_LoestDieTeilnahmen_UndDieVerweiseAufDieVerwaltung()
    {
        var w = await AufbauenAsync();
        var einsatz = await AnlegenAsync(w);
        await AbhakenAsync(w, einsatz, w.Max, w.Anna);
        var konto = new AccountDataService(w.Db, w.Nutzer, w.Meldungen);

        Assert.True((await konto.PurgeAsync(w.Max)).Succeeded);

        var zeilen = await w.Db.ClubWorkParticipations.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(2, zeilen.Count);
        Assert.Single(zeilen, z => z.UserId == w.Anna);
        Assert.Single(zeilen, z => z.UserId is null);

        // Die Verwaltung geht: Einsatz und Stunden bleiben, nur der Verweis auf sie nicht.
        Assert.True((await konto.PurgeAsync(w.Verwaltung)).Succeeded);
        Assert.Null((await w.Db.ClubWorkEvents.IgnoreQueryFilters().SingleAsync()).CreatedByUserId);
        Assert.All(await w.Db.ClubWorkParticipations.IgnoreQueryFilters().ToListAsync(), z => Assert.Null(z.RecordedByUserId));
        Assert.Equal(2, await w.Db.ClubWorkParticipations.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task AufraeumenVerwaisterDaten_LoestDieTeilnahmenEinesGeloeschtenKontos()
    {
        var w = await AufbauenAsync();
        var einsatz = await AnlegenAsync(w);
        var weg = Guid.NewGuid();
        w.Db.ClubMemberships.Add(new ClubMembership { ClubId = w.Verein.Id, UserId = weg, Status = ClubMembershipStatus.Approved });
        w.Db.ClubWorkParticipations.Add(new ClubWorkParticipation { ClubWorkEventId = einsatz, UserId = weg });
        await w.Db.SaveChangesAsync();
        await AbhakenAsync(w, einsatz, w.Max);

        await new CommunityOrphanCleanup(w.Db, w.Nutzer, w.Meldungen).CleanupAsync();

        var zeilen = await w.Db.ClubWorkParticipations.AsNoTracking().ToListAsync();
        Assert.Equal(2, zeilen.Count);
        Assert.Single(zeilen, z => z.UserId == w.Max);
        Assert.Single(zeilen, z => z.UserId is null);
    }

    [Fact]
    public async Task Aufbewahrung_LoeschtEinsaetzeVorDemDrittletztenJahrMitTeilnahmen_NichtDieJuengeren()
    {
        var w = await AufbauenAsync();
        var vorGrenze = await AnlegenAsync(w, new DateOnly(2024, 1, 1));
        await AbhakenAsync(w, vorGrenze, w.Max);
        // Heute ist 2026: 2024 wird noch aufbewahrt. Die älteren Einsätze entstehen am Dienst vorbei (er lehnt sie ab).
        var alt = new ClubWorkEvent { ClubId = w.Verein.Id, Date = new DateOnly(2023, 12, 31), Title = "Alt", DurationMinutes = 60 };
        var altWeich = new ClubWorkEvent { ClubId = w.Verein.Id, Date = new DateOnly(2021, 6, 1), Title = "Alt, gelöscht", DurationMinutes = 60, DeletedAt = Jetzt };
        w.Db.ClubWorkEvents.AddRange(alt, altWeich);
        w.Db.ClubWorkParticipations.AddRange(
            new ClubWorkParticipation { ClubWorkEventId = alt.Id, UserId = w.Max },
            new ClubWorkParticipation { ClubWorkEventId = alt.Id, UserId = null, Minutes = 30 },
            new ClubWorkParticipation { ClubWorkEventId = altWeich.Id, UserId = w.Anna });
        await w.Db.SaveChangesAsync();

        var entfernt = await new ClubWorkRetention(w.Db).CleanupAsync(Jetzt);

        Assert.Equal(2, entfernt);
        Assert.Equal(vorGrenze, (await w.Db.ClubWorkEvents.IgnoreQueryFilters().SingleAsync()).Id);
        Assert.Equal(1, await w.Db.ClubWorkParticipations.IgnoreQueryFilters().CountAsync());
        // Ein zweiter Lauf findet nichts mehr.
        Assert.Equal(0, await new ClubWorkRetention(w.Db).CleanupAsync(Jetzt));
    }

    [Theory]
    [InlineData(2026, 10, 20, 2024, 1, 1)]
    [InlineData(2026, 1, 1, 2024, 1, 1)]
    [InlineData(2027, 1, 1, 2025, 1, 1)]
    [InlineData(2026, 12, 31, 2024, 1, 1)]
    public void Aufbewahrung_GrenzeIstDerAnfangDesDrittletztenKalenderjahres(int jahr, int monat, int tag, int grenzeJahr, int grenzeMonat, int grenzeTag)
    {
        Assert.Equal(new DateOnly(grenzeJahr, grenzeMonat, grenzeTag), ClubWorkRetention.Grenze(new DateOnly(jahr, monat, tag)));
    }

    // ------------------------------------------------------------ Auskunft

    [Fact]
    public async Task Export_EnthaeltDieEigenenTeilnahmen_NichtDieAnderer()
    {
        var w = await AufbauenAsync();
        var a = await AnlegenAsync(w, Heute, "Platzpflege", 120);
        var b = await AnlegenAsync(w, Heute.AddDays(-20), "Aufbau Prüfungstag", 240);
        await AbhakenAsync(w, a, w.Max, w.Anna);
        await AbhakenAsync(w, b, w.Max);
        await w.Dienst.SetMinutesAsync(w.Verwaltung, w.Verein.Id, b, await TeilnahmeAsync(w, b, w.Max), new SetClubWorkMinutesRequest(200));
        var konto = new AccountDataService(w.Db, w.Nutzer, w.Meldungen);

        var export = (await konto.ExportAsync(w.Max)).Value!;

        Assert.Equal([("Hundesportverein Nord", Heute, "Platzpflege", 120), ("Hundesportverein Nord", Heute.AddDays(-20), "Aufbau Prüfungstag", 200)],
            export.Arbeitseinsaetze!.Select(e => (e.Verein, e.Datum, e.Titel, e.Minuten)));
        Assert.Empty((await konto.ExportAsync(w.Verwaltung)).Value!.Arbeitseinsaetze!);
    }

    // ------------------------------------------------------------ Vereinsübersicht

    [Fact]
    public async Task Uebersicht_ZaehltEinsaetzeUndMinutenJeMonat_OhneNamen()
    {
        var w = await AufbauenAsync();
        var a = await AnlegenAsync(w, Heute, minuten: 120);
        var b = await AnlegenAsync(w, Heute.AddDays(-10), minuten: 60);
        var augustEinsatz = await AnlegenAsync(w, new DateOnly(2026, 8, 15), minuten: 180);
        await AbhakenAsync(w, a, w.Max, w.Anna);
        await AbhakenAsync(w, b, w.Max);
        await AbhakenAsync(w, augustEinsatz, w.Anna);
        await w.Dienst.SetMinutesAsync(w.Verwaltung, w.Verein.Id, a, await TeilnahmeAsync(w, a, w.Anna), new SetClubWorkMinutesRequest(60));
        var vorstand = Guid.NewGuid();
        w.Nutzer.Register(vorstand, "v@example.org", "Vera", "Vorstand");
        w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = w.Verein.Id, UserId = vorstand, Role = ClubRole.Vorstand });
        await w.Db.SaveChangesAsync();
        var uebersicht = new ClubOverviewService(w.Db, w.Nutzer, new FesteUhr());

        var monate = (await uebersicht.GetOverviewAsync(vorstand, false, w.Verein.Id)).Value!.Months;

        var oktober = monate.Single(m => (m.Year, m.Month) == (2026, 10));
        Assert.Equal((2, 120 + 60 + 60), (oktober.WorkEvents, oktober.WorkMinutes));
        var august = monate.Single(m => (m.Year, m.Month) == (2026, 8));
        Assert.Equal((1, 180), (august.WorkEvents, august.WorkMinutes));
        Assert.All(monate.Where(m => m.Month is not (8 or 10)), m => Assert.Equal((0, 0), (m.WorkEvents, m.WorkMinutes)));
    }
}
