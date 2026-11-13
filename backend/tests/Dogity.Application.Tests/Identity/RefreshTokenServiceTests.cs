using Dogity.Application.Tests.TestSupport;
using Dogity.Infrastructure.Identity;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dogity.Application.Tests.Identity;

/// <summary>
/// Testet die Refresh-Token-Rotation inkl. Reuse-Erkennung (siehe
/// RefreshTokenService / TODO.md Roadmap 6): ein Refresh dreht den Token,
/// der alte wird ungültig, und die Wiedervorlage eines bereits verbrauchten
/// Tokens widerruft die ganze Kette - es sei denn, der Nachfolger ist noch
/// unbenutzt: Dann ging nur die Antwort der Erneuerung verloren (siehe
/// "Tolerante Rotation" unten).
/// </summary>
public class RefreshTokenServiceTests
{
    private static RefreshTokenService MakeService() => MakeService(out _);

    private static RefreshTokenService MakeService(out ApplicationDbContext db) =>
        MakeService(out db, NullLogger<RefreshTokenService>.Instance);

    private static RefreshTokenService MakeService(out ApplicationDbContext db, ILogger<RefreshTokenService> logger)
    {
        db = InMemoryDbContext.Create();
        var settings = Options.Create(new JwtSettings { RefreshExpiryDays = 60 });
        return new RefreshTokenService(db, settings, logger);
    }

    [Fact]
    public async Task Rotate_WithValidToken_IssuesNewAndInvalidatesOld()
    {
        var service = MakeService();
        var userId = Guid.NewGuid();
        var original = await service.IssueAsync(userId);

        var rotation = await service.RotateAsync(original);

        Assert.True(rotation.Succeeded);
        Assert.Equal(userId, rotation.UserId);
        Assert.NotNull(rotation.NewRawToken);
        Assert.NotEqual(original, rotation.NewRawToken);

        // Der alte Token ist verbraucht, sobald sein Nachfolger benutzt wurde -
        // erneutes Rotieren scheitert. (Solange der Nachfolger unbenutzt ist,
        // gilt die Toleranz für verlorene Antworten, siehe unten.)
        Assert.True((await service.RotateAsync(rotation.NewRawToken!)).Succeeded);
        var reuse = await service.RotateAsync(original);
        Assert.False(reuse.Succeeded);
    }

    [Fact]
    public async Task Rotate_ReusedToken_RevokesEntireChain()
    {
        var service = MakeService();
        var userId = Guid.NewGuid();
        var original = await service.IssueAsync(userId);
        var rotation = await service.RotateAsync(original);
        var newToken = rotation.NewRawToken!;
        // Der Nachfolger ist benutzt - damit ist der Original-Token wirklich verbraucht.
        var zweite = await service.RotateAsync(newToken);

        // Angreifer legt den bereits verbrauchten Original-Token erneut vor:
        // Reuse-Erkennung greift und widerruft die ganze Kette.
        var reuse = await service.RotateAsync(original);
        Assert.False(reuse.Succeeded);

        // Folge: Auch der zwischenzeitlich gültige jüngste Token ist jetzt tot -
        // der legitime Nutzer muss sich neu anmelden (sichere Reaktion auf
        // möglichen Diebstahl).
        var afterReuse = await service.RotateAsync(zweite.NewRawToken!);
        Assert.False(afterReuse.Succeeded);
    }

