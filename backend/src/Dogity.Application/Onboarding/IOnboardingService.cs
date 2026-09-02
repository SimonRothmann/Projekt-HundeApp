using Dogity.Application.Common;

namespace Dogity.Application.Onboarding;

/// <summary>
/// Der geführte Erststart: Hund anlegen, dann entweder Ziel und erstes
/// Training - oder Verein und Trainingsgruppe.
///
/// Ein leeres Dashboard ist die härteste Hürde der App: Man sieht, dass etwas
/// fehlt, aber nicht, was als Erstes zu tun wäre.
/// </summary>
public interface IOnboardingService
{
    Task<Result<OnboardingStatusDto>> GetStatusAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Wegklicken. Bleibt weggeklickt - auch auf einem anderen Gerät, deshalb
    /// am Nutzer und nicht im Browser gespeichert.
    /// </summary>
    Task<Result> DismissAsync(Guid userId, CancellationToken ct = default);
}
