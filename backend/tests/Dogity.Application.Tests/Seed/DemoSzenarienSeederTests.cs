using Dogity.Application;
using Dogity.Application.Abstractions;
using Dogity.Application.Notifications;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Community;
using Dogity.Application.Dogs;
using Dogity.Application.Planning;
using Dogity.Application.Weather;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Learning;
using Dogity.Domain.Planning;
using Dogity.Domain.Preferences;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Identity;
using Dogity.Infrastructure.Persistence;
using Dogity.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dogity.Application.Tests.Seed;

/// <summary>
/// Die Demo-Szenarien laufen bei jedem Start der Test-Umgebung (Development).
/// Sie dürfen dabei nichts doppelt anlegen und ohne Demo-Konten nichts tun -
/// sonst wüchse die Demo-Datenbank mit jedem Neustart oder fremde Daten
/// bekämen Demo-Inhalte.
/// </summary>
public class DemoSzenarienSeederTests
{
    private static ServiceProvider BaueDienste(Action<IServiceCollection>? anpassen = null)
    {
        var datenbank = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(datenbank));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequiredLength = 8;
                o.Password.RequireNonAlphanumeric = false;
                o.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddMemoryCache();
        services.AddScoped<KontoStatus>();
        services.AddScoped<IUserLookupService, UserLookupService>();
        services.AddApplication();
        // Nach AddApplication, damit sie die echte Anreicherung ersetzt: kein Netz im Test.
        services.AddScoped<IWeatherEnrichmentService, FakeWeatherEnrichmentService>();
        // Was NotificationService zum Einreihen von Mail und Push braucht (sonst in
        // AddInfrastructure registriert): Fakes, die nichts versenden.
        services.AddSingleton<INotificationChannels, FakeNotificationChannels>();
        services.AddSingleton<IFrontendLinks>(TestLinks.Mit());
        services.AddSingleton<IEmailAbmeldeToken>(TestAbmeldeToken.Neu());
        services.AddSingleton<IMailQueue, FakeMailQueue>();
        services.AddSingleton<IPushQueue, FakePushQueue>();
        anpassen?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static async Task MitDemoKontenAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        await RoleSeeder.SeedAsync(scope.ServiceProvider);
        await SportCatalogSeeder.SeedAsync(scope.ServiceProvider);
        // Das Land (Tom: AT) wird nur angenommen, wenn es in der Länderliste steht.
        await CountrySeeder.SeedAsync(scope.ServiceProvider);
        await DemoDataSeeder.SeedAsync(scope.ServiceProvider);
    }

    private static async Task<IReadOnlyList<string>> SzenarienAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return await DemoSzenarienSeeder.SeedAsync(scope.ServiceProvider);
    }

    private static async Task<int[]> ZaehleAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return
        [
            await db.Users.CountAsync(),
            await db.Dogs.IgnoreQueryFilters().CountAsync(),
            await db.Goals.IgnoreQueryFilters().CountAsync(),
            await db.TrainingPlanItems.IgnoreQueryFilters().CountAsync(),
            await db.TrainingSessions.IgnoreQueryFilters().CountAsync(),
            await db.TrainingExercises.IgnoreQueryFilters().CountAsync(),
            await db.GpsTracks.IgnoreQueryFilters().CountAsync(),
            await db.GpsWalkRuns.IgnoreQueryFilters().CountAsync(),
            await db.GroupTrainingSessions.IgnoreQueryFilters().CountAsync(),
            await db.GroupTrainingSessionResponses.IgnoreQueryFilters().CountAsync(),
            await db.GroupRegistrations.IgnoreQueryFilters().CountAsync(),
            await db.GroupRegistrationAttendances.IgnoreQueryFilters().CountAsync(),
            await db.ClubMemberships.IgnoreQueryFilters().CountAsync(),
            await db.Notifications.IgnoreQueryFilters().CountAsync(),
            await db.UserPreferences.IgnoreQueryFilters().CountAsync(),
            await db.DogOwners.IgnoreQueryFilters().CountAsync(),
            await db.ClubRegistrations.IgnoreQueryFilters().CountAsync(),
            await db.GpsPoints.IgnoreQueryFilters().CountAsync(),
            await db.DogPauses.IgnoreQueryFilters().CountAsync(),
            await db.DogHealthRecords.IgnoreQueryFilters().CountAsync(),
            await db.Groups.IgnoreQueryFilters().CountAsync(),
            await db.GroupMembers.IgnoreQueryFilters().CountAsync(),
            await db.ClubTrainers.IgnoreQueryFilters().CountAsync(),
        ];
    }

    [Fact]
    public async Task OhneDemoKonten_PassiertNichts()
    {
        await using var provider = BaueDienste();

        var fehler = await SzenarienAsync(provider);

        Assert.Empty(fehler);
        Assert.All(await ZaehleAsync(provider), anzahl => Assert.Equal(0, anzahl));
    }

    [Fact]
    public async Task ZweimaligesAusfuehren_ErzeugtKeineDuplikate()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);

        Assert.Empty(await SzenarienAsync(provider));
        var nachErstemLauf = await ZaehleAsync(provider);

        Assert.Empty(await SzenarienAsync(provider));
        Assert.Equal(nachErstemLauf, await ZaehleAsync(provider));
    }

    [Fact]
    public async Task WasInDerDemoGeloeschtOderEntschiedenWurde_KommtNichtZurueck()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var jetzt = DateTimeOffset.UtcNow;
            // Wie ein Tester in der Demo: eine Anmeldung und ein Ziel löschen, die Anfrage ablehnen.
            (await db.GroupRegistrations.FirstAsync()).DeletedAt = jetzt;
            (await db.Goals.FirstAsync(g => g.Notes == DemoSzenarienSeeder.NotizUprZiel)).DeletedAt = jetzt;
            (await db.ClubMemberships.FirstAsync(m => m.Source == ClubMembershipSource.InviteLink)).Status = ClubMembershipStatus.Rejected;
            await db.SaveChangesAsync();
        }

        var davor = await ZaehleAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));
        Assert.Equal(davor, await ZaehleAsync(provider));

        using var pruefen = provider.CreateScope();
        var pruefDb = pruefen.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.DoesNotContain(await pruefDb.ClubMemberships.ToListAsync(), m => m.Source == ClubMembershipSource.InviteLink && m.Status == ClubMembershipStatus.Pending);
    }

    [Fact]
    public async Task Szenarien_LegenDieErwartetenDatenAn()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var trainerId = (await users.FindByEmailAsync("trainer@dogity.test"))!.Id;
        var maxId = (await users.FindByEmailAsync("mitglied1@dogity.test"))!.Id;
        var neulingId = (await users.FindByEmailAsync(DemoSzenarienSeeder.NeulingEmail))!.Id;
        var jetzt = DateTimeOffset.UtcNow;

        // a) Verein: Link gesetzt, Anfrage über den Link, Benachrichtigung an die Trainerin
        var verein = await db.Clubs.SingleAsync();
        Assert.False(string.IsNullOrEmpty(verein.InviteCode));
        var anfrage = await db.ClubMemberships.SingleAsync(m => m.UserId == neulingId);
        Assert.Equal(ClubMembershipStatus.Pending, anfrage.Status);
        Assert.Equal(ClubMembershipSource.InviteLink, anfrage.Source);
        Assert.Contains(await db.Notifications.Where(n => n.UserId == trainerId).ToListAsync(), n => n.Message.Contains("Nina"));

        // b) Ziele: zwei erreichte (eines ohne, eines mit Punkten), ein aktives mit Plan und Fortschritt, ein überfälliges
        var ziele = await db.Goals.Include(g => g.TrainingPlan).ToListAsync();
        var erreicht = ziele.Where(g => g.Status == GoalStatus.Achieved).ToList();
        Assert.Equal(2, erreicht.Count);
        Assert.Contains(erreicht, g => g.ExamScore is null && g.ExamDate is not null);
        Assert.Contains(erreicht, g => g.ExamScore is { } punkte && punkte is > 0 and < 100);
        var heute = DateOnly.FromDateTime(DateTime.UtcNow);
        var aktiv = ziele.Single(g => g.Notes == DemoSzenarienSeeder.NotizUprZiel);
        Assert.Equal(GoalStatus.Active, aktiv.Status);
        Assert.InRange(aktiv.TargetDate.DayNumber - heute.DayNumber, 21, 40);
        var planIds = await db.TrainingPlanItems.Where(i => i.TrainingPlanId == aktiv.TrainingPlan!.Id).Select(i => i.Id).ToListAsync();
        Assert.NotEmpty(planIds);
        Assert.True(await db.TrainingExercises.AnyAsync(e => e.TrainingPlanItemId != null && planIds.Contains(e.TrainingPlanItemId.Value)));
        Assert.Contains(ziele, g => g.Notes == DemoSzenarienSeeder.NotizFaehrteOffen && g.Status == GoalStatus.Active && g.TargetDate < heute);

        // c) Fährten: vier, jede mit ausgewertetem Ablauf (vom GpsTrackEvaluator) und Temperaturen
        var tracks = await db.GpsTracks.Include(t => t.WalkRuns).Where(t => t.Comment != null && DemoSzenarienSeeder.FaehrtenKommentare.Contains(t.Comment)).ToListAsync();
        Assert.Equal(4, tracks.Count);
        Assert.All(tracks, t =>
        {
            var lauf = Assert.Single(t.WalkRuns);
            Assert.NotNull(lauf.EvaluatedAt);
            Assert.NotNull(lauf.AvgDeviationMeters);
            Assert.Equal(3, lauf.ArticlesTotal);
            Assert.NotNull(t.TemperatureDeltaC);
        });
        // Die Abweichung wird von Fährte zu Fährte kleiner - das ist der Trend, den die Demo zeigen soll.
        var abweichungen = tracks
            .OrderBy(t => Array.IndexOf(DemoSzenarienSeeder.FaehrtenKommentare, t.Comment))
            .Select(t => t.WalkRuns.Single().AvgDeviationMeters!.Value)
            .ToList();
        Assert.True(abweichungen.Zip(abweichungen.Skip(1), (davor, danach) => danach < davor).All(besser => besser),
            $"Abweichung je Fährte: {string.Join(" > ", abweichungen.Select(a => a.ToString("0.0")))}");

        // d) Feedback: eines ohne Reaktion, eines mit Verstanden und Rückfrage (Benachrichtigung an die Trainerin)
        var ohne = await db.TrainingSessions.SingleAsync(s => s.Notes == DemoSzenarienSeeder.NotizFeedbackOhneReaktion);
        Assert.False(string.IsNullOrEmpty(ohne.TrainerFeedback));
        Assert.Null(ohne.OwnerReaction);
        Assert.Contains(await db.Notifications.Where(n => n.UserId == maxId).ToListAsync(), n => n.LinkPath == $"/dogs/{ohne.DogId}?eintrag={ohne.Id}");
        var mit = await db.TrainingSessions.SingleAsync(s => s.Notes == DemoSzenarienSeeder.NotizFeedbackMitRueckfrage);
        Assert.Equal(Dogity.Domain.Training.FeedbackReaction.Understood, mit.OwnerReaction);
        Assert.False(string.IsNullOrEmpty(mit.OwnerReply));
        Assert.Contains(await db.Notifications.Where(n => n.UserId == trainerId).ToListAsync(), n => n.LinkPath == $"/dogs/{mit.DogId}?eintrag={mit.Id}");

        // e) Termine: mindestens zwei künftige geplante, ein abgesagter, vergangene; Zu- und Absage beim nächsten
        // (die der Welpengruppe - die Junghunde von Tina Training haben ihren eigenen Termin, siehe Junghunde_...)
        var termine = await db.GroupTrainingSessions.Include(s => s.Trainers)
            .Where(s => s.Group!.Name != DemoSzenarienSeeder.JunghundeGruppenName).ToListAsync();
        var kuenftig = termine.Where(s => s.Status == GroupTrainingSessionStatus.Planned && s.StartsAt > jetzt && s.StartsAt <= jetzt.AddDays(14)).OrderBy(s => s.StartsAt).ToList();
        Assert.True(kuenftig.Count >= 2);
        Assert.All(kuenftig, s => Assert.Contains(s.Trainers, t => t.UserId == trainerId));
        Assert.Contains(termine, s => s.Status == GroupTrainingSessionStatus.Cancelled && s.StartsAt > jetzt);
        Assert.True(termine.Count(s => s.StartsAt <= jetzt) >= 2);
        var antworten = await db.GroupTrainingSessionResponses.Where(r => r.GroupTrainingSessionId == kuenftig[0].Id).ToListAsync();
        Assert.Contains(antworten, r => r.UserId == maxId && r.IsAttending);
        Assert.Contains(antworten, r => r.UserId != maxId && !r.IsAttending);
        Assert.Contains(await db.Notifications.Where(n => n.UserId == trainerId).ToListAsync(), n => n.Message.Contains("abgesagt"));

        // f) Anmeldungen: sieben, vier bezahlt, gemischte Quellen, eine mit Notiz, Anwesenheit unterschiedlich oft
        var gruppe = await db.Groups.SingleAsync(g => g.Name != DemoSzenarienSeeder.JunghundeGruppenName);
        Assert.False(string.IsNullOrEmpty(gruppe.RegistrationCode));
        var anmeldungen = await db.GroupRegistrations.Include(r => r.Attendances).ToListAsync();
        Assert.Equal(7, anmeldungen.Count);
        Assert.Equal(4, anmeldungen.Count(r => r.PaidAt is not null));
        Assert.Equal(3, anmeldungen.Select(r => r.Source).Distinct().Count());
        Assert.Single(anmeldungen, r => r.Notes is not null);
        Assert.True(anmeldungen.Select(r => r.Attendances.Count).Distinct().Count() >= 3);
        Assert.Contains(await db.Notifications.Where(n => n.UserId == trainerId).ToListAsync(), n => n.Message.StartsWith("Neue Anmeldung"));
    }

    // ---------------------------------------------------------------- Vereinsrollen: Tina Training (V3)

    [Fact]
    public async Task Junghunde_TinaHatNurTrainingUndLeitetEineGruppe_DieAnnaNichtLeitet()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var gruppen = scope.ServiceProvider.GetRequiredService<IGroupService>();
        var tinaId = await IdAsync(scope.ServiceProvider, DemoSzenarienSeeder.TrainingEmail);
        var annaId = await IdAsync(scope.ServiceProvider, "trainer@dogity.test");
        var lisaId = await IdAsync(scope.ServiceProvider, "mitglied2@dogity.test");
        var williId = await IdAsync(scope.ServiceProvider, DemoSzenarienSeeder.AusbildungswartEmail);
        var verein = await db.Clubs.SingleAsync();

        // Nur Training, und als freigegebenes Mitglied im Demo-Verein.
        Assert.Equal(ClubRole.Training, await db.GetClubRolesAsync(tinaId, verein.Id));
        Assert.True(await db.ClubMemberships.AnyAsync(m => m.UserId == tinaId && m.ClubId == verein.Id && m.Status == ClubMembershipStatus.Approved));

        // Die Gruppe: von Tina geleitet, Lisa ist Mitglied, ein künftiger Termin mit Tina als Trainerin.
        var junghunde = await db.Groups.SingleAsync(g => g.Name == DemoSzenarienSeeder.JunghundeGruppenName);
        Assert.Equal(tinaId, junghunde.TrainerId);
        Assert.Equal(verein.Id, junghunde.ClubId);
        Assert.Single(await db.GroupMembers.Where(m => m.GroupId == junghunde.Id).ToListAsync(), m => m.UserId == lisaId && m.Status == GroupMemberStatus.Active);
        var termin = await db.GroupTrainingSessions.Include(s => s.Trainers).SingleAsync(s => s.GroupId == junghunde.Id);
        Assert.True(termin.StartsAt > DateTimeOffset.UtcNow);
        Assert.Contains(termin.Trainers, t => t.UserId == tinaId);

        // Was jede Rolle sieht: Training nur die eigene Gruppe, Verwaltung und Ausbildungswart:in beide.
        Assert.Equal([DemoSzenarienSeeder.JunghundeGruppenName], (await gruppen.GetMyGroupsAsync(tinaId)).Value!.Select(g => g.Name).ToArray());
        Assert.Equal(2, (await gruppen.GetMyGroupsAsync(annaId)).Value!.Count);
        Assert.Equal(2, (await gruppen.GetMyGroupsAsync(williId)).Value!.Count);
        // Anna verwaltet die Gruppe, ohne sie zu leiten.
        Assert.NotEqual(annaId, junghunde.TrainerId);
        Assert.True(await db.CanManageAllGroupsAsync(annaId, verein.Id));
        Assert.False(await db.CanManageAllGroupsAsync(tinaId, verein.Id));
    }

    [Fact]
    public async Task Junghunde_ZweimaligesAusfuehren_LegtNichtsDoppeltAn()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));
        Assert.Empty(await SzenarienAsync(provider));

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var junghunde = await db.Groups.SingleAsync(g => g.Name == DemoSzenarienSeeder.JunghundeGruppenName);
        Assert.Single(await db.GroupMembers.Where(m => m.GroupId == junghunde.Id).ToListAsync());
        Assert.Single(await db.GroupTrainingSessions.Where(s => s.GroupId == junghunde.Id).ToListAsync());
        Assert.Single(await db.ClubTrainers.Where(t => t.UserId == (db.Users.Single(u => u.Email == DemoSzenarienSeeder.TrainingEmail).Id)).ToListAsync());
    }

    [Fact]
    public async Task Junghunde_WasEinTesterGeloeschtHat_KommtNichtZurueck()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var junghunde = await db.Groups.SingleAsync(g => g.Name == DemoSzenarienSeeder.JunghundeGruppenName);
            junghunde.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        Assert.Empty(await SzenarienAsync(provider));

        using var pruefen = provider.CreateScope();
        var pruefDb = pruefen.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var alle = await pruefDb.Groups.IgnoreQueryFilters().Where(g => g.Name == DemoSzenarienSeeder.JunghundeGruppenName).ToListAsync();
        Assert.Single(alle);
        Assert.NotNull(alle[0].DeletedAt);
    }

    [Fact]
    public async Task Junghunde_AufEinerDatenbankOhneTina_ZiehtSieNach()
    {
        // Die bestehende Test-Datenbank kennt die Rollen-Konten aus dem ersten Vereinsrollen-Stand, aber nicht Tina.
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var tina = (await users.FindByEmailAsync(DemoSzenarienSeeder.TrainingEmail))!;
            var junghunde = await db.Groups.SingleAsync(g => g.Name == DemoSzenarienSeeder.JunghundeGruppenName);
            db.GroupTrainingSessions.RemoveRange(await db.GroupTrainingSessions.Where(s => s.GroupId == junghunde.Id).ToListAsync());
            db.GroupMembers.RemoveRange(await db.GroupMembers.Where(m => m.GroupId == junghunde.Id).ToListAsync());
            db.Groups.Remove(junghunde);
            db.ClubTrainers.RemoveRange(await db.ClubTrainers.IgnoreQueryFilters().Where(t => t.UserId == tina.Id).ToListAsync());
            db.ClubMemberships.RemoveRange(await db.ClubMemberships.IgnoreQueryFilters().Where(m => m.UserId == tina.Id).ToListAsync());
            db.Notifications.RemoveRange(await db.Notifications.IgnoreQueryFilters().Where(n => n.UserId == tina.Id).ToListAsync());
            await db.SaveChangesAsync();
            Assert.True((await users.DeleteAsync(tina)).Succeeded);
        }

        Assert.Empty(await SzenarienAsync(provider));

        using var pruefen = provider.CreateScope();
        var pruefDb = pruefen.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var neueTina = await IdAsync(pruefen.ServiceProvider, DemoSzenarienSeeder.TrainingEmail);
        var gruppe = await pruefDb.Groups.SingleAsync(g => g.Name == DemoSzenarienSeeder.JunghundeGruppenName);
        Assert.Equal(neueTina, gruppe.TrainerId);
        Assert.Single(await pruefDb.GroupTrainingSessions.Where(s => s.GroupId == gruppe.Id).ToListAsync());
    }

    // ---------------------------------------------------------------- Szenarien seit 2026-10-16

    private static async Task<Guid> IdAsync(IServiceProvider scopeProvider, string email) =>
        (await scopeProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email))!.Id;

    private static DateTimeOffset GesternUm(int stunde)
    {
        var gestern = Vereinszeit.Heute().AddDays(-1);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(gestern.ToDateTime(new TimeOnly(stunde, 0), DateTimeKind.Unspecified), Vereinszeit.Zone), TimeSpan.Zero);
    }

    [Fact]
    public async Task NeueSzenarien_LegenDieErwartetenDatenAn()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var maxId = await IdAsync(sp, "mitglied1@dogity.test");
        var lisaId = await IdAsync(sp, "mitglied2@dogity.test");
        var tomId = await IdAsync(sp, DemoSzenarienSeeder.InteressentEmail);
        var ninaId = await IdAsync(sp, DemoSzenarienSeeder.NeulingEmail);
        var adminId = await IdAsync(sp, "admin@dogity.test");

        // Englisch: Lisa bekommt die Sprache, aber kein Land (englischsprachig in Deutschland)
        var lisaEinstellung = await db.UserPreferences.SingleAsync(p => p.UserId == lisaId);
        Assert.Equal("en", lisaEinstellung.Locale);
        Assert.Null(lisaEinstellung.Country);

        // Mitbesitz: eine Einladung (kein Mitbesitz), die Benachrichtigung an Lisa ist englisch
        var emma = await db.Dogs.SingleAsync(d => d.Name == "Emma");
        var einladung = await db.DogOwners.IgnoreQueryFilters().SingleAsync(o => o.DogId == emma.Id && o.UserId == lisaId);
        Assert.Equal(DogOwnerStatus.Invited, einladung.Status);
        Assert.Equal(maxId, einladung.InvitedByUserId);
        var lisasMeldungen = await db.Notifications.Where(n => n.UserId == lisaId).Select(n => n.Message).ToListAsync();
        Assert.Contains(lisasMeldungen, m => m.Contains("invites you to manage Emma together"));
        Assert.DoesNotContain(lisasMeldungen, m => m.Contains("lädt dich ein"));

        // Österreich: Tom bekommt das Land, die Beitrittsanfrage an den Verein bleibt offen
        Assert.Equal("AT", (await db.UserPreferences.SingleAsync(p => p.UserId == tomId)).Country);
        Assert.Equal(ClubMembershipStatus.Pending, (await db.ClubMemberships.SingleAsync(m => m.UserId == tomId)).Status);

        // Vereinsantrag: offen, mit Benachrichtigung an den Admin
        var antrag = await db.ClubRegistrations.SingleAsync(r => r.RequestedByUserId == tomId);
        Assert.Equal(DemoSzenarienSeeder.VereinsAntragName, antrag.Name);
        Assert.Equal(ClubRegistrationStatus.Pending, antrag.Status);
        Assert.Contains(await db.Notifications.Where(n => n.UserId == adminId).ToListAsync(), n => n.Message.Contains(DemoSzenarienSeeder.VereinsAntragName));
        Assert.Equal(1, await db.Clubs.CountAsync());

        // Hoopers: Ninas Hund mit einem Ziel H0 samt Plan, Prüfung nach dem Beginn der Ordnung (1.1.2027)
        var flitzer = await db.Dogs.SingleAsync(d => d.Name == "Flitzer");
        Assert.True(await db.DogOwners.AnyAsync(o => o.DogId == flitzer.Id && o.UserId == ninaId));
        var ziel = await db.Goals.Include(g => g.TrainingPlan).SingleAsync(g => g.Notes == DemoSzenarienSeeder.NotizHoopersZiel);
        Assert.Equal(flitzer.Id, ziel.DogId);
        Assert.Equal(GoalStatus.Active, ziel.Status);
        Assert.Equal("Hoopers 0 (H0)", (await db.Regulations.SingleAsync(r => r.Id == ziel.RegulationId)).Name);
        Assert.True(ziel.TargetDate > new DateOnly(2027, 1, 1));
        Assert.NotEmpty(await db.TrainingPlanItems.Where(i => i.TrainingPlanId == ziel.TrainingPlan!.Id).ToListAsync());

        // Nachtfährte: gestern 22 Uhr (Ortszeit) gelegt, ohne Ablauf, mit Punkten
        var nacht = await db.GpsTracks.Include(t => t.Points).Include(t => t.WalkRuns).SingleAsync(t => t.Comment == DemoSzenarienSeeder.KommentarNachtfaehrte);
        Assert.Empty(nacht.WalkRuns);
        Assert.Equal(GesternUm(22), nacht.Points.Min(p => p.Timestamp));
        Assert.Equal(Vereinszeit.Heute().AddDays(-1), (await db.TrainingSessions.SingleAsync(s => s.Id == nacht.TrainingSessionId)).Date);
    }

    [Fact]
    public async Task Schweiz_LegtKontoLandHundUndZielAn()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var sandroId = await IdAsync(sp, DemoSzenarienSeeder.SchweizEmail);

        Assert.Equal("CH", (await db.UserPreferences.SingleAsync(p => p.UserId == sandroId)).Country);
        var hund = await db.Dogs.SingleAsync(d => d.Name == "Bläss");
        Assert.True(await db.DogOwners.AnyAsync(o => o.DogId == hund.Id && o.UserId == sandroId));
        var ziel = await db.Goals.Include(g => g.TrainingPlan).SingleAsync(g => g.Notes == DemoSzenarienSeeder.NotizSkgBhZiel);
        Assert.Equal(hund.Id, ziel.DogId);
        Assert.Equal(GoalStatus.Active, ziel.Status);
        Assert.Equal("SKG BH 1", (await db.Regulations.SingleAsync(r => r.Id == ziel.RegulationId)).Name);
        Assert.Equal("BHCH", (await db.Sports.SingleAsync(s => s.Id == ziel.SportId)).Code);
        Assert.NotEmpty(await db.TrainingPlanItems.Where(i => i.TrainingPlanId == ziel.TrainingPlan!.Id).ToListAsync());
    }

    [Fact]
    public async Task Schweiz_LandUndGeloeschtesZielBleibenWieDerTesterSieLiess()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sandroId = await IdAsync(scope.ServiceProvider, DemoSzenarienSeeder.SchweizEmail);
            // Wie ein Tester: Land umstellen, Ziel samt Hund löschen.
            (await db.UserPreferences.SingleAsync(p => p.UserId == sandroId)).Country = "AT";
            var ziel = await db.Goals.SingleAsync(g => g.Notes == DemoSzenarienSeeder.NotizSkgBhZiel);
            ziel.DeletedAt = DateTimeOffset.UtcNow;
            (await db.Dogs.SingleAsync(d => d.Id == ziel.DogId)).DeletedAt = DateTimeOffset.UtcNow;
            (await db.DogOwners.SingleAsync(o => o.DogId == ziel.DogId)).DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        var davor = await ZaehleAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));
        Assert.Equal(davor, await ZaehleAsync(provider));

        using var pruefen = provider.CreateScope();
        var pruefDb = pruefen.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = await IdAsync(pruefen.ServiceProvider, DemoSzenarienSeeder.SchweizEmail);
        Assert.Equal("AT", (await pruefDb.UserPreferences.SingleAsync(p => p.UserId == id)).Country);
        Assert.Single(await pruefDb.Dogs.IgnoreQueryFilters().Where(d => d.Name == "Bläss").ToListAsync());
    }

    [Fact]
    public async Task Seeder_FragtKeinWetterAnUndReihtKeinesEin()
    {
        var wetterdienst = new ZaehlenderWetterdienst();
        var queue = new LeereWetterQueue();
        // Die ECHTE Anreicherung statt des Fakes: So fiele auf, wenn der Seeder irgendwo einreiht.
        await using var provider = BaueDienste(services =>
        {
            services.AddScoped<IWeatherEnrichmentService, WeatherEnrichmentService>();
            services.AddSingleton<IWeatherProvider>(wetterdienst);
            services.AddSingleton<IWetterQueue>(queue);
        });
        await MitDemoKontenAsync(provider);

        Assert.Empty(await SzenarienAsync(provider));

        using var scope = provider.CreateScope();
        Assert.NotEmpty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().GpsTracks.ToListAsync());
        Assert.Equal(0, wetterdienst.Aufrufe);
        Assert.Equal(0, queue.Eingereiht);
    }

    private sealed class ZaehlenderWetterdienst : IWeatherProvider
    {
        public int Aufrufe { get; private set; }

        public Task<WeatherReading?> GetAtAsync(double latitude, double longitude, DateTimeOffset instant, CancellationToken ct = default)
        {
            Aufrufe++;
            return Task.FromResult<WeatherReading?>(null);
        }
    }

    private sealed class LeereWetterQueue : IWetterQueue
    {
        public int Eingereiht { get; private set; }

        public bool Enqueue(WetterAuftrag auftrag)
        {
            Eingereiht++;
            return true;
        }
    }

    [Fact]
    public async Task EinstellungenDerTester_BleibenErhalten()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);

        Guid lisaId, tomId;
        using (var scope = provider.CreateScope())
        {
            lisaId = await IdAsync(scope.ServiceProvider, "mitglied2@dogity.test");
            tomId = await IdAsync(scope.ServiceProvider, DemoSzenarienSeeder.InteressentEmail);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Wie nach einer Umstellung im Profil, bevor der Seeder je lief.
            db.UserPreferences.Add(new UserPreference { UserId = lisaId, Locale = "de" });
            db.UserPreferences.Add(new UserPreference { UserId = tomId, Country = "CH" });
            await db.SaveChangesAsync();
        }

        Assert.Empty(await SzenarienAsync(provider));

        using var pruefen = provider.CreateScope();
        var pruefDb = pruefen.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("de", (await pruefDb.UserPreferences.SingleAsync(p => p.UserId == lisaId)).Locale);
        Assert.Equal("CH", (await pruefDb.UserPreferences.SingleAsync(p => p.UserId == tomId)).Country);
        // Die Einladung entsteht trotzdem - dann eben in Lisas gespeicherter Sprache.
        Assert.Contains(await pruefDb.Notifications.Where(n => n.UserId == lisaId).ToListAsync(), n => n.Message.Contains("lädt dich ein"));
    }

    [Fact]
    public async Task WasInDerDemoEntschiedenWurde_KommtBeiDenNeuenSzenarienNichtZurueck()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var jetzt = DateTimeOffset.UtcNow;
            // Wie Tester in der Demo: Antrag ablehnen, Einladung ablehnen (Zeile weg), Hoopers-Ziel samt
            // Hund und die Nachtfährte löschen.
            (await db.ClubRegistrations.SingleAsync()).Status = ClubRegistrationStatus.Rejected;
            (await db.DogOwners.IgnoreQueryFilters().SingleAsync(o => o.Status == DogOwnerStatus.Invited)).DeletedAt = jetzt;
            var hoopers = await db.Goals.SingleAsync(g => g.Notes == DemoSzenarienSeeder.NotizHoopersZiel);
            hoopers.DeletedAt = jetzt;
            (await db.Dogs.SingleAsync(d => d.Id == hoopers.DogId)).DeletedAt = jetzt;
            (await db.DogOwners.SingleAsync(o => o.DogId == hoopers.DogId)).DeletedAt = jetzt;
            (await db.GpsTracks.SingleAsync(t => t.Comment == DemoSzenarienSeeder.KommentarNachtfaehrte)).DeletedAt = jetzt;
            await db.SaveChangesAsync();
        }

        var davor = await ZaehleAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));
        Assert.Equal(davor, await ZaehleAsync(provider));

        using var pruefen = provider.CreateScope();
        var pruefDb = pruefen.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.DoesNotContain(await pruefDb.ClubRegistrations.ToListAsync(), r => r.Status == ClubRegistrationStatus.Pending);
        Assert.Empty(await pruefDb.DogOwners.Where(o => o.Status == DogOwnerStatus.Invited).ToListAsync());
    }

    [Fact]
    public async Task HatNinaSchonEinenHund_BekommtDieserDasHoopersZiel()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);

        // Nina gibt es schon (der Seeder nimmt das vorhandene Konto): ein früher gelöschter Hund
        // zählt nicht, ein vorhandener bekommt das Ziel statt eines zweiten Hundes.
        Guid hundId;
        using (var scope = provider.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nina = new ApplicationUser { UserName = DemoSzenarienSeeder.NeulingEmail, Email = DemoSzenarienSeeder.NeulingEmail, EmailConfirmed = true, FirstName = "Nina", LastName = "Neuling" };
            Assert.True((await users.CreateAsync(nina, "Demo1234!")).Succeeded);
            var alt = new Dog { Name = "Alt", Breed = "Mischling", Gender = DogGender.Male, DeletedAt = DateTimeOffset.UtcNow };
            var hund = new Dog { Name = "Rex", Breed = "Beagle", Gender = DogGender.Male };
            hundId = hund.Id;
            db.Dogs.AddRange(alt, hund);
            db.DogOwners.Add(new DogOwner { DogId = alt.Id, UserId = nina.Id, DeletedAt = DateTimeOffset.UtcNow });
            db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = nina.Id });
            await db.SaveChangesAsync();
        }

        Assert.Empty(await SzenarienAsync(provider));

        using var pruefen = provider.CreateScope();
        var pruefDb = pruefen.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.DoesNotContain(await pruefDb.Dogs.IgnoreQueryFilters().ToListAsync(), d => d.Name == "Flitzer");
        Assert.Equal(hundId, (await pruefDb.Goals.SingleAsync(g => g.Notes == DemoSzenarienSeeder.NotizHoopersZiel)).DogId);
    }

    // --- Sachkunde-Übungsprüfung ---------------------------------------------------

    private static async Task MitSachkundeAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        await SachkundeSeeder.SeedAsync(scope.ServiceProvider);
    }

    [Fact]
    public async Task Sachkunde_MaxHatZweiUebungspruefungen_EineDurchEineBestanden()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        await MitSachkundeAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var max = (await users.FindByEmailAsync("mitglied1@dogity.test"))!;

        var versuche = await db.QuizExamAttempts.Where(a => a.UserId == max.Id).OrderBy(a => a.SubmittedAt).ToListAsync();
        Assert.Equal(2, versuche.Count);
        Assert.False(versuche[0].Passed);
        Assert.Equal(30, versuche[0].Points);
        Assert.True(versuche[1].Passed);
        Assert.Equal(40, versuche[1].Points);
        Assert.All(versuche, a => Assert.Equal(50, a.MaxPoints));
        // Vergangen, in der richtigen Reihenfolge.
        Assert.True(versuche[0].SubmittedAt < DateTimeOffset.UtcNow.AddDays(-9));
        Assert.True(versuche[1].SubmittedAt < DateTimeOffset.UtcNow.AddDays(-2));
        // Die falsch beantworteten Fragen liegen im Fehlerspeicher.
        Assert.True(await db.QuizMasteries.AnyAsync(m => m.UserId == max.Id && !m.LastWasCorrect));
        // Und nur Max hat etwas bekommen.
        Assert.Equal(2, await db.QuizExamAttempts.CountAsync());
    }

    [Fact]
    public async Task Sachkunde_ZweimaligesAusfuehren_ErzeugtKeineDuplikate()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        await MitSachkundeAsync(provider);

        Assert.Empty(await SzenarienAsync(provider));
        Assert.Empty(await SzenarienAsync(provider));

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await db.QuizExamAttempts.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Sachkunde_OhneKataloge_EntfaelltDasSzenario()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);

        Assert.Empty(await SzenarienAsync(provider));

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(db.QuizExamAttempts.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Sachkunde_WasEinTesterEntferntHat_KommtNichtZurueck()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        await MitSachkundeAsync(provider);
        await SzenarienAsync(provider);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var a in await db.QuizExamAttempts.ToListAsync()) a.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        Assert.Empty(await SzenarienAsync(provider));

        using var pruef = provider.CreateScope();
        var pruefDb = pruef.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(pruefDb.QuizExamAttempts);   // weiter ausgeblendet, nicht neu angelegt
        Assert.Equal(2, await pruefDb.QuizExamAttempts.IgnoreQueryFilters().CountAsync());
    }

    // --- Trainingspausen ----------------------------------------------------------

    private static async Task<Guid> HundIdAsync(IServiceProvider provider, string name)
    {
        using var scope = provider.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Dogs.SingleAsync(d => d.Name == name)).Id;
    }

    [Fact]
    public async Task Trainingspausen_NinasHuendinIstLaeufig_BelloHatEineAbgeschlosseneVerletzung()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var heute = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin")).DateTime);
        var ninaId = (await users.FindByEmailAsync(DemoSzenarienSeeder.NeulingEmail))!.Id;

        var flitzer = await db.Dogs.SingleAsync(d => d.Name == "Flitzer");
        var laeufig = await db.DogPauses.SingleAsync(p => p.DogId == flitzer.Id);
        Assert.Equal(DogPauseReason.InHeat, laeufig.Reason);
        Assert.Equal(DemoSzenarienSeeder.NotizPauseLaeufig, laeufig.Note);
        Assert.Equal(heute.AddDays(-5), laeufig.StartDate);
        Assert.Equal(heute.AddDays(16), laeufig.EndDate);
        Assert.Equal(ninaId, laeufig.CreatedByUserId);

        var bello = await db.Dogs.SingleAsync(d => d.Name == "Bello");
        var verletzung = await db.DogPauses.SingleAsync(p => p.DogId == bello.Id);
        Assert.Equal(DogPauseReason.Injured, verletzung.Reason);
        Assert.True(verletzung.EndDate < heute);
    }

    [Fact]
    public async Task Trainingspausen_LaeufigkeitLaeuftRollend_NachIhremNatuerlichenEnde()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));
        var flitzerId = await HundIdAsync(provider, "Flitzer");

        // Um 25 Tage zurückgeschoben: Das Ende (Beginn + 21 Tage) lag nach dem Verschieben neun Tage zurück.
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pause = await db.DogPauses.SingleAsync(p => p.DogId == flitzerId);
            const int verschiebung = 25;
            pause.StartDate = pause.StartDate.AddDays(-verschiebung);
            pause.EndDate = pause.EndDate!.Value.AddDays(-verschiebung);
            await db.SaveChangesAsync();
        }

        Assert.Empty(await SzenarienAsync(provider));

        using var pruefen = provider.CreateScope();
        var pausen = await pruefen.ServiceProvider.GetRequiredService<ApplicationDbContext>().DogPauses
            .Where(p => p.DogId == flitzerId).OrderBy(p => p.StartDate).ToListAsync();
        Assert.Equal(2, pausen.Count);
        Assert.True(pausen[0].EndDate < pausen[1].StartDate);
        Assert.True(pausen[1].EndDate > pausen[1].StartDate.AddDays(10));
    }

    [Fact]
    public async Task Trainingspausen_WasEinTesterBeendetOderGeloeschtHat_KommtNichtZurueck()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));
        var flitzerId = await HundIdAsync(provider, "Flitzer");

        // Vorzeitig beendet (gestern war der letzte Tag), nicht die vollen 21 Tage.
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pause = await db.DogPauses.SingleAsync(p => p.DogId == flitzerId);
            pause.EndDate = pause.StartDate.AddDays(3);
            await db.SaveChangesAsync();
        }
        Assert.Empty(await SzenarienAsync(provider));

        // Und gelöscht.
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.DogPauses.SingleAsync(p => p.DogId == flitzerId)).DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
        Assert.Empty(await SzenarienAsync(provider));

        using var pruefen = provider.CreateScope();
        var pruefDb = pruefen.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await pruefDb.DogPauses.IgnoreQueryFilters().CountAsync(p => p.DogId == flitzerId));
    }

    [Fact]
    public async Task Trainingspausen_EigenePauseDesTestersImSelbenZeitraum_GehtVor()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        // Der Tester ersetzt die Demo-Pause durch eine eigene, die bis auf Weiteres gilt.
        Assert.Empty(await SzenarienAsync(provider));
        var flitzerId = await HundIdAsync(provider, "Flitzer");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var demo = await db.DogPauses.SingleAsync(p => p.DogId == flitzerId);
            demo.DeletedAt = DateTimeOffset.UtcNow;
            db.DogPauses.Add(new DogPause { DogId = flitzerId, Reason = DogPauseReason.Sick, StartDate = demo.StartDate, EndDate = null });
            await db.SaveChangesAsync();
        }

        Assert.Empty(await SzenarienAsync(provider));

        using var pruefen = provider.CreateScope();
        var pausen = await pruefen.ServiceProvider.GetRequiredService<ApplicationDbContext>().DogPauses.Where(p => p.DogId == flitzerId).ToListAsync();
        Assert.Equal(DogPauseReason.Sick, Assert.Single(pausen).Reason);
    }

    [Fact]
    public async Task Trainingspausen_NurEinRuedeBeiNina_KeineLaeufigkeit()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        using (var scope = provider.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nina = new ApplicationUser { UserName = DemoSzenarienSeeder.NeulingEmail, Email = DemoSzenarienSeeder.NeulingEmail, EmailConfirmed = true, FirstName = "Nina", LastName = "Neuling" };
            Assert.True((await users.CreateAsync(nina, "Demo1234!")).Succeeded);
            var hund = new Dog { Name = "Rex", Breed = "Beagle", Gender = DogGender.Male };
            db.Dogs.Add(hund);
            db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = nina.Id });
            await db.SaveChangesAsync();
        }

        Assert.Empty(await SzenarienAsync(provider));

        using var pruefen = provider.CreateScope();
        var pruefDb = pruefen.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.DoesNotContain(await pruefDb.DogPauses.ToListAsync(), p => p.Reason == DogPauseReason.InHeat);
    }

    [Fact]
    public async Task Nachtfaehrte_RuecktAufGesternSolangeKeinAblaufExistiert()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        // Fünf Tage später: Fährte, Einheit und alle Punkte liegen fünf Tage zurück.
        Guid trackId;
        TimeSpan spanne;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var track = await db.GpsTracks.Include(t => t.Points).SingleAsync(t => t.Comment == DemoSzenarienSeeder.KommentarNachtfaehrte);
            trackId = track.Id;
            spanne = track.Points.Max(p => p.Timestamp) - track.Points.Min(p => p.Timestamp);
            foreach (var punkt in track.Points) punkt.Timestamp = punkt.Timestamp.AddDays(-5);
            (await db.TrainingSessions.SingleAsync(s => s.Id == track.TrainingSessionId)).Date = Vereinszeit.Heute().AddDays(-6);
            await db.SaveChangesAsync();
        }

        Assert.Empty(await SzenarienAsync(provider));

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var track = await db.GpsTracks.Include(t => t.Points).SingleAsync(t => t.Id == trackId);
            Assert.Equal(GesternUm(22), track.Points.Min(p => p.Timestamp));
            Assert.Equal(spanne, track.Points.Max(p => p.Timestamp) - track.Points.Min(p => p.Timestamp));
            Assert.Equal(Vereinszeit.Heute().AddDays(-1), (await db.TrainingSessions.SingleAsync(s => s.Id == track.TrainingSessionId)).Date);
            Assert.Single(await db.GpsTracks.Where(t => t.Comment == DemoSzenarienSeeder.KommentarNachtfaehrte).ToListAsync());
        }
    }

    [Fact]
    public async Task Nachtfaehrte_MitAblaufWirdNichtVerschoben()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        var alt = Vereinszeit.Heute().AddDays(-6);
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var track = await db.GpsTracks.Include(t => t.Points).SingleAsync(t => t.Comment == DemoSzenarienSeeder.KommentarNachtfaehrte);
            foreach (var punkt in track.Points) punkt.Timestamp = punkt.Timestamp.AddDays(-5);
            (await db.TrainingSessions.SingleAsync(s => s.Id == track.TrainingSessionId)).Date = alt;
            // Ein Tester hat die Fährte abgelaufen.
            db.GpsWalkRuns.Add(new GpsWalkRun { TrackId = track.Id });
            await db.SaveChangesAsync();
        }

        var davor = await ZaehleAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));
        Assert.Equal(davor, await ZaehleAsync(provider));

        using var pruefen = provider.CreateScope();
        var pruefDb = pruefen.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var track2 = await pruefDb.GpsTracks.Include(t => t.Points).SingleAsync(t => t.Comment == DemoSzenarienSeeder.KommentarNachtfaehrte);
        Assert.Equal(GesternUm(22).AddDays(-5), track2.Points.Min(p => p.Timestamp));
        Assert.Equal(alt, (await pruefDb.TrainingSessions.SingleAsync(s => s.Id == track2.TrainingSessionId)).Date);
    }
    [Fact]
    public async Task Nachtfaehrte_HatBelloGesternSchonEinTraining_EntstehtSieNicht()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        // Die Nachtfährte samt Einheit wird entfernt (wie vor dem ersten Start); dafür trägt ein
        // Tester gestern ein eigenes Training für Bello ein.
        Guid belloId;
        Guid testerEinheitId;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var track = await db.GpsTracks.Include(t => t.Points).SingleAsync(t => t.Comment == DemoSzenarienSeeder.KommentarNachtfaehrte);
            var einheit = await db.TrainingSessions.SingleAsync(s => s.Id == track.TrainingSessionId);
            belloId = einheit.DogId;
            db.GpsPoints.RemoveRange(track.Points);
            db.GpsTracks.Remove(track);
            db.TrainingSessions.Remove(einheit);
            var tester = new TrainingSession { UserId = einheit.UserId, DogId = belloId, Date = Vereinszeit.Heute().AddDays(-1), DurationMinutes = 20, Notes = "Von Hand eingetragen." };
            testerEinheitId = tester.Id;
            db.TrainingSessions.Add(tester);
            await db.SaveChangesAsync();
        }

        Assert.Empty(await SzenarienAsync(provider));

        using var pruefen = provider.CreateScope();
        var pruefDb = pruefen.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await pruefDb.GpsTracks.IgnoreQueryFilters().Where(t => t.Comment == DemoSzenarienSeeder.KommentarNachtfaehrte).ToListAsync());
        var einheitNachher = await pruefDb.TrainingSessions.SingleAsync(s => s.Id == testerEinheitId);
        Assert.Equal(20, einheitNachher.DurationMinutes);
        Assert.Equal("Von Hand eingetragen.", einheitNachher.Notes);
    }

    [Fact]
    public async Task Nachtfaehrte_EinheitMitEintragEinesTestersWirdNichtVerschoben()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        var alt = Vereinszeit.Heute().AddDays(-6);
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var track = await db.GpsTracks.Include(t => t.Points).SingleAsync(t => t.Comment == DemoSzenarienSeeder.KommentarNachtfaehrte);
            foreach (var punkt in track.Points) punkt.Timestamp = punkt.Timestamp.AddDays(-5);
            var einheit = await db.TrainingSessions.SingleAsync(s => s.Id == track.TrainingSessionId);
            einheit.Date = alt;
            // Der Tester hat im Eintrag etwas notiert - er gehört nicht mehr allein der Demo.
            einheit.Notes = "Eigene Notiz.";
            await db.SaveChangesAsync();
        }

        Assert.Empty(await SzenarienAsync(provider));

        using var pruefen = provider.CreateScope();
        var pruefDb = pruefen.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var track2 = await pruefDb.GpsTracks.Include(t => t.Points).SingleAsync(t => t.Comment == DemoSzenarienSeeder.KommentarNachtfaehrte);
        Assert.Equal(GesternUm(22).AddDays(-5), track2.Points.Min(p => p.Timestamp));
        Assert.Equal(alt, (await pruefDb.TrainingSessions.SingleAsync(s => s.Id == track2.TrainingSessionId)).Date);
    }

    [Fact]
    public async Task Nachtfaehrte_LiegtAmZieltagSchonEineAndereEinheit_WirdNichtVerschoben()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        var alt = Vereinszeit.Heute().AddDays(-6);
        Guid andereId;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var track = await db.GpsTracks.Include(t => t.Points).SingleAsync(t => t.Comment == DemoSzenarienSeeder.KommentarNachtfaehrte);
            foreach (var punkt in track.Points) punkt.Timestamp = punkt.Timestamp.AddDays(-5);
            var einheit = await db.TrainingSessions.SingleAsync(s => s.Id == track.TrainingSessionId);
            einheit.Date = alt;
            // Ein Tester hat für gestern ein Training nachgetragen.
            var andere = new TrainingSession { UserId = einheit.UserId, DogId = einheit.DogId, Date = Vereinszeit.Heute().AddDays(-1), DurationMinutes = 15 };
            andereId = andere.Id;
            db.TrainingSessions.Add(andere);
            await db.SaveChangesAsync();
        }

        Assert.Empty(await SzenarienAsync(provider));

        using var pruefen = provider.CreateScope();
        var pruefDb = pruefen.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var track2 = await pruefDb.GpsTracks.Include(t => t.Points).SingleAsync(t => t.Comment == DemoSzenarienSeeder.KommentarNachtfaehrte);
        Assert.Equal(alt, (await pruefDb.TrainingSessions.SingleAsync(s => s.Id == track2.TrainingSessionId)).Date);
        Assert.Equal(Vereinszeit.Heute().AddDays(-1), (await pruefDb.TrainingSessions.SingleAsync(s => s.Id == andereId)).Date);
    }

    [Fact]
    public async Task HatTomSchonEinenAnderenOffenenAntrag_BleibtDerDemoAntragAusUndEsGibtKeineWarnung()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        var warnungen = new WarnungsSammler();
        provider.GetRequiredService<ILoggerFactory>().AddProvider(warnungen);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tomId = await IdAsync(scope.ServiceProvider, DemoSzenarienSeeder.InteressentEmail);
            db.ClubRegistrations.Add(new ClubRegistration { Name = "Anderer Verein", RequestedByUserId = tomId });
            await db.SaveChangesAsync();
        }

        Assert.Empty(await SzenarienAsync(provider));

        using var pruefen = provider.CreateScope();
        var pruefDb = pruefen.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.DoesNotContain(await pruefDb.ClubRegistrations.ToListAsync(), r => r.Name == DemoSzenarienSeeder.VereinsAntragName);
        Assert.Empty(warnungen.Zeilen);
    }

    // --- Prüfungsreife ----------------------------------------------------------------

    private sealed record ReifeAnsicht(Guid BelloId, Guid BelloZiel, Guid EmmaZiel, Guid SandroZiel, Guid MaxId, Guid SandroId, Guid TrainerId);

    private static async Task<ReifeAnsicht> AnsichtAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var bello = await db.Dogs.SingleAsync(d => d.Name == "Bello");
        return new ReifeAnsicht(
            bello.Id,
            (await db.Goals.SingleAsync(g => g.Notes == DemoDataSeeder.NotizBelloBh)).Id,
            (await db.Goals.SingleAsync(g => g.Notes == DemoSzenarienSeeder.NotizUprZiel)).Id,
            (await db.Goals.SingleAsync(g => g.Notes == DemoSzenarienSeeder.NotizSkgBhZiel)).Id,
            (await users.FindByEmailAsync("mitglied1@dogity.test"))!.Id,
            (await users.FindByEmailAsync(DemoSzenarienSeeder.SchweizEmail))!.Id,
            (await users.FindByEmailAsync("trainer@dogity.test"))!.Id);
    }

    private static async Task<PruefungsreifeDto> ReifeAsync(IServiceProvider provider, Guid nutzer, Guid ziel)
    {
        using var scope = provider.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<IGoalService>().GetPruefungsreifeAsync(nutzer, ziel)).Value!;
    }

    [Fact]
    public async Task Pruefungsreife_AlleDreiAmpelfarbenSindZuSehen()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        await MitSachkundeAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));
        var a = await AnsichtAsync(provider);

        var bello = await ReifeAsync(provider, a.MaxId, a.BelloZiel);
        var emma = await ReifeAsync(provider, a.MaxId, a.EmmaZiel);
        var sandro = await ReifeAsync(provider, a.SandroId, a.SandroZiel);

        Assert.Equal(Reifeampel.Gelb, bello.Ampel);
        Assert.Equal(Reifeampel.Rot, emma.Ampel);
        Assert.Equal(Reifeampel.Gruen, sandro.Ampel);
        // Gelb heißt: nichts offen, ein Teil sicher, ein Teil wackelig.
        Assert.Equal(0, bello.Offen);
        Assert.True(bello.Sicher > 0 && bello.Wackelig > 0);
        Assert.Equal(0, sandro.Wackelig + sandro.Offen);
    }

    [Fact]
    public async Task Pruefungsreife_BellosBhZielHatDieFciBhUndMaxSiehtDieTheoriezeile_DieTrainerinNicht()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        await MitSachkundeAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));
        var a = await AnsichtAsync(provider);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ordnung = await db.Regulations.SingleAsync(r => db.Goals.Any(g => g.Id == a.BelloZiel && g.RegulationId == r.Id));
            Assert.Equal("BH", ordnung.Name);
            Assert.Null(ordnung.CountryCode);
        }

        // Max: eine nicht bestandene, danach eine bestandene Übungsprüfung - wackelig.
        var max = await ReifeAsync(provider, a.MaxId, a.BelloZiel);
        Assert.NotNull(max.Theorie);
        Assert.Equal(ReifeZustand.Wackelig, max.Theorie!.Zustand);
        Assert.Equal((2, 1), (max.Theorie.Gewertet, max.Theorie.Bestanden));

        // Die betreuende Trainerin sieht die Übungen, aber keine Theorie.
        var trainerin = await ReifeAsync(provider, a.TrainerId, a.BelloZiel);
        Assert.Null(trainerin.Theorie);
        Assert.Equal(max.Uebungen.Count, trainerin.Uebungen.Count);
    }

    /// <summary>Stellt Bellos Ziel auf eine andere Ordnung derselben Sportart (wie auf einer älteren Testumgebung) und gibt die FCI-BH zurück.</summary>
    private static async Task<Guid> BelloZielAufAndereOrdnungAsync(IServiceProvider provider, Guid zielId, bool mitPlan)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ziel = await db.Goals.SingleAsync(g => g.Id == zielId);
        var fciBh = ziel.RegulationId!.Value;
        ziel.RegulationId = await db.Regulations.Where(r => r.SportId == ziel.SportId && r.Id != fciBh).Select(r => r.Id).FirstAsync();

        db.TrainingPlans.RemoveRange(await db.TrainingPlans.Where(p => p.GoalId == zielId).ToListAsync());
        if (mitPlan) db.TrainingPlans.Add(new TrainingPlan { GoalId = zielId });
        await db.SaveChangesAsync();
        return fciBh;
    }

    [Fact]
    public async Task Pruefungsreife_BellosZielAufAndererOrdnungMitPlan_BleibtUnangetastet()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));
        var a = await AnsichtAsync(provider);
        await BelloZielAufAndereOrdnungAsync(provider, a.BelloZiel, mitPlan: true);

        Assert.Empty(await SzenarienAsync(provider));

        using var pruef = provider.CreateScope();
        var pruefDb = pruef.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ziel = await pruefDb.Goals.SingleAsync(g => g.Id == a.BelloZiel);
        // Die Planeinträge hängen an den Übungen dieser Ordnung: Sie darf nicht unter dem Plan getauscht werden.
        Assert.NotEqual("BH", (await pruefDb.Regulations.SingleAsync(r => r.Id == ziel.RegulationId)).Name);
    }

    [Fact]
    public async Task Pruefungsreife_BellosZielAufAndererOrdnungOhnePlan_BekommtDieFciBh()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));
        var a = await AnsichtAsync(provider);
        var fciBh = await BelloZielAufAndereOrdnungAsync(provider, a.BelloZiel, mitPlan: false);

        Assert.Empty(await SzenarienAsync(provider));

        using var pruef = provider.CreateScope();
        var pruefDb = pruef.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(fciBh, (await pruefDb.Goals.SingleAsync(g => g.Id == a.BelloZiel)).RegulationId);
    }

    [Fact]
    public async Task Pruefungsreife_ZweimaligesAusfuehren_LegtNichtsNeuAnUndAendertNichts()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        async Task<List<(Guid Hund, Guid Uebung, int Box, int Sitzungen, DateTimeOffset? Zuletzt, DateTimeOffset? Aktualisiert)>> StaendeAsync()
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return (await db.ExerciseMasteries.OrderBy(m => m.DogId).ThenBy(m => m.ExerciseId).ToListAsync())
                .Select(m => (m.DogId, m.ExerciseId, m.Box, m.SessionCount, m.LastTrainedAt, m.UpdatedAt)).ToList();
        }
        var vorher = await StaendeAsync();

        Assert.Empty(await SzenarienAsync(provider));

        Assert.NotEmpty(vorher);
        Assert.Equal(vorher, await StaendeAsync());
    }

    [Fact]
    public async Task Pruefungsreife_EigenesTrainingEinerUebung_BleibtUnangetastet()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));
        var a = await AnsichtAsync(provider);

        Guid uebungId;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Der Tester hat die Übung selbst trainiert: Der Stand trägt nicht mehr die Merkmale des Seeders.
            var stand = await db.ExerciseMasteries.FirstAsync(m => m.DogId == a.BelloId && m.SessionCount == 3);
            uebungId = stand.ExerciseId;
            stand.SessionCount = 9;
            stand.Box = 3;
            stand.RecentAvgRating = 3.7;
            stand.LastTrainedAt = DateTimeOffset.UtcNow.AddDays(-40);
            await db.SaveChangesAsync();
        }

        Assert.Empty(await SzenarienAsync(provider));

        using var pruef = provider.CreateScope();
        var pruefDb = pruef.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var nachher = await pruefDb.ExerciseMasteries.SingleAsync(m => m.DogId == a.BelloId && m.ExerciseId == uebungId);
        Assert.Equal((9, 3, 3.7), (nachher.SessionCount, nachher.Box, nachher.RecentAvgRating));
        Assert.True(nachher.LastTrainedAt < DateTimeOffset.UtcNow.AddDays(-39));
    }

    [Fact]
    public async Task Pruefungsreife_ZeileOhneSitzung_ZaehltNichtAlsEigenesTraining_UndBekommtDenStand()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));
        var a = await AnsichtAsync(provider);

        Guid uebungId;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Nur eine Gewichtung von Hand, nie trainiert: Die Zeile hat keine Sitzung.
            var stand = await db.ExerciseMasteries.FirstAsync(m => m.DogId == a.BelloId && m.SessionCount == 3);
            uebungId = stand.ExerciseId;
            stand.SessionCount = 0;
            stand.Box = 1;
            stand.RecentAvgRating = 0;
            stand.LastTrainedAt = null;
            stand.ManualPriority = 2;
            await db.SaveChangesAsync();
        }

        Assert.Empty(await SzenarienAsync(provider));

        using var pruef = provider.CreateScope();
        var pruefDb = pruef.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var nachher = await pruefDb.ExerciseMasteries.SingleAsync(m => m.DogId == a.BelloId && m.ExerciseId == uebungId);
        Assert.True(nachher.SessionCount > 0);
        Assert.Equal(2, nachher.ManualPriority);
    }

    [Fact]
    public async Task Pruefungsreife_Rollend_AlterSeederStandRueckenNachVorn_SandroBleibtGruen()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));
        var a = await AnsichtAsync(provider);

        // Sechs Wochen später: Alles ist über die 28 Tage hinaus und nicht mehr "sicher".
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var m in await db.ExerciseMasteries.Where(m => m.SessionCount == 3 || m.SessionCount == 2).ToListAsync())
                m.LastTrainedAt = DateTimeOffset.UtcNow.AddDays(-45);
            await db.SaveChangesAsync();
        }
        Assert.NotEqual(Reifeampel.Gruen, (await ReifeAsync(provider, a.SandroId, a.SandroZiel)).Ampel);

        Assert.Empty(await SzenarienAsync(provider));

        Assert.Equal(Reifeampel.Gruen, (await ReifeAsync(provider, a.SandroId, a.SandroZiel)).Ampel);
        Assert.Equal(Reifeampel.Gelb, (await ReifeAsync(provider, a.MaxId, a.BelloZiel)).Ampel);
    }

    [Fact]
    public async Task Pruefungsreife_GeloeschtesZiel_WirdNichtWiederbelebt_BelloBekommtEinNeuesBhZiel()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        await MitSachkundeAsync(provider);
        Guid altesZiel;
        using (var scope = provider.CreateScope())
        {
            // Vor dem Szenario: Der Tester hat Bellos BH-Ziel gelöscht (auf der Test-Umgebung ist das geschehen).
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ziel = await db.Goals.SingleAsync(g => g.Notes == DemoDataSeeder.NotizBelloBh);
            altesZiel = ziel.Id;
            ziel.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        Assert.Empty(await SzenarienAsync(provider));

        using var pruef = provider.CreateScope();
        var pruefDb = pruef.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bello = await pruefDb.Dogs.SingleAsync(d => d.Name == "Bello");
        // Das gelöschte bleibt gelöscht; daneben steht ein neues, aktives BH-Ziel nach der FCI-BH mit Plan.
        Assert.NotNull((await pruefDb.Goals.IgnoreQueryFilters().SingleAsync(g => g.Id == altesZiel)).DeletedAt);
        var neu = await pruefDb.Goals.Include(g => g.TrainingPlan).SingleAsync(g => g.DogId == bello.Id && g.Notes == DemoDataSeeder.NotizBelloBh);
        Assert.NotEqual(altesZiel, neu.Id);
        Assert.Equal(GoalStatus.Active, neu.Status);
        var ordnung = await pruefDb.Regulations.SingleAsync(r => r.Id == neu.RegulationId);
        Assert.Equal("BH", ordnung.Name);
        Assert.Null(ordnung.CountryCode);
        var heute = Vereinszeit.Heute();
        Assert.InRange(neu.TargetDate.DayNumber - heute.DayNumber, 63, 77);
        Assert.NotNull(neu.TrainingPlan);
        Assert.NotEmpty(await pruefDb.TrainingPlanItems.Where(i => i.TrainingPlanId == neu.TrainingPlan!.Id).ToListAsync());

        // Und die gelbe Szene samt Theoriezeile ist wieder da.
        var maxId = (await pruef.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("mitglied1@dogity.test"))!.Id;
        var reife = await ReifeAsync(provider, maxId, neu.Id);
        Assert.Equal(Reifeampel.Gelb, reife.Ampel);
        Assert.NotNull(reife.Theorie);
    }

    [Fact]
    public async Task Pruefungsreife_AbgebrochenesZiel_BelloBekommtEinNeues_UndEinSpaeteresAusfuehrenLegtKeinDrittesAn()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Goals.SingleAsync(g => g.Notes == DemoDataSeeder.NotizBelloBh)).Status = GoalStatus.Cancelled;
            await db.SaveChangesAsync();
        }

        Assert.Empty(await SzenarienAsync(provider));
        Assert.Empty(await SzenarienAsync(provider));

        using var pruef = provider.CreateScope();
        var pruefDb = pruef.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ziele = await pruefDb.Goals.Where(g => g.Notes == DemoDataSeeder.NotizBelloBh).ToListAsync();
        Assert.Equal(2, ziele.Count);
        Assert.Equal(1, ziele.Count(g => g.Status == GoalStatus.Active));
    }

    [Fact]
    public async Task Pruefungsreife_BelloMitEigenemAktivenZiel_BekommtKeinZweitesDemoZiel()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Guid belloId;
        using (var scope = provider.CreateScope())
        {
            // Ein Tester hat Bellos Demo-Ziel durch ein eigenes ersetzt (andere Sportart, eigene Notiz).
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var demo = await db.Goals.SingleAsync(g => g.Notes == DemoDataSeeder.NotizBelloBh);
            belloId = demo.DogId;
            demo.Status = GoalStatus.Cancelled;
            db.Goals.Add(new Goal
            {
                DogId = belloId, SportId = demo.SportId, TargetDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
                Status = GoalStatus.Active, Notes = "Mein eigenes Ziel",
            });
            await db.SaveChangesAsync();
        }

        Assert.Empty(await SzenarienAsync(provider));

        using var pruef = provider.CreateScope();
        var pruefDb = pruef.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ziele = await pruefDb.Goals.Where(g => g.DogId == belloId).ToListAsync();
        Assert.Equal(2, ziele.Count);
        Assert.Equal("Mein eigenes Ziel", Assert.Single(ziele, g => g.Status == GoalStatus.Active).Notes);
    }

    // --- Gesundheit ---------------------------------------------------------------------

    private static async Task<(Dog Bello, Dog Emma)> HundeFuerGesundheitAsync(ApplicationDbContext db) =>
        (await db.Dogs.SingleAsync(d => d.Name == "Bello"), await db.Dogs.SingleAsync(d => d.Name == "Emma"));

    [Fact]
    public async Task Gesundheit_LegtDieEintraegeFuerBelloUndEmmaAn()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var heute = Vereinszeit.Heute();
        var (bello, emma) = await HundeFuerGesundheitAsync(db);

        var belloEintraege = await db.DogHealthRecords.Where(r => r.DogId == bello.Id).ToListAsync();
        Assert.Equal(4, belloEintraege.Count);
        var tollwut = belloEintraege.Single(r => r.Title == "Tollwut");
        Assert.Equal(DogHealthKind.Vaccination, tollwut.Kind);
        Assert.Equal(heute.AddDays(10), tollwut.DueDate);
        Assert.Equal(DemoSzenarienSeeder.NotizGesundheitTollwut, tollwut.Note);
        Assert.InRange(belloEintraege.Single(r => r.Title == "SHPPi").DueDate!.Value.DayNumber - heute.DayNumber, 700, 735);
        var entwurmung = belloEintraege.Single(r => r.Kind == DogHealthKind.Deworming);
        Assert.Equal(heute.AddDays(-2), entwurmung.DueDate);
        Assert.InRange(heute.DayNumber - entwurmung.Date.DayNumber, 85, 95);
        var termin = belloEintraege.Single(r => r.Kind == DogHealthKind.VetAppointment);
        Assert.Equal((heute.AddDays(2), new TimeOnly(10, 30), (DateOnly?)null), (termin.Date, termin.Time, termin.DueDate));

        // Emma: nur eine Tollwut-Impfung, die in Ordnung ist.
        var emmaEintrag = await db.DogHealthRecords.SingleAsync(r => r.DogId == emma.Id);
        Assert.Equal("Tollwut", emmaEintrag.Title);

        // Was die Oberfläche daraus macht: Bello überfällig (Entwurmung) und bald fällig (Tollwut), Emma nichts.
        var staende = await db.HealthSummariesAsync([bello.Id, emma.Id], heute);
        Assert.Equal((1, 1), (staende[bello.Id].Overdue, staende[bello.Id].DueSoon));
        Assert.Equal(heute.AddDays(2), staende[bello.Id].NextAppointment!.Date);
        Assert.Equal((0, 0), (staende[emma.Id].Overdue, staende[emma.Id].DueSoon));
    }

    [Fact]
    public async Task Gesundheit_DieDemoEintraegeSindNurFuerDieBesitzerSichtbar()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var maxId = (await users.FindByEmailAsync("mitglied1@dogity.test"))!.Id;
        var trainerId = (await users.FindByEmailAsync("trainer@dogity.test"))!.Id;
        var bello = await db.Dogs.SingleAsync(d => d.Name == "Bello");
        var gesundheit = scope.ServiceProvider.GetRequiredService<IDogHealthService>();

        Assert.Equal(4, (await gesundheit.GetAsync(maxId, bello.Id)).Value!.Count);
        // Auch die Demo-Trainerin, die Bello betreut, sieht nichts davon.
        Assert.True((await gesundheit.GetAsync(trainerId, bello.Id)).IsNotFound);
    }

    [Fact]
    public async Task Gesundheit_Rollend_BaldAbgelaufeneTollwutUndVergangenerTerminRuckenNachVorn()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));
        var heute = Vereinszeit.Heute();
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.DogHealthRecords.SingleAsync(r => r.Note == DemoSzenarienSeeder.NotizGesundheitTollwut && r.Title == "Tollwut")).DueDate = heute.AddDays(1);
            (await db.DogHealthRecords.SingleAsync(r => r.Note == DemoSzenarienSeeder.NotizGesundheitTermin)).Date = heute.AddDays(-4);
            await db.SaveChangesAsync();
        }

        Assert.Empty(await SzenarienAsync(provider));

        using var pruef = provider.CreateScope();
        var pruefDb = pruef.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(heute.AddDays(10), (await pruefDb.DogHealthRecords.SingleAsync(r => r.Note == DemoSzenarienSeeder.NotizGesundheitTollwut)).DueDate);
        Assert.Equal(heute.AddDays(2), (await pruefDb.DogHealthRecords.SingleAsync(r => r.Note == DemoSzenarienSeeder.NotizGesundheitTermin)).Date);
        // Nichts Neues: dieselbe Anzahl wie vorher.
        Assert.Equal(5, await pruefDb.DogHealthRecords.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Gesundheit_RuhigeEintraegeBleibenRuhig_DerSeederSchreibtNichtBeiJedemStart()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));

        async Task<List<(Guid, DateOnly, DateOnly?, DateTimeOffset?)>> StandAsync()
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return (await db.DogHealthRecords.IgnoreQueryFilters().OrderBy(r => r.Note).ThenBy(r => r.Title).ToListAsync())
                .Select(r => (r.Id, r.Date, r.DueDate, r.UpdatedAt)).ToList();
        }

        var davor = await StandAsync();
        Assert.Empty(await SzenarienAsync(provider));
        Assert.Equal(davor, await StandAsync());
    }

    [Fact]
    public async Task Gesundheit_VonTesternGeaendertesBleibt_GeloeschtesKommtNichtZurueck()
    {
        await using var provider = BaueDienste();
        await MitDemoKontenAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));
        var heute = Vereinszeit.Heute();
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Ein Tester ändert die Tollwut-Impfung (läuft morgen ab) und den Termin (vorbei) - und löscht die Entwurmung.
            var tollwut = await db.DogHealthRecords.SingleAsync(r => r.Note == DemoSzenarienSeeder.NotizGesundheitTollwut);
            tollwut.DueDate = heute.AddDays(1);
            tollwut.UpdatedAt = DateTimeOffset.UtcNow;
            var termin = await db.DogHealthRecords.SingleAsync(r => r.Note == DemoSzenarienSeeder.NotizGesundheitTermin);
            termin.Date = heute.AddDays(-4);
            termin.UpdatedAt = DateTimeOffset.UtcNow;
            (await db.DogHealthRecords.SingleAsync(r => r.Note == DemoSzenarienSeeder.NotizGesundheitEntwurmung)).DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        var davor = await ZaehleAsync(provider);
        Assert.Empty(await SzenarienAsync(provider));
        Assert.Equal(davor, await ZaehleAsync(provider));

        using var pruef = provider.CreateScope();
        var pruefDb = pruef.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(heute.AddDays(1), (await pruefDb.DogHealthRecords.SingleAsync(r => r.Note == DemoSzenarienSeeder.NotizGesundheitTollwut)).DueDate);
        Assert.Equal(heute.AddDays(-4), (await pruefDb.DogHealthRecords.SingleAsync(r => r.Note == DemoSzenarienSeeder.NotizGesundheitTermin)).Date);
        Assert.DoesNotContain(await pruefDb.DogHealthRecords.ToListAsync(), r => r.Note == DemoSzenarienSeeder.NotizGesundheitEntwurmung);
    }

    /// <summary>Hält Warnungen des Seeders fest - ein still übergangenes Szenario darf keine auslösen.</summary>
    private sealed class WarnungsSammler : ILoggerProvider
    {
        public List<string> Zeilen { get; } = [];
        public ILogger CreateLogger(string categoryName) => new Sammler(Zeilen);
        public void Dispose() { }

        private sealed class Sammler(List<string> zeilen) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= LogLevel.Warning) zeilen.Add(formatter(state, exception));
            }
        }
    }
}