    [Fact]
    public async Task Rotate_UnknownToken_Fails()
    {
        var service = MakeService();
        var result = await service.RotateAsync("nie-ausgestellt");
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RevokeAllForUser_InvalidatesActiveTokens()
    {
        var service = MakeService();
        var userId = Guid.NewGuid();
        var tokenA = await service.IssueAsync(userId);
        var tokenB = await service.IssueAsync(userId);

        await service.RevokeAllForUserAsync(userId);

        Assert.False((await service.RotateAsync(tokenA)).Succeeded);
        Assert.False((await service.RotateAsync(tokenB)).Succeeded);
    }

    [Fact]
    public async Task Revoke_SingleToken_LeavesOtherDevicesActive()
    {
        var service = MakeService();
        var userId = Guid.NewGuid();
        var phone = await service.IssueAsync(userId);
        var laptop = await service.IssueAsync(userId);

        await service.RevokeAsync(phone);

        // Nur das eine Gerät ist abgemeldet, das andere bleibt gültig.
        Assert.False((await service.RotateAsync(phone)).Succeeded);
        Assert.True((await service.RotateAsync(laptop)).Succeeded);
    }

    [Fact]
    public async Task RevokeAllExcept_KeepsOnlyTheCurrentDevice()
    {
        var service = MakeService();
        var userId = Guid.NewGuid();
        var hier = await service.IssueAsync(userId);
        var anderesGeraet = await service.IssueAsync(userId);

        await service.RevokeAllExceptAsync(userId, hier);

        Assert.False((await service.RotateAsync(anderesGeraet)).Succeeded);
        Assert.True((await service.RotateAsync(hier)).Succeeded);
    }

    [Fact]
    public async Task RevokeAllExcept_ForeignTokenProtectsNothing()
    {
        var service = MakeService();
        var userId = Guid.NewGuid();
        var meiner = await service.IssueAsync(userId);
        var fremder = await service.IssueAsync(Guid.NewGuid());

        // Ein Token eines anderen Kontos darf die eigenen Sitzungen nicht retten
        // - und bleibt selbst unberührt.
        await service.RevokeAllExceptAsync(userId, fremder);

        Assert.False((await service.RotateAsync(meiner)).Succeeded);
        Assert.True((await service.RotateAsync(fremder)).Succeeded);
    }

    [Fact]
    public async Task DeleteAllForUser_RemovesTheRowsForGood_AndLeavesOthersAlone()
    {
        var service = MakeService(out var db);
        var geloescht = Guid.NewGuid();
        var anderer = Guid.NewGuid();
        await service.IssueAsync(geloescht);
        var widerrufen = await service.IssueAsync(geloescht);
        await service.RevokeAsync(widerrufen);
        var fremd = await service.IssueAsync(anderer);

        await service.DeleteAllForUserAsync(geloescht);

        // Auch widerrufene Zeilen sind weg, nicht nur die aktiven.
        Assert.Empty(await db.RefreshTokens.Where(t => t.UserId == geloescht).ToListAsync());
        Assert.True((await service.RotateAsync(fremd)).Succeeded);
    }

    [Fact]
    public async Task DeleteOrphaned_RemovesOnlyTokensOfMissingAccounts()
    {
        var service = MakeService(out var db);
        var bestehend = new ApplicationUser { UserName = "da@test.de", Email = "da@test.de" };
        db.Users.Add(bestehend);
        await db.SaveChangesAsync();
        var behalten = await service.IssueAsync(bestehend.Id);
        var geloeschtesKonto = Guid.NewGuid();
        await service.IssueAsync(geloeschtesKonto);
        var widerrufen = await service.IssueAsync(geloeschtesKonto);
        await service.RevokeAsync(widerrufen);

        var entfernt = await service.DeleteOrphanedAsync();

        Assert.Equal(2, entfernt);
        Assert.Empty(await db.RefreshTokens.Where(t => t.UserId == geloeschtesKonto).ToListAsync());
        Assert.True((await service.RotateAsync(behalten)).Succeeded);
        // Idempotent: Ein zweiter Lauf findet nichts mehr.
        Assert.Equal(0, await service.DeleteOrphanedAsync());
    }

    [Fact]
    public async Task CountAccountsWithTokenSince_ZaehltVerschiedeneBestehendeKontenImZeitraum()
    {
        var service = MakeService(out var db);
        var oft = await LegeKontoAnAsync(db);
        var einmal = await LegeKontoAnAsync(db);
        var vorLangem = await LegeKontoAnAsync(db);
        var geloeschtesKonto = Guid.NewGuid();
        await service.IssueAsync(oft);
        var weiter = await service.IssueAsync(oft);
        await service.RotateAsync(weiter); // jede Erneuerung ist ein weiterer Token desselben Kontos
        await service.IssueAsync(einmal);
        await service.IssueAsync(geloeschtesKonto);
        await service.IssueAsync(vorLangem);
        foreach (var token in db.RefreshTokens.Where(t => t.UserId == vorLangem))
            token.CreatedAt = DateTimeOffset.UtcNow.AddDays(-40);
        await db.SaveChangesAsync();

        Assert.Equal(2, await service.CountAccountsWithTokenSinceAsync(DateTimeOffset.UtcNow.AddDays(-30)));
        Assert.Equal(3, await service.CountAccountsWithTokenSinceAsync(DateTimeOffset.UtcNow.AddDays(-60)));
        Assert.Equal(0, await service.CountAccountsWithTokenSinceAsync(DateTimeOffset.UtcNow.AddDays(1)));
    }

    // --- Tolerante Rotation: verlorene Antwort, Fenster, Protokoll ---

    private static async Task SetzeWiderrufAsync(ApplicationDbContext db, string rawToken, DateTimeOffset zeitpunkt)
    {
        var hash = HashVon(rawToken);
        (await db.RefreshTokens.SingleAsync(t => t.TokenHash == hash)).RevokedAt = zeitpunkt;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Rotate_VerloreneAntwort_WirdToleriert_UndLaesstAndereGeraeteInRuhe()
    {
        var service = MakeService();
        var userId = Guid.NewGuid();
        var r0 = await service.IssueAsync(userId);
        var zweitesGeraet = await service.IssueAsync(userId);
        var r1 = (await service.RotateAsync(r0)).NewRawToken!; // Antwort erreicht das Gerät nie

        var erneut = await service.RotateAsync(r0);

        Assert.True(erneut.Succeeded);
        Assert.Equal(userId, erneut.UserId);
        var r2 = erneut.NewRawToken!;
        Assert.NotEqual(r1, r2);
        // Der zwischenzeitliche Nachfolger ist verbraucht, der neueste gilt, das zweite Gerät bleibt gültig.
        Assert.True((await service.RotateAsync(zweitesGeraet)).Succeeded);
        Assert.True((await service.RotateAsync(r2)).Succeeded);
    }

    [Fact]
    public async Task Rotate_VerlorenerNachfolger_NachDerToleranzGiltDerNeueFaden()
    {
        var service = MakeService();
        var r0 = await service.IssueAsync(Guid.NewGuid());
        var r1 = (await service.RotateAsync(r0)).NewRawToken!;
        var r2 = (await service.RotateAsync(r0)).NewRawToken!;

        // R1 hat jetzt einen Nachfolger (R2), der unbenutzt ist: dieselbe Regel, eine Stufe weiter.
        var ueberR1 = await service.RotateAsync(r1);

        Assert.True(ueberR1.Succeeded);
        Assert.NotEqual(r2, ueberR1.NewRawToken);
    }

    [Fact]
    public async Task Rotate_EchterDoppelgebrauch_WiderruftAlleSitzungen()
    {
        var service = MakeService();
        var userId = Guid.NewGuid();
        var r0 = await service.IssueAsync(userId);
        var zweitesGeraet = await service.IssueAsync(userId);
        var r1 = (await service.RotateAsync(r0)).NewRawToken!;
        var r2 = (await service.RotateAsync(r1)).NewRawToken!; // Nachfolger benutzt

        Assert.False((await service.RotateAsync(r0)).Succeeded);

        Assert.False((await service.RotateAsync(r2)).Succeeded);
        Assert.False((await service.RotateAsync(zweitesGeraet)).Succeeded);
    }

    [Fact]
    public async Task Rotate_NachfolgerPerAbmeldenWiderrufen_WiderruftAlleSitzungen()
    {
        var service = MakeService();
        var userId = Guid.NewGuid();
        var r0 = await service.IssueAsync(userId);
        var zweitesGeraet = await service.IssueAsync(userId);
        var r1 = (await service.RotateAsync(r0)).NewRawToken!;
        await service.RevokeAsync(r1);

        Assert.False((await service.RotateAsync(r0)).Succeeded);

        Assert.False((await service.RotateAsync(zweitesGeraet)).Succeeded);
    }

    [Fact]
    public async Task Rotate_NachfolgerFehlt_WiderruftAlleSitzungen()
    {
        var service = MakeService(out var db);
        var userId = Guid.NewGuid();
        var r0 = await service.IssueAsync(userId);
        var zweitesGeraet = await service.IssueAsync(userId);
        var r1 = (await service.RotateAsync(r0)).NewRawToken!;
        var hash1 = HashVon(r1);
        db.RefreshTokens.Remove(await db.RefreshTokens.SingleAsync(t => t.TokenHash == hash1));
        await db.SaveChangesAsync();

        Assert.False((await service.RotateAsync(r0)).Succeeded);

        Assert.False((await service.RotateAsync(zweitesGeraet)).Succeeded);
    }

    [Fact]
    public async Task Rotate_NachfolgerAbgelaufen_WiderruftAlleSitzungen()
    {
        var service = MakeService(out var db);
        var userId = Guid.NewGuid();
        var r0 = await service.IssueAsync(userId);
        var zweitesGeraet = await service.IssueAsync(userId);
        var r1 = (await service.RotateAsync(r0)).NewRawToken!;
        var hash1 = HashVon(r1);
        (await db.RefreshTokens.SingleAsync(t => t.TokenHash == hash1)).ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        Assert.False((await service.RotateAsync(r0)).Succeeded);

        Assert.False((await service.RotateAsync(zweitesGeraet)).Succeeded);
    }

    [Fact]
    public async Task Rotate_Abgemeldet_ScheitertStill_AndereSitzungenBleiben()
    {
        var service = MakeService();
        var userId = Guid.NewGuid();
        var handy = await service.IssueAsync(userId);
        var laptop = await service.IssueAsync(userId);
        await service.RevokeAsync(handy);

        Assert.False((await service.RotateAsync(handy)).Succeeded);

        Assert.True((await service.RotateAsync(laptop)).Succeeded);
    }

    [Fact]
    public async Task Rotate_Abgelaufen_Scheitert_OhneMassenwiderruf()
    {
        var service = MakeService(out var db);
        var userId = Guid.NewGuid();
        var alt = await service.IssueAsync(userId);
        var laptop = await service.IssueAsync(userId);
        var hash = HashVon(alt);
        (await db.RefreshTokens.SingleAsync(t => t.TokenHash == hash)).ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        Assert.False((await service.RotateAsync(alt)).Succeeded);

        Assert.True((await service.RotateAsync(laptop)).Succeeded);
    }

    [Fact]
    public async Task Rotate_RotiertVorMehrAlsSiebenTagen_KeineToleranz()
    {
        var service = MakeService(out var db);
        var userId = Guid.NewGuid();
        var r0 = await service.IssueAsync(userId);
        var zweitesGeraet = await service.IssueAsync(userId);
        await service.RotateAsync(r0);
        await SetzeWiderrufAsync(db, r0, DateTimeOffset.UtcNow.AddDays(-7).AddMinutes(-1));

        Assert.False((await service.RotateAsync(r0)).Succeeded);

        // Außerhalb des Fensters gilt wieder die Wiederverwendungs-Regel: alles widerrufen.
        Assert.False((await service.RotateAsync(zweitesGeraet)).Succeeded);
    }

    [Fact]
    public async Task Rotate_RotiertVorWenigerAlsSiebenTagen_WirdNochToleriert()
    {
        var service = MakeService(out var db);
        var r0 = await service.IssueAsync(Guid.NewGuid());
        await service.RotateAsync(r0);
        await SetzeWiderrufAsync(db, r0, DateTimeOffset.UtcNow.AddDays(-6));

        Assert.True((await service.RotateAsync(r0)).Succeeded);
    }

    [Fact]
    public async Task Rotate_ZweiAufrufeHintereinander_BeideGelingen()
    {
        var service = MakeService(out var db);
        var userId = Guid.NewGuid();
        var r0 = await service.IssueAsync(userId);

        // Die zweite Anfrage kommt nach dem Speichern der ersten: Nachfolger ungenutzt, Toleranzpfad.
        // (Parallele Aufrufe vor dem Speichern lassen sich mit dem In-Memory-Kontext nicht nachstellen.)
        var a = await service.RotateAsync(r0);
        var b = await service.RotateAsync(r0);

        Assert.True(a.Succeeded);
        Assert.True(b.Succeeded);
        // Beide Seiten (Tabs) halten jeweils einen Token und können weiter erneuern, in beliebiger Reihenfolge.
        Assert.True((await service.RotateAsync(a.NewRawToken!)).Succeeded);
        Assert.True((await service.RotateAsync(b.NewRawToken!)).Succeeded);
        Assert.Contains(db.RefreshTokens, t => t.UserId == userId && t.RevokedAt == null);
    }

    [Fact]
    public async Task Issue_BehaeltRotierteTokensImFenster_LoeschtAeltereUndAbgelaufene()
    {
        var service = MakeService(out var db);
        var userId = Guid.NewGuid();
        var frisch = await service.IssueAsync(userId);
        var alt = await service.IssueAsync(userId);
        var abgelaufen = await service.IssueAsync(userId);
        var aktiv = await service.IssueAsync(userId);
        await service.RotateAsync(frisch);
        await service.RotateAsync(alt);
        await SetzeWiderrufAsync(db, alt, DateTimeOffset.UtcNow.AddDays(-8));
        var hashAbgelaufen = HashVon(abgelaufen);
        (await db.RefreshTokens.SingleAsync(t => t.TokenHash == hashAbgelaufen)).ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        await service.IssueAsync(userId); // Anmeldung räumt auf

        // Der frisch rotierte "frisch" wird noch erkannt (Toleranz), "alt" und "abgelaufen" sind unbekannt.
        Assert.True((await service.RotateAsync(frisch)).Succeeded);
        Assert.Null(await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == HashVon(alt)));
        Assert.Null(await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == HashVon(abgelaufen)));
        Assert.True((await service.RotateAsync(aktiv)).Succeeded);
    }

