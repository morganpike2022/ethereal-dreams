using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MMORPG.Api.Data;
using MMORPG.Api.DTOs;

namespace MMORPG.Api.Tests.Characters;

/// <summary>
/// End-to-end tests against the real PostgreSQL database (ethereal_dreams_dev).
/// Each test class instance registers a unique player, runs its tests, then
/// hard-deletes the player — cascade takes care of characters + refresh tokens.
/// </summary>
public class NameValidationIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _anon = null!;     // no auth — for validate-name calls
    private HttpClient _authed = null!;   // Bearer token — for character creation/deletion
    private Guid _testPlayerId;
    private int _testClassId;
    private string _sfx = null!;         // unique suffix so tests never collide in the shared DB

    // ── lifecycle ─────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        _sfx = Guid.NewGuid().ToString("N")[..8];

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("ASPNETCORE_ENVIRONMENT", "Development");
            b.UseSetting("ConnectionStrings:DefaultConnection",
                "Host=localhost;Port=5432;Database=ethereal_dreams_dev;Username=postgres;Password=postgres");
            b.UseSetting("Jwt:SecretKey", "PJF/T2KXleIVA6gb3MPfKeiRnbgi6c1RJA8rOxicN5w=");
            b.UseSetting("Jwt:Issuer", "ethereal-dreams-api");
            b.UseSetting("Jwt:Audience", "ethereal-dreams-client");
            b.UseSetting("Jwt:AccessTokenExpiryMinutes", "15");
            b.UseSetting("Jwt:RefreshTokenExpiryDays", "7");
            b.UseSetting("RateLimit:PermitLimit", "1000"); // keep rate limiter out of the way
            b.UseSetting("RateLimit:WindowSeconds", "60");
        });

        _anon = _factory.CreateClient();

        // Register a unique test player
        var registerResp = await _anon.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"IntUser{_sfx}", $"inttest-{_sfx}@example.com", "Password123!"));
        registerResp.EnsureSuccessStatusCode();

        // Login to get a JWT
        var loginResp = await _anon.PostAsJsonAsync("/api/auth/login",
            new LoginRequest($"inttest-{_sfx}@example.com", "Password123!"));
        loginResp.EnsureSuccessStatusCode();

        var auth = (await loginResp.Content.ReadFromJsonAsync<AuthResponse>())!;
        _testPlayerId = auth.Player.Id;

        _authed = _factory.CreateClient();
        _authed.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Grab the first available class from the real DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _testClassId = (await db.CharacterClasses.FirstAsync()).Id;
    }

    public async Task DisposeAsync()
    {
        // Deleting the player cascades to characters + refresh_tokens in PostgreSQL
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var player = await db.Players.FindAsync(_testPlayerId);
        if (player != null)
        {
            db.Players.Remove(player);
            await db.SaveChangesAsync();
        }
        await _factory.DisposeAsync();
    }

    // Helper — creates a character via the real HTTP API
    private async Task<CharacterSummaryDto> CreateCharacterAsync(string name)
    {
        var resp = await _authed.PostAsJsonAsync("/api/characters",
            new CreateCharacterRequest(name, _testClassId, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CharacterSummaryDto>())!;
    }

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task UnusedValidName_Returns200_Available()
    {
        var name = $"Hero{_sfx}";
        var resp = await _anon.GetAsync($"/api/characters/validate-name?name={name}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = (await resp.Content.ReadFromJsonAsync<NameValidationResponse>())!;
        Assert.True(body.Available);
        Assert.Null(body.Reason);
    }

    // ── character creation flow ───────────────────────────────────────────────

    [Fact]
    public async Task AfterCharacterCreated_NameIsUnavailable()
    {
        var name = $"Fighter{_sfx}";

        // Create the character through the real API
        var character = await CreateCharacterAsync(name);
        Assert.NotEqual(Guid.Empty, character.Id);

        // Now the name must be reported as taken
        var resp = await _anon.GetAsync($"/api/characters/validate-name?name={name}");
        var body = (await resp.Content.ReadFromJsonAsync<NameValidationResponse>())!;

        Assert.False(body.Available);
        Assert.Contains("already taken", body.Reason);
    }

    // ── soft-deleted character releases its name ──────────────────────────────

    [Fact]
    public async Task SoftDeletedCharacter_NameBecomesAvailable()
    {
        var name = $"Ranger{_sfx}";

        var character = await CreateCharacterAsync(name);

        // Soft-delete via the real DELETE endpoint
        var delResp = await _authed.DeleteAsync($"/api/characters/{character.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delResp.StatusCode);

        // Name must now be free
        var resp = await _anon.GetAsync($"/api/characters/validate-name?name={name}");
        var body = (await resp.Content.ReadFromJsonAsync<NameValidationResponse>())!;

        Assert.True(body.Available);
    }

    // ── case-insensitive duplicate detection ──────────────────────────────────

    [Fact]
    public async Task ExistingName_DifferentCase_IsUnavailable()
    {
        var name = $"Paladin{_sfx}";
        await CreateCharacterAsync(name);

        // Try lowercase variant
        var resp = await _anon.GetAsync($"/api/characters/validate-name?name={name.ToLower()}");
        var body = (await resp.Content.ReadFromJsonAsync<NameValidationResponse>())!;

        Assert.False(body.Available);
        Assert.Contains("already taken", body.Reason);
    }

    [Fact]
    public async Task ExistingName_UpperCase_IsUnavailable()
    {
        var name = $"Mage{_sfx}";
        await CreateCharacterAsync(name);

        var resp = await _anon.GetAsync($"/api/characters/validate-name?name={name.ToUpper()}");
        var body = (await resp.Content.ReadFromJsonAsync<NameValidationResponse>())!;

        Assert.False(body.Available);
    }

    // ── format validation over real HTTP ─────────────────────────────────────

    [Fact]
    public async Task TooShort_Returns200_Unavailable()
    {
        var resp = await _anon.GetAsync("/api/characters/validate-name?name=A");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = (await resp.Content.ReadFromJsonAsync<NameValidationResponse>())!;
        Assert.False(body.Available);
        Assert.Contains("2 and 32", body.Reason);
    }

    [Fact]
    public async Task TooLong_Returns200_Unavailable()
    {
        var resp = await _anon.GetAsync($"/api/characters/validate-name?name={new string('A', 33)}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = (await resp.Content.ReadFromJsonAsync<NameValidationResponse>())!;
        Assert.False(body.Available);
        Assert.Contains("2 and 32", body.Reason);
    }

    [Fact]
    public async Task StartsWithDigit_Returns200_Unavailable()
    {
        var resp = await _anon.GetAsync("/api/characters/validate-name?name=9Thief");
        var body = (await resp.Content.ReadFromJsonAsync<NameValidationResponse>())!;
        Assert.False(body.Available);
        Assert.Contains("start with a letter", body.Reason);
    }

    [Fact]
    public async Task NameWithSpace_Returns200_Unavailable()
    {
        // URL-encode the space so it reaches the endpoint as part of the name
        var resp = await _anon.GetAsync("/api/characters/validate-name?name=Dark%20Knight");
        var body = (await resp.Content.ReadFromJsonAsync<NameValidationResponse>())!;
        Assert.False(body.Available);
    }

    [Fact]
    public async Task MissingNameParam_Returns400()
    {
        var resp = await _anon.GetAsync("/api/characters/validate-name");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
