using Dogity.Api.Contracts;
using Dogity.Api.Controllers;
using Dogity.Api.Hosting;
using Dogity.Application.Abstractions;
using Dogity.Application.Account;
using Dogity.Application.Admin;
using Dogity.Application.Community;
using Dogity.Application.Dogs;
using Dogity.Application.Notifications;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Weather;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Email;
using Dogity.Infrastructure.Identity;
using Dogity.Infrastructure.Persistence;
using Dogity.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dogity.Application.Tests.Gastzugang;

/// <summary>
/// Der Gastzugang ("App ausprobieren", docs/GASTZUGANG.md): Anlegen, Abschottung von echten Konten und
/// Vereinen, keine Zählung in Admin-Auswertungen, keine Mails, restloses Löschen nach Ablauf.
/// </summary>
public class GastzugangTests
{
    private sealed class Uhr(DateTimeOffset jetzt) : TimeProvider
    {
        public DateTimeOffset Jetzt { get; set; } = jetzt;
        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    private sealed class TestJwt : IJwtTokenGenerator
    {
        public string GenerateToken(Guid userId, string email, IEnumerable<string> roles) => $"jwt-{userId}";
    }

    private static async Task<(ServiceProvider Provider, Uhr Uhr)> BaueAsync()
    {
        var uhr = new Uhr(DateTimeOffset.UtcNow);
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
        services.AddScoped<IRefreshTokenService, FakeRefreshTokenService>();
        services.AddApplication();
        // Nach AddApplication, damit die Uhr des Tests gilt.
        services.AddSingleton<TimeProvider>(uhr);
        services.AddScoped<IWeatherEnrichmentService, FakeWeatherEnrichmentService>();
        services.AddSingleton<INotificationChannels, FakeNotificationChannels>();
        services.AddSingleton<IFrontendLinks>(TestLinks.Mit());
        services.AddSingleton<IEmailAbmeldeToken>(TestAbmeldeToken.Neu());
        services.AddSingleton<IMailQueue, FakeMailQueue>();
        services.AddSingleton<IPushQueue, FakePushQueue>();
        services.AddScoped<GastkontoService>();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        await RoleSeeder.SeedAsync(scope.ServiceProvider);
        await SportCatalogSeeder.SeedAsync(scope.ServiceProvider);
        await CountrySeeder.SeedAsync(scope.ServiceProvider);
        return (provider, uhr);
    }

    /// <summary>Ein echtes Konto mit Hund, Training, Verein und Trainerrolle - alles, was die Auswertungen zählen.</summary>
    private static async Task<(ApplicationUser Nutzer, Club Verein)> EchtesKontoAsync(IServiceScope scope, string email)
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var nutzer = new ApplicationUser { UserName = email, Email = email, FirstName = "Echt", LastName = "Person" };
        Assert.True((await users.CreateAsync(nutzer, "Passwort123")).Succeeded);
        await users.AddToRolesAsync(nutzer, [Roles.User, Roles.Trainer]);