    [Fact]
    public async Task Rotate_AnmeldungAufGeraetBZwischenVerlustUndWiedervorlage_ToleriertGeraetAWeiterhin()
    {
        var service = MakeService();
        var userId = Guid.NewGuid();
        var geraetA = await service.IssueAsync(userId);
        await service.RotateAsync(geraetA); // Antwort geht verloren
        await service.IssueAsync(userId); // Anmeldung auf Gerät B räumt auf

        Assert.True((await service.RotateAsync(geraetA)).Succeeded);
    }

    [Fact]
    public async Task Logzeilen_NennenGrundUndKonto_ABERNieTokenOderHash()
    {
        var logger = new SammelLogger();
        var service = MakeService(out var db, logger);
        var userId = Guid.NewGuid();
        var r0 = await service.IssueAsync(userId);
        var r1 = (await service.RotateAsync(r0)).NewRawToken!;
        var r2 = (await service.RotateAsync(r0)).NewRawToken!; // tolerieren
        await service.RotateAsync("nie-ausgestellt");
        var abgemeldet = await service.IssueAsync(userId);
        await service.RevokeAsync(abgemeldet);
        await service.RotateAsync(abgemeldet);
        var ablauf = await service.IssueAsync(userId);
        var hashAblauf = HashVon(ablauf);
        (await db.RefreshTokens.SingleAsync(t => t.TokenHash == hashAblauf)).ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        await service.RotateAsync(ablauf);
        await service.RotateAsync(r2); // erneuern, damit R1/R0 echte Wiederverwendung werden
        await service.RotateAsync(r1);
        await service.RotateAsync(r0);

        var alle = string.Join("\n", logger.Zeilen.Select(z => z.Text));
        Assert.Contains(logger.Zeilen, z => z.Stufe == LogLevel.Information && z.Text == "Sitzung: unbekannter Token");
        Assert.Contains(logger.Zeilen, z => z.Stufe == LogLevel.Information && z.Text == $"Sitzung: verlorene Antwort toleriert (Konto {userId})");
        Assert.Contains(logger.Zeilen, z => z.Stufe == LogLevel.Information && z.Text == $"Sitzung: abgemeldet (widerrufen ohne Nachfolger) (Konto {userId})");
        Assert.Contains(logger.Zeilen, z => z.Stufe == LogLevel.Information && z.Text == $"Sitzung: abgelaufen (Konto {userId})");
        Assert.Contains(logger.Zeilen, z => z.Stufe == LogLevel.Warning && z.Text == $"Sitzung: Wiederverwendung – alle Sitzungen widerrufen (Konto {userId})");
        Assert.All(logger.Zeilen, z => Assert.StartsWith("Sitzung:", z.Text));
        foreach (var geheim in new[] { r0, r1, r2, abgemeldet, ablauf, HashVon(r0), HashVon(r1), HashVon(r2), HashVon(abgemeldet), hashAblauf })
            Assert.DoesNotContain(geheim, alle);
    }

