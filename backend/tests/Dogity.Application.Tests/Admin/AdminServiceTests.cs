using Dogity.Application.Admin;
using Dogity.Application.Tests.TestSupport;

namespace Dogity.Application.Tests.Admin;

/// <summary>
/// Testet die Admin-Nutzerverwaltung (Sperren, Entsperren, Löschen, Pagination).
/// Da AdminService für diese Operationen vollständig an IUserLookupService
/// delegiert, testet dies das korrekte Result-Mapping und Grenzverhalten.
/// </summary>
public class AdminServiceTests
{
    private static AdminService MakeService(out FakeUserLookupService lookup) => MakeService(out lookup, out _);

    private static AdminService MakeService(out FakeUserLookupService lookup, out FakeRefreshTokenService refreshTokens)
    {
        var db = InMemoryDbContext.Create();
        lookup = new FakeUserLookupService();
        refreshTokens = new FakeRefreshTokenService();
        return new AdminService(db, lookup, refreshTokens);
    }

    [Fact]
    public async Task LockUser_UserExists_Succeeds()
    {
        var service = MakeService(out var lookup);
        var userId = Guid.NewGuid();
        lookup.Register(userId, "user@test.de");

        var result = await service.LockUserAsync(userId);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task LockUser_RevokesRefreshTokens()
    {
        var service = MakeService(out var lookup, out var refreshTokens);
        var userId = Guid.NewGuid();
        lookup.Register(userId, "user@test.de");

        await service.LockUserAsync(userId);

        // Sperre muss die Sitzungen sofort beenden - sonst könnte der Nutzer
        // bis zum Access-Token-Ablauf weiter neue Tokens nachladen.
        Assert.Contains(userId, refreshTokens.RevokedAllForUsers);
    }

    [Fact]
    public async Task LockUser_UserNotFound_Fails()
    {
        var service = MakeService(out _);

        var result = await service.LockUserAsync(Guid.NewGuid());

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task DeleteUser_UserExists_RemovesAndSucceeds()
    {
        var service = MakeService(out var lookup);
        var userId = Guid.NewGuid();
        lookup.Register(userId, "user@test.de");

        var result = await service.DeleteUserAsync(userId);

        Assert.True(result.Succeeded);
        // Anschließend sollte der User im Fake nicht mehr vorhanden sein
        var page = await service.GetUsersAsync();
        Assert.DoesNotContain(page.Value!.Users, u => u.Id == userId);
    }

    [Fact]
    public async Task GetUsers_DefaultPage_Returns50PerPage()
    {
        var service = MakeService(out var lookup);
        for (var i = 0; i < 75; i++)
            lookup.Register(Guid.NewGuid(), $"user{i:D3}@test.de");

        var page1 = await service.GetUsersAsync(page: 1);
        var page2 = await service.GetUsersAsync(page: 2);

        Assert.True(page1.Succeeded);
        Assert.Equal(50, page1.Value!.Users.Count);
        Assert.Equal(75, page1.Value.TotalCount);
        Assert.Equal(2, page1.Value.TotalPages);
        Assert.Equal(25, page2.Value!.Users.Count);
    }

    [Fact]
    public async Task GetUsers_PageClampsAtOne_WhenZeroGiven()
    {
        var service = MakeService(out var lookup);
        lookup.Register(Guid.NewGuid(), "user@test.de");

        var result = await service.GetUsersAsync(page: 0);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.Page);
    }
}
