using Dogity.Application.Community;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Verwaltung der Anmeldungen durch die Trainer:innen: Rechte, Link, Liste,
/// "bezahlt", Anwesenheit je Tag und Import.
/// </summary>
public class GroupRegistrationServiceTests
{
    // ---- Rechte ----

    /// <summary>Jede Verwaltungsaktion einmal - für die Rechteprüfung ohne zehnfachen Code.</summary>
    private static IEnumerable<(string Name, Func<AnmeldeAufbau, Guid, Guid, Task<Dogity.Application.Common.Result>> Aktion)> Aktionen() =>
    [
        ("Link lesen", (a, wer, _) => a.Dienst.GetLinkAsync(wer, a.Gruppe.Id).ContinueWith(t => (Dogity.Application.Common.Result)t.Result)),
        ("Link erzeugen", async (a, wer, _) => await a.Dienst.RegenerateLinkAsync(wer, a.Gruppe.Id)),
        ("Link schließen", (a, wer, _) => a.Dienst.DisableLinkAsync(wer, a.Gruppe.Id)),
        ("Liste", async (a, wer, _) => await a.Dienst.ListAsync(wer, a.Gruppe.Id)),
        ("Anlegen", async (a, wer, _) => await a.Dienst.CreateAsync(wer, a.Gruppe.Id, AnmeldeAufbau.Eingabe(rufname: "Neu", telefon: "0160 555555"))),
        ("Bearbeiten", async (a, wer, id) => await a.Dienst.UpdateAsync(wer, a.Gruppe.Id, id, AnmeldeAufbau.Eingabe())),
        ("Bezahlt", async (a, wer, id) => await a.Dienst.SetPaidAsync(wer, a.Gruppe.Id, id, true)),
        ("Tage", async (a, wer, _) => await a.Dienst.GetAttendanceDaysAsync(wer, a.Gruppe.Id)),
        ("Anwesenheit lesen", async (a, wer, _) => await a.Dienst.GetAttendanceAsync(wer, a.Gruppe.Id, Vereinszeit.Heute())),
        ("Abhaken", (a, wer, id) => a.Dienst.SetAttendanceAsync(wer, a.Gruppe.Id, id, Vereinszeit.Heute(), true)),
        ("Import", async (a, wer, _) => await a.Dienst.ImportAsync(wer, a.Gruppe.Id,
            new ImportRegistrationsRequest([new ImportRegistrationRow(2, "A", "B", "Rex", "Mix", Vereinszeit.Heute().AddMonths(-1), "0160 777777", null)]))),
        ("Löschen", (a, wer, id) => a.Dienst.DeleteAsync(wer, a.Gruppe.Id, id)),
    ];

    [Fact]
    public async Task Verwalten_DarfLeitung_CoTrainerin_UndVereinsTrainerin()
    {
        foreach (var (name, aktion) in Aktionen())
        {
            foreach (var wer in new[] { "Leitung", "Co", "Verein" })
            {
                var a = await AnmeldeAufbau.ErzeugeAsync();
                var zeile = await a.AnmeldungAsync();
                var nutzer = wer switch { "Leitung" => a.Leitung, "Co" => a.CoTrainerin, _ => a.VereinsTrainerin };

                var ergebnis = await aktion(a, nutzer, zeile.Id);

                Assert.True(ergebnis.Succeeded, $"{name} als {wer}: {string.Join(", ", ergebnis.Errors)}");
            }
        }
    }