    [Fact]
    public async Task Refresh_KontoGesperrtOderGeloescht_LogtWarnungMitKonto()
    {
        var logger = new SammelLogger();
        var service = MakeService(out var db, logger);
        var gesperrt = await LegeKontoAnAsync(db, DateTimeOffset.MaxValue);
        var geloescht = Guid.NewGuid();

        await service.RefreshAsync(await service.IssueAsync(gesperrt));
        await service.RefreshAsync(await service.IssueAsync(geloescht));

        Assert.Contains(logger.Zeilen, z => z.Stufe == LogLevel.Warning && z.Text == $"Sitzung: Konto gesperrt/gelöscht (Konto {gesperrt})");
        Assert.Contains(logger.Zeilen, z => z.Stufe == LogLevel.Warning && z.Text == $"Sitzung: Konto gesperrt/gelöscht (Konto {geloescht})");
    }

    private static string HashVon(string rawToken) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));

    private sealed class SammelLogger : ILogger<RefreshTokenService>
    {
        public List<(LogLevel Stufe, string Text)> Zeilen { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Zeilen.Add((logLevel, formatter(state, exception)));
    }

    // --- RefreshAsync: Rotation plus Kontoprüfung ---

    private static async Task<Guid> LegeKontoAnAsync(ApplicationDbContext db, DateTimeOffset? sperreBis = null)
    {
        var konto = new ApplicationUser { UserName = "a@dogity.test", Email = "a@dogity.test", LockoutEnd = sperreBis };
        db.Users.Add(konto);
        await db.SaveChangesAsync();
        return konto.Id;
    }

    [Fact]
    public async Task Refresh_AktivesKonto_StelltNeuenTokenAus()
    {
        var service = MakeService(out var db);
        var userId = await LegeKontoAnAsync(db);
        var token = await service.IssueAsync(userId);

        var ergebnis = await service.RefreshAsync(token);

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(userId, ergebnis.UserId);
        Assert.NotNull(ergebnis.NewRawToken);
        // Der neue Token arbeitet weiter.
        Assert.True((await service.RefreshAsync(ergebnis.NewRawToken!)).Succeeded);
    }

    [Fact]
    public async Task Refresh_UnbekannterToken_Scheitert()
    {
        var service = MakeService();

        Assert.False((await service.RefreshAsync("nie-ausgestellt")).Succeeded);
    }

    [Fact]
    public async Task Refresh_DauerhaftGesperrt_Verweigert_UndWiderruftAlleTokens()
    {
        var service = MakeService(out var db);
        var userId = await LegeKontoAnAsync(db, DateTimeOffset.MaxValue);
        var handy = await service.IssueAsync(userId);
        var laptop = await service.IssueAsync(userId);

        var ergebnis = await service.RefreshAsync(handy);

        Assert.False(ergebnis.Succeeded);
        // Auch das zweite Gerät ist raus, und der gerade rotierte Nachfolger
        // ebenfalls: Es bleibt kein aktiver Token übrig.
        Assert.False((await service.RotateAsync(laptop)).Succeeded);
        Assert.DoesNotContain(db.RefreshTokens, t => t.UserId == userId && t.RevokedAt == null);
    }

    [Fact]
    public async Task Refresh_KontoGeloescht_Verweigert_UndRaeumtDieTokensAuf()
    {
        var service = MakeService(out var db);
        var userId = Guid.NewGuid();
        var token = await service.IssueAsync(userId);
        await service.IssueAsync(userId);

        var ergebnis = await service.RefreshAsync(token);

        Assert.False(ergebnis.Succeeded);
        Assert.Empty(db.RefreshTokens.Where(t => t.UserId == userId));
    }

    [Fact]
    public async Task Refresh_VoruebergehendGesperrt_GehtWeiter()
    {
        var service = MakeService(out var db);
        // Sperre nach Fehlversuchen: ein paar Minuten, kein Grund, laufende
        // Sitzungen zu beenden (sonst sperrte jede:r mit der E-Mail-Adresse
        // andere aus).
        var userId = await LegeKontoAnAsync(db, DateTimeOffset.UtcNow.AddMinutes(5));
        var token = await service.IssueAsync(userId);

        var ergebnis = await service.RefreshAsync(token);

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(userId, ergebnis.UserId);
    }

    [Fact]
    public async Task Refresh_AbgelaufeneSperre_GehtWeiter()
    {
        var service = MakeService(out var db);
        var userId = await LegeKontoAnAsync(db, DateTimeOffset.UtcNow.AddMinutes(-1));
        var token = await service.IssueAsync(userId);

        Assert.True((await service.RefreshAsync(token)).Succeeded);
    }
}
