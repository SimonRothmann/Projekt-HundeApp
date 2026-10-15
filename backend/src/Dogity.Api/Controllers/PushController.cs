using Dogity.Application.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

/// <summary>
/// Geräte für Push-Benachrichtigungen an- und abmelden. Angemeldet, und jede:r
/// verwaltet nur die eigenen Geräte.
/// </summary>
[Route("api/push/subscriptions")]
public class PushController(IPushSubscriptionService subscriptions) : ApiControllerBase
{
    /// <summary>
    /// Meldet das Gerät an (oder erneuert es). Der Body ist das unveränderte
    /// <c>PushSubscription.toJSON()</c> des Browsers.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Subscribe(SubscribePushRequest request, CancellationToken ct) =>
        FromResult(await subscriptions.SubscribeAsync(CurrentUserId, request, Request.Headers.UserAgent.ToString(), ct));

    [HttpDelete]
    public async Task<IActionResult> Unsubscribe(UnsubscribePushRequest request, CancellationToken ct) =>
        FromResult(await subscriptions.UnsubscribeAsync(CurrentUserId, request, ct));
}
