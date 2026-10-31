using System.Text.Json;
using Dogity.Application.Common;
using Dogity.Application.Notifications;
using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Training;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Notifications;
using Dogity.Domain.Preferences;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dogity.Application.Tests.Notifications;

/// <summary>
/// Benachrichtigung, Push und Mail in der Sprache der Empfänger:in: Der Satz entsteht
/// einmal und steht dann in Glocke, Mail und Push gleich da. Ohne gespeicherte Sprache
/// (bestehende Konten) bleibt es Deutsch.
/// </summary>
public class MeldungSpracheTests
{
    private sealed record Umgebung(
        ApplicationDbContext Db,
        FakeUserLookupService Lookup,
        FakeMailQueue Mails,
        FakePushQueue Pushs,
        NotificationService Service);

    private static Umgebung Aufbauen()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var kanaele = new FakeNotificationChannels { EmailAktiv = true, PushPublicKey = "pub-key" };
        var mails = new FakeMailQueue();
        var pushs = new FakePushQueue();
        var service = new NotificationService(db, lookup, kanaele, TestLinks.Mit(), TestAbmeldeToken.Neu(), mails, pushs, NullLogger<NotificationService>.Instance);
        return new Umgebung(db, lookup, mails, pushs, service);
    }

    /// <summary>Eine Person mit Konto, einem Push-Gerät und - wenn angegeben - gespeicherter Sprache.</summary>
    private static async Task<Guid> PersonAsync(Umgebung u, string? sprache, string vorname = "Max")
    {
        var id = Guid.NewGuid();
        u.Lookup.Register(id, $"{vorname.ToLowerInvariant()}@example.com", vorname, "Muster");
        u.Db.PushSubscriptions.Add(new PushSubscription { UserId = id, Endpoint = $"https://fcm.googleapis.com/{id}", P256dh = "a", Auth = "b" });
        if (sprache is not null) u.Db.UserPreferences.Add(new UserPreference { UserId = id, Locale = sprache });
        await u.Db.SaveChangesAsync();
        return id;
    }

    private static Meldung Einladung() =>
        Meldung.Von("{name} lädt dich in den Verein \"{verein}\" ein.", ("name", "Anna"), ("verein", "Nord"));

    [Fact]
    public async Task EnglischeEmpfaengerin_BekommtGlockeMailUndPushAufEnglisch()
    {
        var u = Aufbauen();
        var id = await PersonAsync(u, "en");

        await u.Service.CreateAsync(id, Einladung(), "/clubs", wichtig: true);

        const string erwartet = "Anna invites you to the club \"Nord\".";
        Assert.Equal(erwartet, Assert.Single(u.Db.Notifications).Message);

        var mail = Assert.Single(u.Mails.Mails);
        Assert.Equal($"Dogity: {erwartet}", mail.Betreff);
        Assert.Contains(erwartet, mail.Text);
        Assert.Contains("Hi Max,", mail.Text);
        Assert.Contains("The Dogity team", mail.Text);
        Assert.Contains("lang=\"en\"", mail.Html);
        Assert.Contains("View in Dogity", mail.Html);
        // Der Abmelde-Link bleibt auch in der englischen Mail.
        Assert.NotNull(mail.AbmeldeSeite);
        Assert.Contains(mail.AbmeldeSeite!, mail.Text);
        Assert.Contains("Unsubscribe here", mail.Html);

        using var push = JsonDocument.Parse(Assert.Single(u.Pushs.Auftraege).Payload);
        var notification = push.RootElement.GetProperty("notification");
        Assert.Equal(erwartet, notification.GetProperty("body").GetString());
        Assert.Equal("en", notification.GetProperty("lang").GetString());
        Assert.Equal("Dogity", notification.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("de")]
    [InlineData("fr")]
    public async Task OhneGespeicherteSpracheOderMitDeutsch_BleibtAllesDeutsch(string? sprache)
    {
        var u = Aufbauen();
        var id = await PersonAsync(u, sprache);

        await u.Service.CreateAsync(id, Einladung(), "/clubs", wichtig: true);

        const string erwartet = "Anna lädt dich in den Verein \"Nord\" ein.";
        Assert.Equal(erwartet, Assert.Single(u.Db.Notifications).Message);
        var mail = Assert.Single(u.Mails.Mails);
        Assert.Equal($"Dogity: {erwartet}", mail.Betreff);
        Assert.Contains("Hallo Max,", mail.Text);
        Assert.Contains("lang=\"de\"", mail.Html);
        using var push = JsonDocument.Parse(Assert.Single(u.Pushs.Auftraege).Payload);
        Assert.Equal("de", push.RootElement.GetProperty("notification").GetProperty("lang").GetString());
    }

    [Fact]
    public async Task DieselbeMeldungAnZweiPersonen_JeInIhrerSprache()
    {
        var u = Aufbauen();
        var englisch = await PersonAsync(u, "en", "Ella");
        var deutsch = await PersonAsync(u, "de", "Dora");

        var meldung = Meldung.Von("Das Training {gruppe} am {datum} fällt aus.", ("gruppe", "Welpen"), ("datum", new DateOnly(2026, 10, 3)));
        await u.Service.CreateAsync(englisch, meldung, "/");
        await u.Service.CreateAsync(deutsch, meldung, "/");

        Assert.Equal("The Welpen training on 3 Oct 2026 is cancelled.", u.Db.Notifications.Single(n => n.UserId == englisch).Message);
        Assert.Equal("Das Training Welpen am 03.10.2026 fällt aus.", u.Db.Notifications.Single(n => n.UserId == deutsch).Message);
    }

    [Fact]
    public async Task SchonGespeicherteMeldungen_WerdenNichtNachtraeglichUebersetzt()
    {
        var u = Aufbauen();
        var id = await PersonAsync(u, null);
        await u.Service.CreateAsync(id, Einladung(), "/clubs");

        // Die Person stellt auf Englisch um.
        u.Db.UserPreferences.Add(new UserPreference { UserId = id, Locale = "en" });
        await u.Db.SaveChangesAsync();

        var alt = Assert.Single(u.Db.Notifications);
        Assert.Equal("Anna lädt dich in den Verein \"Nord\" ein.", alt.Message);

        await u.Service.CreateAsync(id, Einladung(), "/clubs");
        Assert.Contains(u.Db.Notifications, n => n.Message == "Anna invites you to the club \"Nord\".");
        Assert.Contains(u.Db.Notifications, n => n.Message == "Anna lädt dich in den Verein \"Nord\" ein.");
    }

    [Fact]
    public async Task HatUngelesen_VergleichtMitDemTextInDerSpracheDerPerson()
    {
        var u = Aufbauen();
        var englisch = await PersonAsync(u, "en", "Ella");
        var deutsch = await PersonAsync(u, null, "Dora");
        await u.Service.CreateAsync(englisch, Einladung(), "/clubs");
        await u.Service.CreateAsync(deutsch, Einladung(), "/clubs");

        Assert.True(await u.Service.HatUngelesenAsync(englisch, Einladung(), "/clubs"));
        Assert.True(await u.Service.HatUngelesenAsync(deutsch, Einladung(), "/clubs"));
        // Anderer Link oder andere Meldung: nicht dieselbe.
        Assert.False(await u.Service.HatUngelesenAsync(englisch, Einladung(), "/dashboard"));
        Assert.False(await u.Service.HatUngelesenAsync(englisch, Meldung.Von("Du bist jetzt Trainer bei \"{verein}\".", ("verein", "Nord")), "/clubs"));

        // Gelesen zählt nicht mehr.
        foreach (var n in u.Db.Notifications.Where(n => n.UserId == englisch)) n.IsRead = true;
        await u.Db.SaveChangesAsync();
        Assert.False(await u.Service.HatUngelesenAsync(englisch, Einladung(), "/clubs"));
    }

    [Fact]
    public async Task Feedback_RueckmeldungDerBesitzerin_DoppeltSichAuchBeiEnglischerTrainerinNicht()
    {
        // Der Schutz gegen gleichlautende Meldungen vergleicht den gespeicherten Text -
        // der ist bei englischen Empfänger:innen englisch.
        var u = Aufbauen();
        var besitzer = await PersonAsync(u, "de", "Max");
        var trainer = await PersonAsync(u, "en", "Tina");
        var hund = new Dog { Name = "Bello" };
        u.Db.Dogs.Add(hund);
        u.Db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = besitzer });
        u.Db.TrainerAssignments.Add(new TrainerAssignment { DogId = hund.Id, TrainerId = trainer, MemberId = besitzer });
        var eintrag = new TrainingSession { UserId = besitzer, DogId = hund.Id, Date = new DateOnly(2026, 9, 12), DurationMinutes = 30 };
        u.Db.TrainingSessions.Add(eintrag);
        await u.Db.SaveChangesAsync();
        var training = new TrainingService(u.Db, u.Service, u.Lookup, new ExerciseMasteryService(u.Db), new FakeWeatherEnrichmentService(), TimeProvider.System);
        Assert.True((await training.SetFeedbackAsync(trainer, eintrag.Id, new SetFeedbackRequest("Schön gearbeitet."))).Succeeded);
        var davor = u.Db.Notifications.Count(n => n.UserId == trainer);

        await training.ReplyToFeedbackAsync(besitzer, eintrag.Id, new FeedbackReplyRequest(FeedbackReaction.Thanks, null));
        await training.ReplyToFeedbackAsync(besitzer, eintrag.Id, new FeedbackReplyRequest(FeedbackReaction.Understood, null));

        var neu = Assert.Single(u.Db.Notifications.Where(n => n.UserId == trainer).Skip(davor));
        Assert.Equal("Max has replied to your feedback on the training session from 12 Sep 2026.", neu.Message);
        // Und die Besitzerin las das Feedback deutsch.
        Assert.Equal(
            "Dein Trainer hat Feedback zum Training von Bello vom 12.09.2026 hinterlassen.",
            Assert.Single(u.Db.Notifications.Where(n => n.UserId == besitzer)).Message);
    }

    [Fact]
    public async Task Feedback_AnEnglischeBesitzerin_IstEnglisch()
    {
        var u = Aufbauen();
        var besitzer = await PersonAsync(u, "en", "Max");
        var trainer = await PersonAsync(u, null, "Tina");
        var hund = new Dog { Name = "Bello" };
        u.Db.Dogs.Add(hund);
        u.Db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = besitzer });
        u.Db.TrainerAssignments.Add(new TrainerAssignment { DogId = hund.Id, TrainerId = trainer, MemberId = besitzer });
        var eintrag = new TrainingSession { UserId = besitzer, DogId = hund.Id, Date = new DateOnly(2026, 9, 12), DurationMinutes = 30 };
        u.Db.TrainingSessions.Add(eintrag);
        await u.Db.SaveChangesAsync();
        var training = new TrainingService(u.Db, u.Service, u.Lookup, new ExerciseMasteryService(u.Db), new FakeWeatherEnrichmentService(), TimeProvider.System);

        await training.SetFeedbackAsync(trainer, eintrag.Id, new SetFeedbackRequest("Schön gearbeitet."));

        Assert.Equal(
            "Your trainer left feedback on Bello's training session from 12 Sep 2026.",
            Assert.Single(u.Db.Notifications).Message);
    }

    [Fact]
    public async Task Testpush_KommtInDerSpracheDerAdminPerson()
    {
        var u = Aufbauen();
        var admin = await PersonAsync(u, "en", "Alex");
        var dienst = new PushTestService(u.Db, u.Service, new FakeNotificationChannels { PushPublicKey = "pub-key" }, TestLinks.Mit());

        await dienst.SendeAnMichAsync(admin);

        Assert.Equal(
            "Test push from the admin area: if this message arrives, push works on your devices.",
            Assert.Single(u.Db.Notifications).Message);
        using var push = JsonDocument.Parse(Assert.Single(u.Pushs.Auftraege).Payload);
        Assert.Equal("en", push.RootElement.GetProperty("notification").GetProperty("lang").GetString());
    }
}
