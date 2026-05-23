using MMORPG.Api.DTOs;
using MMORPG.Api.Tests.Helpers;

namespace MMORPG.Api.Tests.Auth;

public class PasswordHashingTests
{
    [Fact]
    public async Task Register_StoresPasswordHashWithBcryptCostFactor12()
    {
        var (service, db) = AuthServiceFactory.Create();
        var request = new RegisterRequest("TestPlayer", "test@example.com", "SecurePassword123!");

        await service.RegisterAsync(request);

        var player = db.Players.First();
        // BCrypt cost-12 hashes are always prefixed "$2a$12$"
        Assert.StartsWith("$2a$12$", player.PasswordHash);
    }

    [Fact]
    public async Task Register_PasswordHashVerifiesCorrectlyWithBcrypt()
    {
        var (service, db) = AuthServiceFactory.Create();
        const string plaintext = "SecurePassword123!";
        await service.RegisterAsync(new RegisterRequest("TestPlayer2", "test2@example.com", plaintext));

        var player = db.Players.First();
        Assert.True(BCrypt.Net.BCrypt.Verify(plaintext, player.PasswordHash));
    }

    [Fact]
    public async Task Register_PasswordHashDoesNotVerifyWrongPassword()
    {
        var (service, db) = AuthServiceFactory.Create();
        await service.RegisterAsync(new RegisterRequest("TestPlayer3", "test3@example.com", "CorrectPassword!"));

        var player = db.Players.First();
        Assert.False(BCrypt.Net.BCrypt.Verify("WrongPassword!", player.PasswordHash));
    }
}
