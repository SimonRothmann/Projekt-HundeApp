using Dogity.Application.Abstractions;
using Dogity.Application.Onboarding;

namespace Dogity.Application.Tests.TestSupport;

/// <summary>
/// Ob der Erststart eine Person als zugehörig zu einem Verein ansieht - die Regel
/// steht im OnboardingService (Mitglied ODER mit einer Rolle im Verein, auch Vorstand).
/// </summary>
public static class Erststart
{
    public static async Task<bool> ZaehltImErststartZumVereinAsync(this IApplicationDbContext db, Guid userId) =>
        (await new OnboardingService(db, new FakeUserLookupService()).GetStatusAsync(userId)).Value!.HasClubMembership;
}
