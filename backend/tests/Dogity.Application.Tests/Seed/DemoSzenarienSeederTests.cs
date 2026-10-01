using Dogity.Application;
using Dogity.Application.Abstractions;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Weather;
using Dogity.Domain.Community;
using Dogity.Domain.Planning;
using Dogity.Infrastructure.Identity;
using Dogity.Infrastructure.Persistence;
using Dogity.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Application.Tests.Seed;

/// <summary>
/// Die Demo-Szenarien laufen bei jedem Start der Test-Umgebung (Development).
/// Sie dürfen dabei nichts doppelt anlegen und ohne Demo-Konten nichts tun -
/// sonst wüchse die Demo-Datenbank mit jedem Neustart oder fremde Daten
/// bekämen Demo-Inhalte.
/// </summary>
public class DemoSzenarienSeederTests
{
    private static ServiceProvider BaueDienste()
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
        return services.BuildServiceProvider();
    }

    private static async Task MitDemoKontenAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        await RoleSeeder.SeedAsync(scope.ServiceProvider);
        await SportCatalogSeeder.SeedAsync(scope.ServiceProvider);
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
        var termine = await db.GroupTrainingSessions.Include(s => s.Trainers).ToListAsync();
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
        var gruppe = await db.Groups.SingleAsync();
        Assert.False(string.IsNullOrEmpty(gruppe.RegistrationCode));
        var anmeldungen = await db.GroupRegistrations.Include(r => r.Attendances).ToListAsync();
        Assert.Equal(7, anmeldungen.Count);
        Assert.Equal(4, anmeldungen.Count(r => r.PaidAt is not null));
        Assert.Equal(3, anmeldungen.Select(r => r.Source).Distinct().Count());
        Assert.Single(anmeldungen, r => r.Notes is not null);
        Assert.True(anmeldungen.Select(r => r.Attendances.Count).Distinct().Count() >= 3);
        Assert.Contains(await db.Notifications.Where(n => n.UserId == trainerId).ToListAsync(), n => n.Message.StartsWith("Neue Anmeldung"));
    }
}
