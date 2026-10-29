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
    public List<Guid> DeletedAllForUsers { get; } = [];

    public Task<string> IssueAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult($"fake-refresh-{userId}");

    public Task<RefreshRotationResult> RotateAsync(string rawToken, CancellationToken ct = default)
        => Task.FromResult(new RefreshRotationResult(false));

    public Task<RefreshRotationResult> RefreshAsync(string rawToken, CancellationToken ct = default)
        => Task.FromResult(new RefreshRotationResult(false));

    public Task RevokeAsync(string rawToken, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        RevokedAllForUsers.Add(userId);
        return Task.CompletedTask;
    }

    public Task RevokeAllExceptAsync(Guid userId, string? keepRawToken, CancellationToken ct = default)
        => RevokeAllForUserAsync(userId, ct);

    public Task DeleteAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        DeletedAllForUsers.Add(userId);
        return Task.CompletedTask;
    }

    public Task<int> DeleteOrphanedAsync(CancellationToken ct = default)
        => Task.FromResult(0);

    /// <summary>Wer wann einen Token bekam - Grundlage für CountAccountsWithTokenSinceAsync.</summary>
    public List<(Guid UserId, DateTimeOffset At)> Ausgestellt { get; } = [];

    public Task<int> CountAccountsWithTokenSinceAsync(DateTimeOffset since, CancellationToken ct = default)
        => Task.FromResult(Ausgestellt.Where(t => t.At >= since).Select(t => t.UserId).Distinct().Count());
}
