using Dogity.Application.Notifications;
using Dogity.Infrastructure.Push;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dogity.Application.Tests.Notifications;

/// <summary>Die Push-Warteschlange hält den Vertrag von <see cref="IPushQueue"/>: voll gleich false.</summary>
public class PushQueueTests
{
    private static PushAuftrag Auftrag() => new(Guid.NewGuid(), "{}");

    [Fact]
    public void Enqueue_NimmtBisZurKapazitaetAnUndLiefertDanachFalse()
    {
        var queue = new PushQueue(NullLogger<PushQueue>.Instance);

        for (var i = 0; i < 1000; i++) Assert.True(queue.Enqueue(Auftrag()));

        // Voll: der Auftrag wird verworfen, nichts wirft.
        Assert.False(queue.Enqueue(Auftrag()));

        // Wieder Platz.
        Assert.True(queue.Reader.TryRead(out _));
        Assert.True(queue.Enqueue(Auftrag()));
    }
}
