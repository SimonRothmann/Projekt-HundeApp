using Dogity.Application.Tests.TestSupport;
using Dogity.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace Dogity.Application.Tests.Identity;

/// <summary>
/// Testet die Refresh-Token-Rotation inkl. Reuse-Erkennung (siehe
/// RefreshTokenService / TODO.md Roadmap 6): ein Refresh dreht den Token,
/// der alte wird ungültig, und die Wiedervorlage eines bereits verbrauchten
/// Tokens widerruft die ganze Kette.
/// </summary>
public class RefreshTokenServiceTests
{
    private static RefreshTokenService MakeService()
    {
        var db = InMemoryDbContext.Create();
        var settings = Options.Create(new JwtSettings { RefreshExpiryDays = 60 });
        return new RefreshTokenService(db, settings);
    }

    [Fact]
    public async Task Rotate_WithValidToken_IssuesNewAndInvalidatesOld()
    {
        var service = MakeService();
        var userId = Guid.NewGuid();
        var original = await service.IssueAsync(userId);

        var rotation = await service.RotateAsync(original);

        Assert.True(rotation.Succeeded);
        Assert.Equal(userId, rotation.UserId);
        Assert.NotNull(rotation.NewRawToken);
        Assert.NotEqual(original, rotation.NewRawToken);

        // Der alte Token ist jetzt verbraucht - erneutes Rotieren scheitert.
        var reuse = await service.RotateAsync(original);
        Assert.False(reuse.Succeeded);
    }

    [Fact]
    public async Task Rotate_ReusedToken_RevokesEntireChain()
    {
        var service = MakeService();
        var userId = Guid.NewGuid();
        var original = await service.IssueAsync(userId);
        var rotation = await service.RotateAsync(original);
        var newToken = rotation.NewRawToken!;

        // Angreifer legt den bereits verbrauchten Original-Token erneut vor:
        // Reuse-Erkennung greift und widerruft die ganze Kette.
        var reuse = await service.RotateAsync(original);
        Assert.False(reuse.Succeeded);

        // Folge: Auch der zwischenzeitlich gültige neue Token ist jetzt tot -
        // der legitime Nutzer muss sich neu anmelden (sichere Reaktion auf
        // möglichen Diebstahl).
        var afterReuse = await service.RotateAsync(newToken);
        Assert.False(afterReuse.Succeeded);
    }

    [Fact]
    public async Task Rotate_UnknownToken_Fails()
    {
        var service = MakeService();
        var result = await service.RotateAsync("nie-ausgestellt");
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RevokeAllForUser_InvalidatesActiveTokens()
    {
        var service = MakeService();
        var userId = Guid.NewGuid();
        var tokenA = await service.IssueAsync(userId);
        var tokenB = await service.IssueAsync(userId);

        await service.RevokeAllForUserAsync(userId);

        Assert.False((await service.RotateAsync(tokenA)).Succeeded);
        Assert.False((await service.RotateAsync(tokenB)).Succeeded);
    }

    [Fact]
    public async Task Revoke_SingleToken_LeavesOtherDevicesActive()
    {
        var service = MakeService();
        var userId = Guid.NewGuid();
        var phone = await service.IssueAsync(userId);
        var laptop = await service.IssueAsync(userId);

        await service.RevokeAsync(phone);

        // Nur das eine Gerät ist abgemeldet, das andere bleibt gültig.
        Assert.False((await service.RotateAsync(phone)).Succeeded);
        Assert.True((await service.RotateAsync(laptop)).Succeeded);
    }
}
