using Dogity.Application.Abstractions;

namespace Dogity.Application.Tests.TestSupport;

/// <summary>
/// Minimaler Fake für IRefreshTokenService in Tests, die nur die Aufrufe
/// beobachten wollen (z.B. dass ein Admin-Lock alle Tokens widerruft), ohne
/// echte Krypto/DB. Merkt sich die zuletzt widerrufenen UserIds.
/// </summary>
public class FakeRefreshTokenService : IRefreshTokenService
{
    public List<Guid> RevokedAllForUsers { get; } = [];

    public Task<string> IssueAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult($"fake-refresh-{userId}");

    public Task<RefreshRotationResult> RotateAsync(string rawToken, CancellationToken ct = default)
        => Task.FromResult(new RefreshRotationResult(false));

    public Task RevokeAsync(string rawToken, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        RevokedAllForUsers.Add(userId);
        return Task.CompletedTask;
    }
}
