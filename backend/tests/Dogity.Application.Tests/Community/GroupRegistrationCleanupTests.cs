using Dogity.Application.Account;
using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Wann Anmeldungen wirklich verschwinden: Aufbewahrungsfrist, Gruppe löschen,
/// Aufräumen verwaister Zeilen, Löschung des Kontos einer Trainer:in.
/// Geprüft wird immer mit IgnoreQueryFilters - ein bloßes Ausblenden bestünde
/// jeden Test, der nur die gewöhnliche Abfrage benutzt.
/// </summary>
public class GroupRegistrationCleanupTests
{
    private static GroupRegistrationRetention Aufbewahrung(AnmeldeAufbau a) => new(a.Db);

    private static async Task AltMachenAsync(AnmeldeAufbau a, GroupRegistration zeile, int monate)
    {
        var vor = DateTimeOffset.UtcNow.AddMonths(-monate);
        zeile.CreatedAt = vor;
        zeile.UpdatedAt = null;
        await a.Db.SaveChangesAsync();
    }

    // ---- Aufbewahrungsfrist ----

    [Fact]
    public async Task Aufbewahrung_LoeschtAnmeldungenOhneAktivitaetSeitZwoelfMonaten_Endgueltig()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var alt = await a.AnmeldungAsync("Alt", "0160 111111");
        var jung = await a.AnmeldungAsync("Jung", "0160 222222");
        await AltMachenAsync(a, alt, 13);

        var entfernt = await Aufbewahrung(a).CleanupAsync();

