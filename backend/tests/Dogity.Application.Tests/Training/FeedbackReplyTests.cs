using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Training;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Training;

/// <summary>
/// Reaktion und Rückfrage des Besitzers auf Trainer-Feedback: nur
/// Besitzer:innen, nur mit vorhandenem Feedback, neues Feedback setzt zurück,
/// Benachrichtigungen führen auf den Eintrag.
/// </summary>
public class FeedbackReplyTests
{
    private sealed record Szene(
        TrainingService Dienst, ApplicationDbContext Db, FakeNotificationService Meldungen,
        Guid Besitzer, Guid Trainer, Guid Hund, Guid EintragId);

    private static async Task<Szene> AufbauenAsync(bool mitFeedback = true)
    {
        var db = InMemoryDbContext.Create();
        var meldungen = new FakeNotificationService();
        var lookup = new FakeUserLookupService();
        var dienst = new TrainingService(db, meldungen, lookup, new ExerciseMasteryService(db), new FakeWeatherEnrichmentService());

        var besitzer = Guid.NewGuid();
        var trainer = Guid.NewGuid();
        lookup.Register(besitzer, "max@dogity.test", "Max", "Mustermann");
        var hund = new Dog { Name = "Bello" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = besitzer });
        db.TrainerAssignments.Add(new TrainerAssignment { DogId = hund.Id, TrainerId = trainer, MemberId = besitzer });
        var eintrag = new TrainingSession
        {
            UserId = besitzer, DogId = hund.Id, Date = new DateOnly(2026, 9, 12), DurationMinutes = 30,
        };
        db.TrainingSessions.Add(eintrag);
        await db.SaveChangesAsync();