    [Fact]
    public async Task Verwalten_Fremde_BekommenUeberallDieselbe404()
    {
        foreach (var (name, aktion) in Aktionen())
        {
            var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);
            var zeile = await a.AnmeldungAsync();

            var ergebnis = await aktion(a, a.Fremde, zeile.Id);

            Assert.False(ergebnis.Succeeded, name);
            Assert.True(ergebnis.IsNotFound, name);
        }
    }

    [Fact]
    public async Task Verwalten_EingeladeneCoTrainerin_HatNochKeineRechte()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var eingeladen = Guid.NewGuid();
        a.Db.GroupTrainers.Add(new GroupTrainer { GroupId = a.Gruppe.Id, UserId = eingeladen, Status = GroupTrainerStatus.Invited });
        await a.Db.SaveChangesAsync();

        Assert.True((await a.Dienst.ListAsync(eingeladen, a.Gruppe.Id)).IsNotFound);
    }

    [Fact]
    public async Task Verwalten_TrainerinEinesAnderenVereins_BekommtKeinenZugriff()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var anderer = new Club { Name = "Anderer Verein" };
        var fremdeTrainerin = Guid.NewGuid();
        a.Db.Clubs.Add(anderer);
        a.Db.ClubTrainers.Add(new ClubTrainer { ClubId = anderer.Id, UserId = fremdeTrainerin });
        await a.Db.SaveChangesAsync();

        Assert.True((await a.Dienst.ListAsync(fremdeTrainerin, a.Gruppe.Id)).IsNotFound);
    }

    [Fact]
    public async Task Verwalten_AnmeldungAusFremderGruppe_IstFuerEigeneGruppeUnsichtbar()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var andere = new Group { TrainerId = a.Fremde, Name = "Andere Gruppe" };
        var fremdeZeile = new GroupRegistration
        {
            GroupId = andere.Id, FirstName = "X", LastName = "Y", DogName = "Rex", DogBreed = "Mix",
            DogBirthDate = Vereinszeit.Heute().AddMonths(-2), Phone = "0160 123456",
        };
        a.Db.Groups.Add(andere);
        a.Db.GroupRegistrations.Add(fremdeZeile);
        await a.Db.SaveChangesAsync();

        // Die Leitung der ersten Gruppe kennt die ID, darf sie über SIE aber nicht anfassen.
        Assert.True((await a.Dienst.SetPaidAsync(a.Leitung, a.Gruppe.Id, fremdeZeile.Id, true)).IsNotFound);
        Assert.True((await a.Dienst.DeleteAsync(a.Leitung, a.Gruppe.Id, fremdeZeile.Id)).IsNotFound);
        Assert.True((await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, fremdeZeile.Id, Vereinszeit.Heute(), true)).IsNotFound);
        Assert.Null((await a.Db.GroupRegistrations.SingleAsync(r => r.Id == fremdeZeile.Id)).PaidAt);
    }

    // ---- Link ----

    [Fact]
    public async Task Link_ErzeugenErsetztDenAlten_SchliessenSchaltetAb()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        Assert.Null((await a.Dienst.GetLinkAsync(a.Leitung, a.Gruppe.Id)).Value);

        var erster = (await a.Dienst.RegenerateLinkAsync(a.Leitung, a.Gruppe.Id)).Value!.Code;
        var zweiter = (await a.Dienst.RegenerateLinkAsync(a.CoTrainerin, a.Gruppe.Id)).Value!.Code;

        Assert.NotEqual(erster, zweiter);
        Assert.True(ClubInviteCode.IstGueltigeForm(zweiter));
        Assert.True((await a.Formular.GetFormAsync(erster)).IsNotFound);
        Assert.True((await a.Formular.GetFormAsync(zweiter)).Succeeded);

        Assert.True((await a.Dienst.DisableLinkAsync(a.Leitung, a.Gruppe.Id)).Succeeded);
        Assert.True((await a.Dienst.DisableLinkAsync(a.Leitung, a.Gruppe.Id)).Succeeded); // idempotent
        Assert.Null((await a.Dienst.GetLinkAsync(a.Leitung, a.Gruppe.Id)).Value);
        Assert.True((await a.Formular.GetFormAsync(zweiter)).IsNotFound);
    }

    [Fact]
    public async Task Link_Schliessen_LaesstBisherigeAnmeldungenStehen()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync(mitCode: true);
        await a.AnmeldungAsync();

        await a.Dienst.DisableLinkAsync(a.Leitung, a.Gruppe.Id);

        Assert.Single((await a.Dienst.ListAsync(a.Leitung, a.Gruppe.Id)).Value!);
    }

    // ---- Liste, Anlegen, Bearbeiten, Löschen ----

    [Fact]
    public async Task Liste_NeuesteZuerst_MitAnzahlDerAnwesenheiten()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var alt = await a.AnmeldungAsync("Alt", "0160 111111", DateTimeOffset.UtcNow.AddDays(-10));
        var neu = await a.AnmeldungAsync("Neu", "0160 222222", DateTimeOffset.UtcNow.AddDays(-1));
        await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, alt.Id, Vereinszeit.Heute(), true);
        await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, alt.Id, Vereinszeit.Heute().AddDays(-7), true);

        var liste = (await a.Dienst.ListAsync(a.Leitung, a.Gruppe.Id)).Value!;

        Assert.Equal(["Neu", "Alt"], liste.Select(r => r.DogName));
        Assert.Equal(0, liste[0].AttendanceCount);
        Assert.Equal(2, liste[1].AttendanceCount);
        Assert.Equal(neu.Id, liste[0].Id);
    }

    [Fact]
    public async Task Anlegen_Manuell_HatHerkunftVonHand_UndPrueftWieDasFormular()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();

        var ok = await a.Dienst.CreateAsync(a.Leitung, a.Gruppe.Id, AnmeldeAufbau.Eingabe(notiz: "  Futterallergie "));
        var schlecht = await a.Dienst.CreateAsync(a.Leitung, a.Gruppe.Id, AnmeldeAufbau.Eingabe(telefon: "x"));

        Assert.True(ok.Succeeded);
        Assert.Equal(GroupRegistrationSource.Manual, ok.Value!.Source);
        Assert.Equal("Futterallergie", ok.Value.Notes);
        Assert.False(schlecht.Succeeded);
        Assert.False(schlecht.IsNotFound);
        Assert.Single(a.Db.GroupRegistrations);
    }

    [Fact]
    public async Task Anlegen_NotizDarfNichtZuLangSein()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();

        var ergebnis = await a.Dienst.CreateAsync(a.Leitung, a.Gruppe.Id, AnmeldeAufbau.Eingabe(notiz: new string('n', 501)));

        Assert.False(ergebnis.Succeeded);
        Assert.Contains("zu lang", ergebnis.Errors[0]);
    }

    [Fact]
    public async Task Bearbeiten_AendertAlleFelder_UndKorrigiertAlteAnmeldungenOhneAltersgrenzeVonHeute()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        // Angemeldet vor 3 Jahren, Hund damals 4 Monate alt - heute wäre er "zu alt" fürs Formular.
        var alt = await a.AnmeldungAsync("Bella", "0160 111111", DateTimeOffset.UtcNow.AddYears(-3));
        var wurftag = Vereinszeit.Tag(alt.RegisteredAt).AddMonths(-4);

        var ergebnis = await a.Dienst.UpdateAsync(a.CoTrainerin, a.Gruppe.Id, alt.Id,
            AnmeldeAufbau.Eingabe(vorname: "Berta", rufname: "Bella2", rasse: "Mix", wurftag: wurftag, telefon: "0160 999999", notiz: "Neue Nummer"));

        Assert.True(ergebnis.Succeeded, string.Join(", ", ergebnis.Errors));
        var zeile = await a.Db.GroupRegistrations.SingleAsync();
        Assert.Equal("Berta", zeile.FirstName);
        Assert.Equal("Bella2", zeile.DogName);
        Assert.Equal("0160 999999", zeile.Phone);
        Assert.Equal("Neue Nummer", zeile.Notes);
        Assert.NotNull(zeile.UpdatedAt);
    }

    [Fact]
    public async Task Bearbeiten_UngueltigeEingabe_AendertNichts()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var zeile = await a.AnmeldungAsync();

        var ergebnis = await a.Dienst.UpdateAsync(a.Leitung, a.Gruppe.Id, zeile.Id, AnmeldeAufbau.Eingabe(vorname: ""));

        Assert.False(ergebnis.Succeeded);
        Assert.Equal("Anna", (await a.Db.GroupRegistrations.SingleAsync()).FirstName);
    }

    [Fact]
    public async Task Loeschen_EntferntAnmeldungUndAnwesenheitEndgueltig()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var zeile = await a.AnmeldungAsync();
        await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, zeile.Id, Vereinszeit.Heute(), true);
        await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, zeile.Id, Vereinszeit.Heute().AddDays(-7), true);
        await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, zeile.Id, Vereinszeit.Heute().AddDays(-7), false); // weich entfernt

        var ergebnis = await a.Dienst.DeleteAsync(a.Leitung, a.Gruppe.Id, zeile.Id);

        Assert.True(ergebnis.Succeeded);
        Assert.Empty(a.Db.GroupRegistrations.IgnoreQueryFilters());
        Assert.Empty(a.Db.GroupRegistrationAttendances.IgnoreQueryFilters());
    }

    // ---- bezahlt ----

    [Fact]
    public async Task Bezahlt_SetzenUndZuruecknehmen()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var zeile = await a.AnmeldungAsync();

        var bezahlt = await a.Dienst.SetPaidAsync(a.CoTrainerin, a.Gruppe.Id, zeile.Id, true);

        Assert.NotNull(bezahlt.Value!.PaidAt);
        var gespeichert = await a.Db.GroupRegistrations.SingleAsync();
        Assert.Equal(a.CoTrainerin, gespeichert.PaidByUserId);

        var offen = await a.Dienst.SetPaidAsync(a.Leitung, a.Gruppe.Id, zeile.Id, false);

        Assert.Null(offen.Value!.PaidAt);
        gespeichert = await a.Db.GroupRegistrations.SingleAsync();
        Assert.Null(gespeichert.PaidAt);
        Assert.Null(gespeichert.PaidByUserId);
    }

    [Fact]
    public async Task Bezahlt_ZweitesSetzen_BehaeltDieErsteTrainerin()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var zeile = await a.AnmeldungAsync();
        await a.Dienst.SetPaidAsync(a.CoTrainerin, a.Gruppe.Id, zeile.Id, true);
        var zeitpunkt = (await a.Db.GroupRegistrations.SingleAsync()).PaidAt;

        await a.Dienst.SetPaidAsync(a.Leitung, a.Gruppe.Id, zeile.Id, true);

        var gespeichert = await a.Db.GroupRegistrations.SingleAsync();
        Assert.Equal(a.CoTrainerin, gespeichert.PaidByUserId);
        Assert.Equal(zeitpunkt, gespeichert.PaidAt);
    }

    // ---- Anwesenheit ----

    [Fact]
    public async Task Anwesenheit_Abhaken_ErscheintInDerTagesliste_UndWirdZurueckgenommen()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var bella = await a.AnmeldungAsync("Bella", "0160 111111");
        var luna = await a.AnmeldungAsync("Luna", "0160 222222");
        var heute = Vereinszeit.Heute();

        await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, bella.Id, heute, true);
        await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, luna.Id, heute.AddDays(-7), true);

        Assert.Equal([bella.Id], (await a.Dienst.GetAttendanceAsync(a.Leitung, a.Gruppe.Id, heute)).Value);
        Assert.Equal([luna.Id], (await a.Dienst.GetAttendanceAsync(a.Leitung, a.Gruppe.Id, heute.AddDays(-7))).Value);

        await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, bella.Id, heute, false);

        Assert.Empty((await a.Dienst.GetAttendanceAsync(a.Leitung, a.Gruppe.Id, heute)).Value!);
    }

    [Fact]
    public async Task Anwesenheit_WiederholtesAbhaken_AendertDieZeileStattEinDuplikatAnzulegen()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var zeile = await a.AnmeldungAsync();
        var heute = Vereinszeit.Heute();

        await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, zeile.Id, heute, true);
        await a.Dienst.SetAttendanceAsync(a.CoTrainerin, a.Gruppe.Id, zeile.Id, heute, true); // doppelt
        await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, zeile.Id, heute, false);
        await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, zeile.Id, heute, false);    // doppelt
        await a.Dienst.SetAttendanceAsync(a.CoTrainerin, a.Gruppe.Id, zeile.Id, heute, true); // belebt die entfernte Zeile

        var zeilen = await a.Db.GroupRegistrationAttendances.IgnoreQueryFilters().ToListAsync();
        var haken = Assert.Single(zeilen);
        Assert.Null(haken.DeletedAt);
        Assert.Equal(a.CoTrainerin, haken.MarkedByUserId);
        Assert.Equal(1, (await a.Dienst.ListAsync(a.Leitung, a.Gruppe.Id)).Value![0].AttendanceCount);
    }

    [Fact]
    public async Task Anwesenheit_ZukuenftigerTag_WirdAbgelehnt()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var zeile = await a.AnmeldungAsync();

        var ergebnis = await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, zeile.Id, Vereinszeit.Heute().AddDays(1), true);

        Assert.False(ergebnis.Succeeded);
        Assert.False(ergebnis.IsNotFound);
        Assert.Empty(a.Db.GroupRegistrationAttendances);
    }

    [Fact]
    public async Task Anwesenheit_SehrAlterTag_WirdAbgelehnt()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var zeile = await a.AnmeldungAsync();

        var ergebnis = await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, zeile.Id, new DateOnly(1999, 1, 1), true);

        Assert.False(ergebnis.Succeeded);
    }

    [Fact]
    public async Task Anwesenheit_VerknuepftDenTerminDerGruppeAnDemTag()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var zeile = await a.AnmeldungAsync();
        var heute = Vereinszeit.Heute();
        // Gestern 10:00 Uhr Vereinszeit; als UTC gespeichert.
        var gestern = heute.AddDays(-1);
        var start = new DateTimeOffset(gestern.ToDateTime(new TimeOnly(10, 0)), Vereinszeit.Zone.GetUtcOffset(gestern.ToDateTime(new TimeOnly(10, 0))));
        var termin = new GroupTrainingSession { ClubId = a.Verein.Id, GroupId = a.Gruppe.Id, StartsAt = start };
        // Ein Termin einer anderen Gruppe und ein abgesagter am selben Tag zählen nicht.
        var fremder = new GroupTrainingSession { ClubId = a.Verein.Id, GroupId = Guid.NewGuid(), StartsAt = start };
        var abgesagt = new GroupTrainingSession { ClubId = a.Verein.Id, GroupId = a.Gruppe.Id, StartsAt = start.AddHours(-1), Status = GroupTrainingSessionStatus.Cancelled };
        a.Db.GroupTrainingSessions.AddRange(termin, fremder, abgesagt);
        await a.Db.SaveChangesAsync();

        await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, zeile.Id, gestern, true);
        await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, zeile.Id, heute, true);

        var haken = await a.Db.GroupRegistrationAttendances.ToListAsync();
        Assert.Equal(termin.Id, haken.Single(h => h.Date == gestern).GroupTrainingSessionId);
        Assert.Null(haken.Single(h => h.Date == heute).GroupTrainingSessionId);
    }

    [Fact]
    public async Task Anwesenheit_Tage_ZeigenTermineDerLetztenVierUndNaechstenZweiWochen()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var heute = Vereinszeit.Heute();
        DateTimeOffset Um(DateOnly tag) => new(tag.ToDateTime(new TimeOnly(12, 0)), Vereinszeit.Zone.GetUtcOffset(tag.ToDateTime(new TimeOnly(12, 0))));
        var innen = new GroupTrainingSession { ClubId = a.Verein.Id, GroupId = a.Gruppe.Id, StartsAt = Um(heute.AddDays(-7)) };
        var zweiter = new GroupTrainingSession { ClubId = a.Verein.Id, GroupId = a.Gruppe.Id, StartsAt = Um(heute.AddDays(-7)).AddHours(3) };
        var kommend = new GroupTrainingSession { ClubId = a.Verein.Id, GroupId = a.Gruppe.Id, StartsAt = Um(heute.AddDays(10)) };
        var zuAlt = new GroupTrainingSession { ClubId = a.Verein.Id, GroupId = a.Gruppe.Id, StartsAt = Um(heute.AddDays(-40)) };
        var zuFern = new GroupTrainingSession { ClubId = a.Verein.Id, GroupId = a.Gruppe.Id, StartsAt = Um(heute.AddDays(30)) };
        var abgesagt = new GroupTrainingSession { ClubId = a.Verein.Id, GroupId = a.Gruppe.Id, StartsAt = Um(heute.AddDays(-3)), Status = GroupTrainingSessionStatus.Cancelled };
        var anderes = new GroupTrainingSession { ClubId = a.Verein.Id, GroupId = Guid.NewGuid(), StartsAt = Um(heute.AddDays(-2)) };
        a.Db.GroupTrainingSessions.AddRange(innen, zweiter, kommend, zuAlt, zuFern, abgesagt, anderes);
        await a.Db.SaveChangesAsync();

        var tage = (await a.Dienst.GetAttendanceDaysAsync(a.Leitung, a.Gruppe.Id)).Value!;

        Assert.Equal([heute.AddDays(-7), heute.AddDays(10)], tage.Select(t => t.Date));
        Assert.Equal(innen.Id, tage[0].SessionId); // der früheste je Tag
    }

    // ---- Import ----

    private static ImportRegistrationRow Zeile(int nr, string rufname = "Rex", string telefon = "0160 777777", DateOnly? wurftag = null, DateTimeOffset? angemeldet = null, string vorname = "Max") =>
        new(nr, vorname, "Muster", rufname, "Mix", wurftag ?? Vereinszeit.Heute().AddMonths(-2), telefon, angemeldet);

    [Fact]
    public async Task Import_LegtZeilenAnMitHerkunftImportUndGoogleZeitstempel()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var damals = DateTimeOffset.UtcNow.AddMonths(-5);

        var ergebnis = await a.Dienst.ImportAsync(a.Leitung, a.Gruppe.Id, new ImportRegistrationsRequest([
            Zeile(2, "Rex", "0160 111111", angemeldet: damals),
            Zeile(3, "Luna", "0160 222222"),
        ]));

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(2, ergebnis.Value!.Angelegt);
        Assert.Equal(0, ergebnis.Value.Uebersprungen);
        Assert.Empty(ergebnis.Value.Fehler);
        var rex = await a.Db.GroupRegistrations.SingleAsync(r => r.DogName == "Rex");
        Assert.Equal(GroupRegistrationSource.Import, rex.Source);
        Assert.Equal(damals, rex.RegisteredAt);
        // Ohne Zeitstempel gilt der Moment des Imports.
        var luna = await a.Db.GroupRegistrations.SingleAsync(r => r.DogName == "Luna");
        Assert.True(luna.RegisteredAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Import_UeberspringtDuplikate_AuchOhneZeitgrenze_UndInnerhalbderDatei()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        await a.AnmeldungAsync("Rex", "0160 111111", angemeldet: DateTimeOffset.UtcNow.AddMonths(-8));

        var ergebnis = await a.Dienst.ImportAsync(a.Leitung, a.Gruppe.Id, new ImportRegistrationsRequest([
            Zeile(2, "REX", "+49 160 111111"),   // schon in der Gruppe (anderes Format, andere Schreibung)
            Zeile(3, "Luna", "0160 222222"),
            Zeile(4, "Luna", "0160/222222"),     // doppelt in der Datei
        ]));

        Assert.Equal(1, ergebnis.Value!.Angelegt);
        Assert.Equal(2, ergebnis.Value.Uebersprungen);
        Assert.Equal(2, await a.Db.GroupRegistrations.CountAsync());
    }

    [Fact]
    public async Task Import_MeldetFehlerzeilenMitZeilennummer_UndImportiertDenRest()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var ohneWurftag = new ImportRegistrationRow(3, "Max", "Muster", "Luna", "Mix", null, "0160 222222", null);

        var ergebnis = await a.Dienst.ImportAsync(a.Leitung, a.Gruppe.Id, new ImportRegistrationsRequest([
            Zeile(2, "Rex", "0160 111111"),
            ohneWurftag,
            Zeile(4, "Fritz", "abc", vorname: "Fritz"),
            Zeile(5, "Zukunft", "0160 333333", wurftag: Vereinszeit.Heute().AddDays(2)),
        ]));

        Assert.Equal(1, ergebnis.Value!.Angelegt);
        Assert.Equal([3, 4, 5], ergebnis.Value.Fehler.Select(f => f.Zeile));
        Assert.Contains("Wurftag", ergebnis.Value.Fehler[0].Meldung);
        Assert.Contains("Telefonnummer", ergebnis.Value.Fehler[1].Meldung);
    }

    [Fact]
    public async Task Import_AltesDatumPruefungGiltRelativZumZeitstempel()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        // Vor einem Jahr angemeldet, Hund damals 18 Monate alt: heute 30 Monate, aber gültig.
        var damals = DateTimeOffset.UtcNow.AddYears(-1);
        var wurftag = Vereinszeit.Tag(damals).AddMonths(-18);

        var ergebnis = await a.Dienst.ImportAsync(a.Leitung, a.Gruppe.Id, new ImportRegistrationsRequest([
            Zeile(2, "Opa", "0160 111111", wurftag: wurftag, angemeldet: damals),
        ]));

        Assert.Equal(1, ergebnis.Value!.Angelegt);
    }

    [Fact]
    public async Task Import_ZeitstempelAusDerZukunft_WirdZumMomentDesImports()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();

        await a.Dienst.ImportAsync(a.Leitung, a.Gruppe.Id, new ImportRegistrationsRequest([
            Zeile(2, angemeldet: DateTimeOffset.UtcNow.AddYears(1)),
        ]));

        Assert.True((await a.Db.GroupRegistrations.SingleAsync()).RegisteredAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Import_Obergrenze_ZuvieleZeilenWerdenAbgewiesen()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var zeilen = Enumerable.Range(2, GroupRegistrationRules.MaxImportZeilen + 1).Select(i => Zeile(i, $"Hund{i}", $"0160 {100000 + i}")).ToList();

        var ergebnis = await a.Dienst.ImportAsync(a.Leitung, a.Gruppe.Id, new ImportRegistrationsRequest(zeilen));

        Assert.False(ergebnis.Succeeded);
        Assert.False(ergebnis.IsNotFound);
        Assert.Empty(a.Db.GroupRegistrations);
    }

    [Fact]
    public async Task Import_ExakteObergrenze_GehtDurch()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var zeilen = Enumerable.Range(2, GroupRegistrationRules.MaxImportZeilen).Select(i => Zeile(i, $"Hund{i}", $"0160 {100000 + i}")).ToList();

        var ergebnis = await a.Dienst.ImportAsync(a.Leitung, a.Gruppe.Id, new ImportRegistrationsRequest(zeilen));

        Assert.Equal(GroupRegistrationRules.MaxImportZeilen, ergebnis.Value!.Angelegt);
    }

    [Fact]
    public async Task Import_LeereDatei_IstEinFehler()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();

        Assert.False((await a.Dienst.ImportAsync(a.Leitung, a.Gruppe.Id, new ImportRegistrationsRequest([]))).Succeeded);
        Assert.False((await a.Dienst.ImportAsync(a.Leitung, a.Gruppe.Id, new ImportRegistrationsRequest(null))).Succeeded);
    }

    [Fact]
    public async Task Import_RespektiertDieObergrenzeJeGruppeAuchMitBestand()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        for (var i = 0; i < GroupRegistrationRules.MaxProGruppe - 1; i++)
            a.Db.GroupRegistrations.Add(new GroupRegistration
            {
                GroupId = a.Gruppe.Id, FirstName = "A", LastName = "B", DogName = $"Alt{i}", DogBreed = "Mix",
                DogBirthDate = Vereinszeit.Heute().AddMonths(-2), Phone = $"0721 {100000 + i}",
            });
        await a.Db.SaveChangesAsync();

        var ergebnis = await a.Dienst.ImportAsync(a.Leitung, a.Gruppe.Id, new ImportRegistrationsRequest([
            Zeile(2, "Eins", "0160 111111"), Zeile(3, "Zwei", "0160 222222")]));

        Assert.Equal(1, ergebnis.Value!.Angelegt);
        Assert.Single(ergebnis.Value.Fehler);
        Assert.Equal(GroupRegistrationRules.MaxProGruppe, await a.Db.GroupRegistrations.CountAsync());
    }

    // ---- Telefon-Normalisierung ----

    [Theory]
    [InlineData("0721 123456", "0721123456")]
    [InlineData("+49 721 123456", "0721123456")]
    [InlineData("0049 721 123456", "0721123456")]
    [InlineData("0721/12 34-56", "0721123456")]
    [InlineData("+41 44 123 45 67", "41441234567")]
    [InlineData("+49 (0) 721 123456", "0721123456")]
    [InlineData("+49 0721 123456", "0721123456")]
    public void Telefon_WirdNormalisiert(string eingabe, string erwartet) =>
        Assert.Equal(erwartet, GroupRegistrationRules.NormalisiereTelefon(eingabe));
}