        var hund = new Dog { Name = "Bello", Gender = DogGender.Male };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = nutzer.Id });
        db.TrainingSessions.Add(new TrainingSession { UserId = nutzer.Id, DogId = hund.Id, Date = Vereinszeit.Heute(), DurationMinutes = 30 });
        var verein = new Club { Name = "Echter Verein e.V." };
        db.Clubs.Add(verein);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = verein.Id, UserId = nutzer.Id, Role = ClubRole.Training | ClubRole.Verwaltung });
        db.Groups.Add(new Group { ClubId = verein.Id, TrainerId = nutzer.Id, Name = "Echte Gruppe" });
        await db.SaveChangesAsync();
        return (nutzer, verein);
    }

    private static async Task<ApplicationUser> GastAsync(IServiceProvider provider) =>
        (await provider.GetRequiredService<GastkontoService>().AnlegenAsync("de"))!;

    [Fact]
    public async Task Anlegen_LegtKontoHundVerlaufVereinUndTrainerrolleAn()
    {
        var (provider, uhr) = await BaueAsync();
        using var scope = provider.CreateScope();
        var gast = await GastAsync(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.EndsWith("@gast.invalid", gast.Email);
        Assert.Equal(uhr.Jetzt + Gastkonto.Laufzeit, gast.GuestUntil);
        Assert.Contains(Roles.Trainer, await users.GetRolesAsync(gast));
        Assert.NotNull(gast.OnboardingDismissedAt);
        Assert.Null(gast.PasswordHash);

        var hund = await db.Dogs.SingleAsync(d => d.Name == "Ayla");
        Assert.True(await db.DogOwners.AnyAsync(o => o.DogId == hund.Id && o.UserId == gast.Id));
        var einheiten = await db.TrainingSessions.Where(s => s.UserId == gast.Id).ToListAsync();
        Assert.True(einheiten.Count >= 8);
        Assert.True(await db.TrainingExercises.AnyAsync());
        Assert.Single(await db.Goals.ToListAsync());
        Assert.Single(await db.GpsWalkRuns.ToListAsync());

        var verein = await db.Clubs.SingleAsync();
        Assert.Equal(gast.GuestUntil, verein.GuestUntil);
        var rolle = await db.ClubTrainers.SingleAsync(t => t.UserId == gast.Id);
        Assert.Equal(ClubRole.Training | ClubRole.Verwaltung, rolle.Role);
        Assert.Equal(5, await db.ClubMemberships.CountAsync());
        Assert.Single(await db.ClubMemberships.Where(m => m.Status == ClubMembershipStatus.Pending).ToListAsync());
        var gruppe = await db.Groups.SingleAsync();
        Assert.Equal(3, await db.GroupMembers.CountAsync(m => m.GroupId == gruppe.Id));
        Assert.True(await db.GroupTrainingSessions.AnyAsync(s => s.GroupId == gruppe.Id && s.StartsAt > uhr.Jetzt));
        // Gast und seine fünf erfundenen Mitglieder - alle ohne Passwort, also nicht anmeldbar.
        var gaeste = await users.Users.Where(u => u.GuestUntil != null).ToListAsync();
        Assert.Equal(6, gaeste.Count);
        Assert.All(gaeste, u => Assert.Null(u.PasswordHash));
    }

    [Fact]
    public async Task Endpunkt_LiefertTokens_UndBeiVielenGaestenEine503()
    {
        var (provider, _) = await BaueAsync();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var controller = new AuthController(
            sp.GetRequiredService<UserManager<ApplicationUser>>(), null!, new TestJwt(), new FakeRefreshTokenService(), null!,
            sp.GetRequiredService<Application.Preferences.IPreferenceService>(), sp.GetRequiredService<GastkontoService>(),
            sp.GetRequiredService<IFrontendLinks>(), new AnmeldeDrossel(), TimeProvider.System, NullLogger<AuthController>.Instance);

        var antwort = Assert.IsType<OkObjectResult>((await controller.Guest(new GuestRequest("de"), default)).Result);
        var auth = Assert.IsType<AuthResponse>(antwort.Value);
        Assert.False(string.IsNullOrEmpty(auth.Token));
        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));
        Assert.Contains(Roles.Trainer, auth.Roles);

        // Obergrenze: Sind schon so viele Gast-Vereine aktiv, wird kein weiterer Gast angelegt.
        var db = sp.GetRequiredService<ApplicationDbContext>();
        db.Clubs.AddRange(Enumerable.Range(0, Gastkonto.MaxAktiv).Select(i => new Club { Name = $"Gast {i}", GuestUntil = DateTimeOffset.UtcNow.AddHours(1) }));
        await db.SaveChangesAsync();
        var voll = Assert.IsType<ObjectResult>((await controller.Guest(new GuestRequest("de"), default)).Result);
        Assert.Equal(503, voll.StatusCode);
        Assert.Null(await sp.GetRequiredService<GastkontoService>().AnlegenAsync("de"));
    }

    [Fact]
    public async Task Vereinsliste_EchteSehenKeinenGastVerein_GaesteNurIhren()
    {
        var (provider, _) = await BaueAsync();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var (echt, echterVerein) = await EchtesKontoAsync(sp.CreateScope(), "echt@dogity.test");
        var gast = await GastAsync(sp);
        var anderer = await GastAsync(sp);
        var clubs = sp.GetRequiredService<IClubService>();
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var gastVerein = await db.Clubs.SingleAsync(c => c.Id == db.ClubTrainers.First(t => t.UserId == gast.Id).ClubId);

        var echteSicht = (await clubs.GetBrowsableClubsAsync(echt.Id)).Value!;
        Assert.Equal([echterVerein.Id], echteSicht.Select(c => c.Id));

        var gastSicht = (await clubs.GetBrowsableClubsAsync(gast.Id)).Value!;
        Assert.Equal([gastVerein.Id], gastSicht.Select(c => c.Id));
        Assert.DoesNotContain(gastSicht, c => c.Id == echterVerein.Id);

        // Die Admin-Liste kennt nur echte Vereine; eine Beitrittsanfrage über die Grenze ist "nicht gefunden".
        Assert.Equal([echterVerein.Id], (await clubs.GetClubsAsync()).Value!.Select(c => c.Id));
        Assert.True((await clubs.RequestJoinAsync(echt.Id, gastVerein.Id)).IsNotFound);
        Assert.True((await clubs.RequestJoinAsync(gast.Id, echterVerein.Id)).IsNotFound);
        Assert.NotNull(anderer);
    }

    [Fact]
    public async Task EmailSuche_ZwischenGastUndEchtemKonto_VerrätNichtObEsDieAdresseGibt()
    {
        var (provider, _) = await BaueAsync();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var (echt, echterVerein) = await EchtesKontoAsync(sp.CreateScope(), "echt@dogity.test");
        var gast = await GastAsync(sp);
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var gastVerein = await db.Clubs.SingleAsync(c => c.GuestUntil != null);
        var gastHund = await db.Dogs.SingleAsync(d => d.Name == "Ayla");
        var echterHund = await db.Dogs.SingleAsync(d => d.Name == "Bello");
        var gruppeGast = await db.Groups.SingleAsync(g => g.ClubId == gastVerein.Id);
        var gruppeEcht = await db.Groups.SingleAsync(g => g.ClubId == echterVerein.Id);
        var clubs = sp.GetRequiredService<IClubService>();
        var gruppen = sp.GetRequiredService<IGroupService>();
        var hunde = sp.GetRequiredService<IDogService>();
        const string Unbekannt = "gibt-es-nicht@dogity.test";

        // Gast -> echte Adresse: dieselbe Antwort wie bei einer unbekannten Adresse.
        var inVerein = await clubs.AddMemberAsync(gast.Id, false, gastVerein.Id, new AssignClubMemberRequest(echt.Email!));
        var inVereinUnbekannt = await clubs.AddMemberAsync(gast.Id, false, gastVerein.Id, new AssignClubMemberRequest(Unbekannt));
        Assert.False(inVerein.Succeeded);
        Assert.Equal(inVereinUnbekannt.Errors, inVerein.Errors);

        var trainer = await clubs.AssignTrainerAsync(gast.Id, false, gastVerein.Id, new AssignClubTrainerRequest(echt.Email!, ClubRole.Training));
        var trainerUnbekannt = await clubs.AssignTrainerAsync(gast.Id, false, gastVerein.Id, new AssignClubTrainerRequest(Unbekannt, ClubRole.Training));
        Assert.Equal(trainerUnbekannt.Errors, trainer.Errors);

        var inGruppe = await gruppen.AddMemberAsync(gast.Id, gruppeGast.Id, new AddMemberRequest(echt.Email!));
        var inGruppeUnbekannt = await gruppen.AddMemberAsync(gast.Id, gruppeGast.Id, new AddMemberRequest(Unbekannt));
        Assert.Equal(inGruppeUnbekannt.Errors, inGruppe.Errors);

        var gruppenTrainer = await gruppen.AddGroupTrainerAsync(gast.Id, gruppeGast.Id, new AddGroupTrainerRequest(echt.Email!));
        var gruppenTrainerUnbekannt = await gruppen.AddGroupTrainerAsync(gast.Id, gruppeGast.Id, new AddGroupTrainerRequest(Unbekannt));
        Assert.Equal(gruppenTrainerUnbekannt.Errors, gruppenTrainer.Errors);

        var mitbesitz = await hunde.AddOwnerAsync(gast.Id, gastHund.Id, new AddDogOwnerRequest(echt.Email!));
        var mitbesitzUnbekannt = await hunde.AddOwnerAsync(gast.Id, gastHund.Id, new AddDogOwnerRequest(Unbekannt));
        Assert.Equal(mitbesitzUnbekannt.Errors, mitbesitz.Errors);

        // Echtes Konto -> Adresse eines Gastkontos: ebenso.
        Assert.False((await clubs.AddMemberAsync(echt.Id, false, echterVerein.Id, new AssignClubMemberRequest(gast.Email!))).Succeeded);
        Assert.False((await gruppen.AddMemberAsync(echt.Id, gruppeEcht.Id, new AddMemberRequest(gast.Email!))).Succeeded);
        Assert.False((await hunde.AddOwnerAsync(echt.Id, echterHund.Id, new AddDogOwnerRequest(gast.Email!))).Succeeded);

        // Und nichts davon hat eine Verknüpfung hinterlassen.
        Assert.DoesNotContain(await db.ClubMemberships.ToListAsync(), m => m.UserId == echt.Id);
        Assert.DoesNotContain(await db.DogOwners.IgnoreQueryFilters().ToListAsync(), o => o.UserId == echt.Id && o.DogId == gastHund.Id);
        Assert.DoesNotContain(await db.ClubTrainers.ToListAsync(), t => t.UserId == echt.Id && t.ClubId == gastVerein.Id);
    }

    [Fact]
    public async Task Gast_DarfKeinenVereinsantragStellenUndKeinenEinladungslinkErzeugen()
    {
        var (provider, _) = await BaueAsync();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var gast = await GastAsync(sp);
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var verein = await db.Clubs.SingleAsync();
        var gruppe = await db.Groups.SingleAsync();

        var antrag = await sp.GetRequiredService<IClubRegistrationService>().RequestAsync(gast.Id, new CreateClubRegistrationRequest("Hundefreunde Wien", null));
        Assert.Equal([Gastkonto.NichtImDemoModus], antrag.Errors);
        var link = await sp.GetRequiredService<IClubInviteService>().RegenerateAsync(gast.Id, verein.Id);
        Assert.Equal([Gastkonto.NichtImDemoModus], link.Errors);
        var anmeldelink = await sp.GetRequiredService<IGroupRegistrationService>().RegenerateLinkAsync(gast.Id, gruppe.Id);
        Assert.Equal([Gastkonto.NichtImDemoModus], anmeldelink.Errors);
    }

    [Fact]
    public async Task AdminStatistik_ZaehltWederGaesteNochIhreDaten()
    {
        var (provider, _) = await BaueAsync();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        await EchtesKontoAsync(sp.CreateScope(), "echt@dogity.test");
        await GastAsync(sp);
        await GastAsync(sp);

        var stats = (await sp.GetRequiredService<IAdminService>().GetStatsAsync()).Value!;

        // Nur das echte Konto: ein Hund, eine Gruppe, ein Training, keine Fährte - und es ist das einzige aktive Konto.
        Assert.Equal(1, stats.UserCount);
        Assert.Equal(1, stats.DogCount);
        Assert.Equal(1, stats.GroupCount);
        Assert.Equal(1, stats.TrainingSessionCount);
        Assert.Equal(0, stats.GpsTrackCount);
        Assert.Equal(1, stats.Last30Days.NewAccounts);
        Assert.Equal(1, stats.Last30Days.WithDog);
        Assert.Equal(1, stats.Last30Days.InClub);
        Assert.Equal(0, stats.Last30Days.WithGoal);
        Assert.Equal(1, stats.Last30Days.ActiveAccounts);
    }

    [Fact]
    public async Task AdminNutzung_UndNutzerliste_SchliessenGaesteAus()
    {
        var (provider, _) = await BaueAsync();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        await EchtesKontoAsync(sp.CreateScope(), "echt@dogity.test");
        await GastAsync(sp);

        var nutzung = (await sp.GetRequiredService<IAdminUsageService>().GetAsync()).Value!;
        Assert.Equal(1, nutzung.Activity.TotalAccounts);
        Assert.Equal(1, nutzung.Activity.ActiveLast90Days);
        // Nur das eine Training des echten Kontos - die Gast-Trainings, -Fährte und das Ziel zählen nicht.
        Assert.Equal(1, nutzung.Features.Single(f => f.Key == "training").AccountsEver);
        Assert.Equal(1, nutzung.Features.Single(f => f.Key == "training").EntriesLast30Days);
        Assert.All(new[] { "track-laid", "track-walk", "goal", "plan", "group-session", "session-response" },
            key => Assert.Equal(0, nutzung.Features.Single(f => f.Key == key).AccountsEver));

        var liste = (await sp.GetRequiredService<IAdminService>().GetUsersAsync()).Value!;
        Assert.Equal(1, liste.TotalCount);
        Assert.Equal("echt@dogity.test", Assert.Single(liste.Users).Email);
    }

    [Fact]
    public void Mail_AnGastadresse_WirdNichtEingereiht()
    {
        var queue = new MailQueue(NullLogger<MailQueue>.Instance);

        Assert.True(queue.Enqueue(new MailNachricht("gast-123@gast.invalid", "Betreff", "Text", null)));
        Assert.False(queue.Reader.TryRead(out _));

        Assert.True(queue.Enqueue(new MailNachricht("echt@dogity.test", "Betreff", "Text", null)));
        Assert.True(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Loeschdienst_EntferntNachAblaufAllesRestlos_NurEchteDatenBleiben()
    {
        var (provider, uhr) = await BaueAsync();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var (echt, echterVerein) = await EchtesKontoAsync(sp.CreateScope(), "echt@dogity.test");
        await GastAsync(sp);
        var dienst = sp.GetRequiredService<GastkontoService>();
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Equal(7, await users.Users.CountAsync());

        // Vor Ablauf bleibt alles.
        uhr.Jetzt += TimeSpan.FromHours(3);
        Assert.Equal(0, await dienst.EntferneAbgelaufeneAsync());
        Assert.Equal(7, await users.Users.CountAsync());

        uhr.Jetzt += TimeSpan.FromHours(2);
        Assert.Equal(6, await dienst.EntferneAbgelaufeneAsync());

        // Vom Gast, seinen erfundenen Mitgliedern und dem Gast-Verein ist keine Zeile übrig ...
        Assert.Equal([echt.Id], await users.Users.Select(u => u.Id).ToListAsync());
        Assert.Equal(1, await db.Dogs.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await db.DogOwners.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await db.TrainingSessions.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.TrainingExercises.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Goals.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.TrainingPlans.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.TrainingPlanItems.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.GpsTracks.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.GpsPoints.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.GpsWalkRuns.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.GpsWalkPoints.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.ExerciseMasteries.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.UserPreferences.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.UserSportSelections.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Notifications.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await db.Clubs.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await db.ClubTrainers.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.ClubMemberships.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await db.Groups.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.GroupMembers.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.GroupTrainers.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.GroupTrainingSessions.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.GroupTrainingSessionTrainers.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.GroupTrainingSessionItems.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.GroupTrainingSessionResponses.IgnoreQueryFilters().CountAsync());

        // ... und der echte Verein samt Gruppe und Rolle ist unberührt.
        Assert.Equal(echterVerein.Id, (await db.Clubs.SingleAsync()).Id);
        Assert.Equal(echt.Id, (await db.ClubTrainers.SingleAsync()).UserId);
    }

    [Fact]
    public async Task AbgelaufenerGast_IstFuerDieAnfragenSofortNichtMehrNutzbar()
    {
        var (provider, uhr) = await BaueAsync();
        using var scope = provider.CreateScope();
        var gast = await GastAsync(scope.ServiceProvider);
        var status = scope.ServiceProvider.GetRequiredService<KontoStatus>();

        Assert.True(await status.IstNutzbarAsync(gast.Id));
        uhr.Jetzt += Gastkonto.Laufzeit + TimeSpan.FromMinutes(1);
        status.Vergessen(gast.Id);

        Assert.False(await status.IstNutzbarAsync(gast.Id));
    }
}