        Assert.Equal(1, entfernt);
        var uebrig = await a.Db.GroupRegistrations.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(jung.Id, Assert.Single(uebrig).Id);
    }

    [Fact]
    public async Task Aufbewahrung_NimmtDieAnwesenheitMit()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var alt = await a.AnmeldungAsync();
        a.Db.GroupRegistrationAttendances.Add(new GroupRegistrationAttendance
        {
            RegistrationId = alt.Id, Date = Vereinszeit.Heute().AddMonths(-14), MarkedAt = DateTimeOffset.UtcNow.AddMonths(-14),
        });
        a.Db.GroupRegistrationAttendances.Add(new GroupRegistrationAttendance
        {
            RegistrationId = alt.Id, Date = Vereinszeit.Heute().AddMonths(-15), MarkedAt = DateTimeOffset.UtcNow.AddMonths(-15),
            DeletedAt = DateTimeOffset.UtcNow.AddMonths(-15),
        });
        await a.Db.SaveChangesAsync();
        await AltMachenAsync(a, alt, 14);

        await Aufbewahrung(a).CleanupAsync();

        Assert.Empty(a.Db.GroupRegistrations.IgnoreQueryFilters());
        Assert.Empty(a.Db.GroupRegistrationAttendances.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Aufbewahrung_EineJuengereAenderungHaeltDieAnmeldungAmLeben()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var zeile = await a.AnmeldungAsync();
        await AltMachenAsync(a, zeile, 20);

        // Bezahlt gesetzt = angefasst.
        await a.Dienst.SetPaidAsync(a.Leitung, a.Gruppe.Id, zeile.Id, true);

        Assert.Equal(0, await Aufbewahrung(a).CleanupAsync());
        Assert.Single(a.Db.GroupRegistrations.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Aufbewahrung_EinJuengererHakenHaeltDieAnmeldungAmLeben()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var zeile = await a.AnmeldungAsync();
        await AltMachenAsync(a, zeile, 20);
        a.Db.GroupRegistrationAttendances.Add(new GroupRegistrationAttendance
        {
            RegistrationId = zeile.Id, Date = Vereinszeit.Heute().AddDays(-3), MarkedAt = DateTimeOffset.UtcNow.AddDays(-3),
        });
        await a.Db.SaveChangesAsync();

        Assert.Equal(0, await Aufbewahrung(a).CleanupAsync());
    }

    [Fact]
    public async Task Aufbewahrung_DerGoogleZeitstempelAlleinLaesstEineFrischeUebernahmeNichtVerfallen()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        // Gerade importiert (CreatedAt = jetzt), aber im Google-Formular vor 14 Monaten angemeldet.
        await a.AnmeldungAsync(angemeldet: DateTimeOffset.UtcNow.AddMonths(-14));

        Assert.Equal(0, await Aufbewahrung(a).CleanupAsync());
    }

    [Fact]
    public async Task Aufbewahrung_RaeumtAuchAnmeldungenGeloeschterGruppenAb()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var zeile = await a.AnmeldungAsync();
        await AltMachenAsync(a, zeile, 13);
        a.Gruppe.DeletedAt = DateTimeOffset.UtcNow.AddMonths(-1);
        await a.Db.SaveChangesAsync();

        Assert.Equal(1, await Aufbewahrung(a).CleanupAsync());
        Assert.Empty(a.Db.GroupRegistrations.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Aufbewahrung_ZweiterLaufTutNichts()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var zeile = await a.AnmeldungAsync();
        await AltMachenAsync(a, zeile, 13);

        await Aufbewahrung(a).CleanupAsync();

        Assert.Equal(0, await Aufbewahrung(a).CleanupAsync());
    }

    // ---- Gruppe löschen ----

    [Fact]
    public async Task GruppeLoeschen_NimmtAnmeldungenUndAnwesenheitEndgueltigMit_AndereGruppenBleiben()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var lookup = new FakeUserLookupService();
        var gruppenDienst = new GroupService(a.Db, lookup, new TrainerRoleService(a.Db, lookup), new FakeNotificationService());
        var zeile = await a.AnmeldungAsync();
        await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, zeile.Id, Vereinszeit.Heute(), true);
        var andere = new Group { ClubId = a.Verein.Id, TrainerId = a.Leitung, Name = "Junghunde" };
        var anderesZeile = new GroupRegistration
        {
            GroupId = andere.Id, FirstName = "X", LastName = "Y", DogName = "Rex", DogBreed = "Mix",
            DogBirthDate = Vereinszeit.Heute().AddMonths(-2), Phone = "0160 123456",
        };
        a.Db.Groups.Add(andere);
        a.Db.GroupRegistrations.Add(anderesZeile);
        await a.Db.SaveChangesAsync();

        var ergebnis = await gruppenDienst.DeleteGroupAsync(a.Leitung, a.Gruppe.Id);

        Assert.True(ergebnis.Succeeded);
        var uebrig = await a.Db.GroupRegistrations.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(anderesZeile.Id, Assert.Single(uebrig).Id);
        Assert.Empty(a.Db.GroupRegistrationAttendances.IgnoreQueryFilters());
    }

    [Fact]
    public async Task GruppeLoeschen_DurchFremde_LaesstAnmeldungenUnberuehrt()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var lookup = new FakeUserLookupService();
        var gruppenDienst = new GroupService(a.Db, lookup, new TrainerRoleService(a.Db, lookup), new FakeNotificationService());
        await a.AnmeldungAsync();

        var ergebnis = await gruppenDienst.DeleteGroupAsync(a.Fremde, a.Gruppe.Id);

        Assert.False(ergebnis.Succeeded);
        Assert.Single(a.Db.GroupRegistrations);
    }

    // ---- Aufräumen verwaister Zeilen ----

    [Fact]
    public async Task Aufraeumen_EntferntAnmeldungenZuGruppenDieEsNichtMehrGibt_UndLaesstDenRestStehen()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var bleibt = await a.AnmeldungAsync("Bleibt", "0160 111111");
        var verwaist = new GroupRegistration
        {
            GroupId = Guid.NewGuid(), FirstName = "X", LastName = "Y", DogName = "Weg", DogBreed = "Mix",
            DogBirthDate = Vereinszeit.Heute().AddMonths(-2), Phone = "0160 999999",
        };
        var inGeloeschterGruppe = new Group { ClubId = a.Verein.Id, TrainerId = a.Leitung, Name = "Alt", DeletedAt = DateTimeOffset.UtcNow };
        var zweite = new GroupRegistration
        {
            GroupId = inGeloeschterGruppe.Id, FirstName = "X", LastName = "Y", DogName = "Auch weg", DogBreed = "Mix",
            DogBirthDate = Vereinszeit.Heute().AddMonths(-2), Phone = "0160 888888",
        };
        a.Db.Groups.Add(inGeloeschterGruppe);
        a.Db.GroupRegistrations.AddRange(verwaist, zweite);
        a.Db.GroupRegistrationAttendances.Add(new GroupRegistrationAttendance { RegistrationId = verwaist.Id, Date = Vereinszeit.Heute() });
        await a.Db.SaveChangesAsync();

        // Die Trainer:innen sind Konten, die es gibt - nur die Anmeldungen sind verwaist.
        var lookup = new FakeUserLookupService();
        foreach (var id in new[] { a.Leitung, a.CoTrainerin, a.VereinsTrainerin })
            lookup.Register(id, $"{id}@example.com");

        var entfernt = await new CommunityOrphanCleanup(a.Db, lookup).CleanupAsync();

        Assert.Equal(2, entfernt);
        Assert.Equal(bleibt.Id, Assert.Single(await a.Db.GroupRegistrations.IgnoreQueryFilters().ToListAsync()).Id);
        Assert.Empty(a.Db.GroupRegistrationAttendances.IgnoreQueryFilters());
    }

    // ---- Kontolöschung einer Trainer:in ----

    private static AccountDataService Konto(AnmeldeAufbau a) => new(a.Db, new FakeUserLookupService());

    [Fact]
    public async Task Kontoloeschung_SetztVerweiseAufNull_DieAnmeldungenBleiben()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var zeile = await a.AnmeldungAsync();
        var andere = await a.AnmeldungAsync("Luna", "0160 222222");
        await a.Dienst.SetPaidAsync(a.CoTrainerin, a.Gruppe.Id, zeile.Id, true);
        await a.Dienst.SetPaidAsync(a.Leitung, a.Gruppe.Id, andere.Id, true);
        await a.Dienst.SetAttendanceAsync(a.CoTrainerin, a.Gruppe.Id, zeile.Id, Vereinszeit.Heute(), true);
        await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, zeile.Id, Vereinszeit.Heute().AddDays(-7), true);

        var ergebnis = await Konto(a).PurgeAsync(a.CoTrainerin);

        Assert.True(ergebnis.Succeeded);
        var zeilen = await a.Db.GroupRegistrations.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(2, zeilen.Count);
        var bella = zeilen.Single(z => z.Id == zeile.Id);
        Assert.Null(bella.PaidByUserId);
        Assert.NotNull(bella.PaidAt); // bezahlt bleibt bezahlt - nur der Name der Trainer:in geht
        Assert.Equal(a.Leitung, zeilen.Single(z => z.Id == andere.Id).PaidByUserId);
        var haken = await a.Db.GroupRegistrationAttendances.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(2, haken.Count);
        Assert.Null(haken.Single(h => h.Date == Vereinszeit.Heute()).MarkedByUserId);
        Assert.Equal(a.Leitung, haken.Single(h => h.Date == Vereinszeit.Heute().AddDays(-7)).MarkedByUserId);
    }

    [Fact]
    public async Task Kontoloeschung_DerLetztenTrainerin_SchliesstGruppeUndLoeschtAnmeldungen()
    {
        var db = InMemoryDbContext.Create();
        var ich = Guid.NewGuid();
        var gruppe = new Group { Name = "Welpen", TrainerId = ich };
        db.Groups.Add(gruppe);
        var zeile = new GroupRegistration
        {
            GroupId = gruppe.Id, FirstName = "A", LastName = "B", DogName = "Rex", DogBreed = "Mix",
            DogBirthDate = Vereinszeit.Heute().AddMonths(-2), Phone = "0160 123456",
        };
        db.GroupRegistrations.Add(zeile);
        db.GroupRegistrationAttendances.Add(new GroupRegistrationAttendance { RegistrationId = zeile.Id, Date = Vereinszeit.Heute(), MarkedByUserId = ich });
        await db.SaveChangesAsync();

        await new AccountDataService(db, new FakeUserLookupService()).PurgeAsync(ich);

        Assert.Empty(db.GroupRegistrations.IgnoreQueryFilters());
        Assert.Empty(db.GroupRegistrationAttendances.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Kontoloeschung_MitNachfolgerin_LaesstAnmeldungenDerGruppeStehen()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        await a.AnmeldungAsync();

        await Konto(a).PurgeAsync(a.Leitung);

        // Die Co-Trainerin übernimmt die Gruppe - und damit die Anmeldungen.
        Assert.Single(a.Db.GroupRegistrations);
        Assert.Equal(a.CoTrainerin, (await a.Db.Groups.SingleAsync()).TrainerId);
    }

    [Fact]
    public async Task Datenexport_EnthaeltKeineKursteilnehmenden()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var lookup = new FakeUserLookupService();
        lookup.Register(a.Leitung, "leitung@example.com", "Lena", "Leitung");
        await a.AnmeldungAsync("Geheimhund", "0160 424242");
        var export = (await new AccountDataService(a.Db, lookup).ExportAsync(a.Leitung)).Value!;

        var json = System.Text.Json.JsonSerializer.Serialize(export);

        Assert.DoesNotContain("Geheimhund", json);
        Assert.DoesNotContain("424242", json);
        Assert.DoesNotContain("Muster", json);
    }
}
