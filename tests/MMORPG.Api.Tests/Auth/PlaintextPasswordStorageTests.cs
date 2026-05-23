using Microsoft.EntityFrameworkCore;
using MMORPG.Api.DTOs;
using MMORPG.Api.Tests.Helpers;

namespace MMORPG.Api.Tests.Auth;

public class PlaintextPasswordStorageTests
{
    [Fact]
    public async Task Register_NeverStoresPlaintextPassword()
    {
        var (service, db) = AuthServiceFactory.Create();
        const string plaintext = "MySecret$Password99";
        await service.RegisterAsync(new RegisterRequest("PlayerA", "a@example.com", plaintext));

        var player = await db.Players.FirstAsync();

        Assert.NotEqual(plaintext, player.PasswordHash);
    }

    [Fact]
    public async Task Register_StoredHashIsRecognisedBcryptFormat()
    {
        var (service, db) = AuthServiceFactory.Create();
        await service.RegisterAsync(new RegisterRequest("PlayerB", "b@example.com", "AnotherPassword!1"));

        var player = await db.Players.FirstAsync();

        // All BCrypt.Net hashes begin with the Blowfish magic prefix
        Assert.True(
            player.PasswordHash.StartsWith("$2a$") || player.PasswordHash.StartsWith("$2b$"),
            $"Expected a bcrypt hash but got: {player.PasswordHash[..Math.Min(20, player.PasswordHash.Length)]}..."
        );
    }

    [Fact]
    public async Task Register_PasswordHashDoesNotContainPlaintextAsSubstring()
    {
        var (service, db) = AuthServiceFactory.Create();
        const string plaintext = "UniqueDetectablePassword123";
        await service.RegisterAsync(new RegisterRequest("PlayerC", "c@example.com", plaintext));

        var player = await db.Players.FirstAsync();

        Assert.DoesNotContain(plaintext, player.PasswordHash);
    }
}
