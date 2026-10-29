using Dogity.Application.Notifications;
using Dogity.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dogity.Application.Tests.Notifications;

/// <summary>Die Mail-Warteschlange hält den Vertrag von <see cref="IMailQueue"/>: voll gleich false.</summary>
public class MailQueueTests
{
    private static MailNachricht Mail() => new("a@dogity.example", "Betreff", "Text", null);

    [Fact]
    public void Enqueue_NimmtBisZurKapazitaetAnUndLiefertDanachFalse()
    {
        var queue = new MailQueue(NullLogger<MailQueue>.Instance);

        for (var i = 0; i < 500; i++) Assert.True(queue.Enqueue(Mail()));

        // Voll: die Mail wird verworfen, nichts wirft.
        Assert.False(queue.Enqueue(Mail()));

        // Wieder Platz.
        Assert.True(queue.Reader.TryRead(out _));
        Assert.True(queue.Enqueue(Mail()));
    }
}
