using System.IdentityModel.Tokens.Jwt;
using MMORPG.Api.DTOs;
using MMORPG.Api.Tests.Helpers;

namespace MMORPG.Api.Tests.Auth;

public class JwtTokenTests
{
    [Fact]
    public async Task Login_ReturnsNonEmptyAccessToken()
    {
        var (service, _) = AuthServiceFactory.Create();
        await service.RegisterAsync(new RegisterRequest("JwtUser1", "jwt1@example.com", "Password123!"));

        var response = await service.LoginAsync(new LoginRequest("jwt1@example.com", "Password123!"));

        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
    }

    [Fact]
    public async Task Login_AccessTokenContainsCorrectClaims()
    {
        var (service, _) = AuthServiceFactory.Create();
        await service.RegisterAsync(new RegisterRequest("JwtUser2", "jwt2@example.com", "Password123!"));

        var response = await service.LoginAsync(new LoginRequest("jwt2@example.com", "Password123!"));

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(response.AccessToken);

        Assert.Equal("jwt2@example.com", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("JwtUser2", jwt.Claims.First(c => c.Type == "username").Value);
        Assert.Equal("false", jwt.Claims.First(c => c.Type == "is_admin").Value);
        Assert.NotEmpty(jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
    }

    [Fact]
    public async Task Login_AccessTokenExpiresInApproximately15Minutes()
    {
        var (service, _) = AuthServiceFactory.Create();
        await service.RegisterAsync(new RegisterRequest("JwtUser3", "jwt3@example.com", "Password123!"));

        var before = DateTimeOffset.UtcNow;
        var response = await service.LoginAsync(new LoginRequest("jwt3@example.com", "Password123!"));
        var after = DateTimeOffset.UtcNow;

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(response.AccessToken);

        var expiry = new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero);
        // JWT stores expiry as Unix seconds — allow 1 second of sub-second truncation
        var expectedExpiry = before.AddMinutes(15).AddSeconds(-1);

        Assert.True(expiry >= expectedExpiry, $"Token expired too early: {expiry} < {expectedExpiry}");
        Assert.True(expiry <= after.AddMinutes(15).AddSeconds(5), $"Token expires too late: {expiry}");
    }

    [Fact]
    public async Task Login_AccessTokenHasCorrectIssuerAndAudience()
    {
        var (service, _) = AuthServiceFactory.Create();
        await service.RegisterAsync(new RegisterRequest("JwtUser4", "jwt4@example.com", "Password123!"));

        var response = await service.LoginAsync(new LoginRequest("jwt4@example.com", "Password123!"));

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(response.AccessToken);

        Assert.Equal("test-issuer", jwt.Issuer);
        Assert.Contains("test-audience", jwt.Audiences);
    }

    [Fact]
    public async Task Login_ReturnsRefreshTokenWithFutureExpiry()
    {
        var (service, _) = AuthServiceFactory.Create();
        await service.RegisterAsync(new RegisterRequest("JwtUser5", "jwt5@example.com", "Password123!"));

        var response = await service.LoginAsync(new LoginRequest("jwt5@example.com", "Password123!"));

        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
        Assert.True(response.AccessTokenExpiry > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Refresh_RotatesRefreshTokenAndIssuesNewAccessToken()
    {
        var (service, db) = AuthServiceFactory.Create();
        await service.RegisterAsync(new RegisterRequest("JwtUser6", "jwt6@example.com", "Password123!"));
        var login = await service.LoginAsync(new LoginRequest("jwt6@example.com", "Password123!"));

        var refreshed = await service.RefreshAsync(login.RefreshToken);

        Assert.NotEqual(login.AccessToken, refreshed.AccessToken);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);

        // Old refresh token must be revoked
        var old = db.RefreshTokens.ToList().FirstOrDefault(rt => rt.IsRevoked);
        Assert.NotNull(old);
    }

    [Fact]
    public async Task Refresh_ThrowsOnRevokedToken()
    {
        var (service, _) = AuthServiceFactory.Create();
        await service.RegisterAsync(new RegisterRequest("JwtUser7", "jwt7@example.com", "Password123!"));
        var login = await service.LoginAsync(new LoginRequest("jwt7@example.com", "Password123!"));
        await service.RefreshAsync(login.RefreshToken); // first use — rotates it

        // Second use of same token should throw
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RefreshAsync(login.RefreshToken));
    }
}