        if (mitFeedback)
        {
            var r = await dienst.SetFeedbackAsync(trainer, eintrag.Id, new SetFeedbackRequest("Schön gearbeitet."));
            Assert.True(r.Succeeded);
            meldungen.Created.Clear();
        }
        return new Szene(dienst, db, meldungen, besitzer, trainer, hund.Id, eintrag.Id);
    }

    [Fact]
    public async Task SetFeedback_LinktAufDenEintrag()
    {
        var s = await AufbauenAsync(mitFeedback: false);

        await s.Dienst.SetFeedbackAsync(s.Trainer, s.EintragId, new SetFeedbackRequest("Gut."));

        var meldung = Assert.Single(s.Meldungen.Created);
        Assert.Equal(s.Besitzer, meldung.UserId);
        Assert.Equal($"/dogs/{s.Hund}?eintrag={s.EintragId}", meldung.LinkPath);
    }

    [Fact]
    public async Task Reply_Besitzer_SpeichertReaktionUndRueckfrage()
    {
        var s = await AufbauenAsync();

        var result = await s.Dienst.ReplyToFeedbackAsync(s.Besitzer, s.EintragId,
            new FeedbackReplyRequest(FeedbackReaction.Thanks, "  Wie oft soll ich das üben?  "));

        Assert.True(result.Succeeded);
        var dto = (await s.Dienst.GetByIdAsync(s.Besitzer, s.EintragId)).Value!;
        Assert.Equal(FeedbackReaction.Thanks, dto.OwnerReaction);
        Assert.Equal("Wie oft soll ich das üben?", dto.OwnerReply);
        Assert.NotNull(dto.OwnerReplyAt);
    }

    [Fact]
    public async Task Reply_OhneFeedback_WirdAbgewiesen()
    {
        var s = await AufbauenAsync(mitFeedback: false);

        var result = await s.Dienst.ReplyToFeedbackAsync(s.Besitzer, s.EintragId, new FeedbackReplyRequest(FeedbackReaction.Thanks, null));

        Assert.False(result.Succeeded);
        Assert.False(result.IsNotFound);
    }

    [Fact]
    public async Task Reply_Fremde_BekommenNotFound()
    {
        var s = await AufbauenAsync();

        var result = await s.Dienst.ReplyToFeedbackAsync(Guid.NewGuid(), s.EintragId, new FeedbackReplyRequest(FeedbackReaction.Thanks, null));

        Assert.True(result.IsNotFound);
        var eintrag = await s.Db.TrainingSessions.FirstAsync();
        Assert.Null(eintrag.OwnerReaction);
    }

    [Fact]
    public async Task Reply_BetreuendeTrainerin_DarfNichtAufEigenesFeedbackAntworten()
    {
        var s = await AufbauenAsync();

        var result = await s.Dienst.ReplyToFeedbackAsync(s.Trainer, s.EintragId, new FeedbackReplyRequest(FeedbackReaction.Understood, null));

        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task Reply_MitbesitzerDarfAntworten()
    {
        var s = await AufbauenAsync();
        var mit = Guid.NewGuid();
        s.Db.DogOwners.Add(new DogOwner { DogId = s.Hund, UserId = mit });
        await s.Db.SaveChangesAsync();

        var result = await s.Dienst.ReplyToFeedbackAsync(mit, s.EintragId, new FeedbackReplyRequest(FeedbackReaction.Understood, null));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Reply_ZuLang_WirdAbgewiesen()
    {
        var s = await AufbauenAsync();

        var result = await s.Dienst.ReplyToFeedbackAsync(s.Besitzer, s.EintragId,
            new FeedbackReplyRequest(null, new string('x', Dogity.Application.Common.Textlaengen.FeedbackRueckfrage + 1)));

        Assert.False(result.Succeeded);
        Assert.False(result.IsNotFound);
    }

    [Fact]
    public async Task Reply_BenachrichtigtDieTrainerinMitLinkAufDenEintrag()
    {
        var s = await AufbauenAsync();

        await s.Dienst.ReplyToFeedbackAsync(s.Besitzer, s.EintragId, new FeedbackReplyRequest(FeedbackReaction.Thanks, null));

        var meldung = Assert.Single(s.Meldungen.Created);
        Assert.Equal(s.Trainer, meldung.UserId);
        Assert.Equal("Max hat auf dein Feedback zum Training vom 12.09.2026 geantwortet.", meldung.Message);
        Assert.Equal($"/dogs/{s.Hund}?eintrag={s.EintragId}", meldung.LinkPath);
    }

    [Fact]
    public async Task Reply_ZuruecknehmenOderUnveraendert_MeldetNichts()
    {
        var s = await AufbauenAsync();
        await s.Dienst.ReplyToFeedbackAsync(s.Besitzer, s.EintragId, new FeedbackReplyRequest(FeedbackReaction.Thanks, "Frage?"));
        s.Meldungen.Created.Clear();

        // Dasselbe noch einmal, dann alles zurücknehmen.
        await s.Dienst.ReplyToFeedbackAsync(s.Besitzer, s.EintragId, new FeedbackReplyRequest(FeedbackReaction.Thanks, "Frage?"));
        await s.Dienst.ReplyToFeedbackAsync(s.Besitzer, s.EintragId, new FeedbackReplyRequest(null, null));

        Assert.Empty(s.Meldungen.Created);
        var dto = (await s.Dienst.GetByIdAsync(s.Besitzer, s.EintragId)).Value!;
        Assert.Null(dto.OwnerReaction);
        Assert.Null(dto.OwnerReply);
        Assert.Null(dto.OwnerReplyAt);
    }

    [Fact]
    public async Task NeuesFeedback_SetztReaktionUndRueckfrageZurueck()
    {
        var s = await AufbauenAsync();
        await s.Dienst.ReplyToFeedbackAsync(s.Besitzer, s.EintragId, new FeedbackReplyRequest(FeedbackReaction.Understood, "Und wenn er zieht?"));

        await s.Dienst.SetFeedbackAsync(s.Trainer, s.EintragId, new SetFeedbackRequest("Ergänzung: bitte ruhiger."));

        var dto = (await s.Dienst.GetByIdAsync(s.Besitzer, s.EintragId)).Value!;
        Assert.Null(dto.OwnerReaction);
        Assert.Null(dto.OwnerReply);
        Assert.Null(dto.OwnerReplyAt);
        Assert.Equal("Ergänzung: bitte ruhiger.", dto.TrainerFeedback);
    }
}
