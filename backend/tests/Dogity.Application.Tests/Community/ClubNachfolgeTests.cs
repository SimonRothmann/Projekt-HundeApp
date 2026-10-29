using Dogity.Application.Account;
using Dogity.Application.Common;
using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Löscht die letzte verwaltende Person eines Vereins ihr Konto (oder fällt sie
/// beim Aufräumen verwaister Daten weg), rückt die dienstälteste Trainer:in
/// zur Verwaltung nach und bekommt Bescheid. Ohne diese Regel bliebe ein Verein
/// zurück, den niemand mehr verwalten kann.
/// </summary>
public class ClubNachfolgeTests
{
    private static readonly DateTimeOffset Anfang = new(2024, 3, 1, 8, 0, 0, TimeSpan.Zero);

    private sealed class Verein
    {
        public ApplicationDbContext Db { get; } = InMemoryDbContext.Create();
        public FakeUserLookupService Konten { get; } = new();
        public FakeNotificationService Meldungen { get; } = new();
        public Club Club { get; } = new() { Name = "Nordverein" };

        public Verein() => Db.Clubs.Add(Club);

        /// <summary>Legt ein Konto samt Trainer-Zuweisung an; <paramref name="seitTagen"/> bestimmt das Dienstalter.</summary>
        public Guid Trainer(ClubRole rolle, int seitTagen, bool konto = true, Guid? id = null)
        {
            var nutzer = id ?? Guid.NewGuid();
            if (konto) Konten.Register(nutzer, $"{nutzer}@example.com");
            Db.ClubTrainers.Add(new ClubTrainer
            {
                ClubId = Club.Id,
                UserId = nutzer,
                Role = rolle,
                CreatedAt = Anfang.AddDays(seitTagen),
            });
            return nutzer;
        }

        public Task SpeichernAsync() => Db.SaveChangesAsync();

        public Task<Result> LoeschenAsync(Guid nutzer) =>
            new AccountDataService(Db, Konten, Meldungen).PurgeAsync(nutzer);

        public Task<int> AufraeumenAsync() =>
            new CommunityOrphanCleanup(Db, Konten, Meldungen).CleanupAsync();

        public async Task<ClubRole?> RolleAsync(Guid nutzer) =>
            (await Db.ClubTrainers.SingleOrDefaultAsync(t => t.UserId == nutzer && t.ClubId == Club.Id))?.Role;
    }

    // ---- Kontolöschung (PurgeAsync) ----

    [Fact]
    public async Task Kontoloeschung_LetzteVerwaltung_DienstaeltesteTrainerinRueckt_UndBekommtMeldung()
    {
        var v = new Verein();
        var verwaltung = v.Trainer(ClubRole.Verwaltung, 0);
        var juengere = v.Trainer(ClubRole.Training, 200);
        var aelteste = v.Trainer(ClubRole.Training, 100);
        await v.SpeichernAsync();

        var ergebnis = await v.LoeschenAsync(verwaltung);

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(ClubRole.Verwaltung, await v.RolleAsync(aelteste));
        Assert.Equal(ClubRole.Training, await v.RolleAsync(juengere));
        var meldung = Assert.Single(v.Meldungen.Created);
        Assert.Equal(aelteste, meldung.UserId);
        Assert.Equal("Du verwaltest jetzt den Verein \"Nordverein\".", meldung.Message);
        Assert.Equal("/trainer", meldung.LinkPath);
    }

    [Fact]
    public async Task Kontoloeschung_GleichesDienstalter_EntscheidetDieId()
    {
        var v = new Verein();
        var verwaltung = v.Trainer(ClubRole.Verwaltung, 0);
        v.Trainer(ClubRole.Training, 50);
        v.Trainer(ClubRole.Training, 50);
        await v.SpeichernAsync();
        var erwartet = v.Db.ClubTrainers
            .Where(t => t.Role == ClubRole.Training)
            .AsEnumerable()
            .OrderBy(t => t.Id)
            .First().UserId;

        await v.LoeschenAsync(verwaltung);

        Assert.Equal(erwartet, Assert.Single(v.Meldungen.Created).UserId);
        Assert.Equal(1, await v.Db.ClubTrainers.CountAsync(t => t.Role == ClubRole.Verwaltung));
    }

    [Fact]
    public async Task Kontoloeschung_ZweiteVerwaltungVorhanden_NiemandWirdBefoerdert()
    {
        var v = new Verein();
        var verwaltung = v.Trainer(ClubRole.Verwaltung, 0);
        var zweite = v.Trainer(ClubRole.Verwaltung, 10);
        var trainerin = v.Trainer(ClubRole.Training, 20);
        await v.SpeichernAsync();

        await v.LoeschenAsync(verwaltung);

        Assert.Equal(ClubRole.Verwaltung, await v.RolleAsync(zweite));
        Assert.Equal(ClubRole.Training, await v.RolleAsync(trainerin));
        Assert.Empty(v.Meldungen.Created);
    }

    [Fact]
    public async Task Kontoloeschung_KeineTrainerinMehr_VereinOhneVerwaltung_KeinFehler()
    {
        var v = new Verein();
        var verwaltung = v.Trainer(ClubRole.Verwaltung, 0);
        await v.SpeichernAsync();

        var ergebnis = await v.LoeschenAsync(verwaltung);

        Assert.True(ergebnis.Succeeded);
        Assert.Empty(v.Db.ClubTrainers);
        Assert.Empty(v.Meldungen.Created);
        Assert.NotNull(await v.Db.Clubs.SingleAsync());
    }

    [Fact]
    public async Task Kontoloeschung_Trainerin_VerwaltungBleibt_NiemandWirdBefoerdert()
    {
        var v = new Verein();
        v.Trainer(ClubRole.Verwaltung, 0);
        var trainerin = v.Trainer(ClubRole.Training, 10);
        var andere = v.Trainer(ClubRole.Training, 20);
        await v.SpeichernAsync();

        await v.LoeschenAsync(trainerin);

        Assert.Equal(ClubRole.Training, await v.RolleAsync(andere));
        Assert.Empty(v.Meldungen.Created);
    }

    [Fact]
    public async Task Kontoloeschung_NachfolgeNurImEigenenVerein()
    {
        var v = new Verein();
        var verwaltung = v.Trainer(ClubRole.Verwaltung, 0);
        var eigene = v.Trainer(ClubRole.Training, 30);

        var fremderVerein = new Club { Name = "Suedverein" };
        v.Db.Clubs.Add(fremderVerein);
        var fremde = Guid.NewGuid();
        v.Db.ClubTrainers.Add(new ClubTrainer { ClubId = fremderVerein.Id, UserId = fremde, Role = ClubRole.Training, CreatedAt = Anfang.AddDays(-500) });
        await v.SpeichernAsync();

        await v.LoeschenAsync(verwaltung);

        Assert.Equal(ClubRole.Verwaltung, await v.RolleAsync(eigene));
        Assert.Equal(ClubRole.Training, (await v.Db.ClubTrainers.SingleAsync(t => t.UserId == fremde)).Role);
        Assert.Equal(eigene, Assert.Single(v.Meldungen.Created).UserId);
    }

    // ---- Aufräumen verwaister Daten (CommunityOrphanCleanup) ----

    [Fact]
    public async Task Aufraeumen_LetzteVerwaltungOhneKonto_DienstaeltesteTrainerinRueckt()
    {
        var v = new Verein();
        v.Trainer(ClubRole.Verwaltung, 0, konto: false);
        var juengere = v.Trainer(ClubRole.Training, 200);
        var aelteste = v.Trainer(ClubRole.Training, 100);
        await v.SpeichernAsync();

        await v.AufraeumenAsync();

        Assert.Equal(ClubRole.Verwaltung, await v.RolleAsync(aelteste));
        Assert.Equal(ClubRole.Training, await v.RolleAsync(juengere));
        Assert.Equal(aelteste, Assert.Single(v.Meldungen.Created).UserId);
        Assert.Equal(2, await v.Db.ClubTrainers.CountAsync());
    }

    [Fact]
    public async Task Aufraeumen_ZweiteVerwaltungVorhanden_NiemandWirdBefoerdert()
    {
        var v = new Verein();
        v.Trainer(ClubRole.Verwaltung, 0, konto: false);
        v.Trainer(ClubRole.Verwaltung, 10);
        var trainerin = v.Trainer(ClubRole.Training, 20);
        await v.SpeichernAsync();

        await v.AufraeumenAsync();

        Assert.Equal(ClubRole.Training, await v.RolleAsync(trainerin));
        Assert.Empty(v.Meldungen.Created);
    }

    [Fact]
    public async Task Aufraeumen_KeineTrainerinMehr_VereinOhneVerwaltung_KeinFehler()
    {
        var v = new Verein();
        v.Trainer(ClubRole.Verwaltung, 0, konto: false);
        await v.SpeichernAsync();

        var entfernt = await v.AufraeumenAsync();

        Assert.Equal(1, entfernt);
        Assert.Empty(v.Db.ClubTrainers);
        Assert.Empty(v.Meldungen.Created);
    }

    [Fact]
    public async Task Aufraeumen_AuchDieNachfolgeKannVerwaistSein_NiemandOhneKontoRueckt()
    {
        var v = new Verein();
        v.Trainer(ClubRole.Verwaltung, 0, konto: false);
        v.Trainer(ClubRole.Training, 10, konto: false);
        var mitKonto = v.Trainer(ClubRole.Training, 20);
        await v.SpeichernAsync();

        await v.AufraeumenAsync();

        Assert.Equal(ClubRole.Verwaltung, await v.RolleAsync(mitKonto));
        Assert.Equal(mitKonto, Assert.Single(v.Meldungen.Created).UserId);
    }
}
