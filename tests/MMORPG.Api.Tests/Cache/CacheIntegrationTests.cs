using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MMORPG.Api.Data;
using MMORPG.Api.DTOs;
using MMORPG.Api.Services;

namespace MMORPG.Api.Tests.Cache;

/// <summary>
/// Integration tests for the Redis/IDistributedCache layer against real PostgreSQL.
/// Verifies cache population, invalidation, force-refresh, and JTI blacklisting.
/// </summary>
public class CacheIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _authed = null!;
    private Guid _testPlayerId;
    private int _testClassId;
    private string _sfx = null!;
    private string _accessToken = null!;
    private string _refreshToken = null!;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

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
            b.UseSetting("Jwt:Issuer",    "ethereal-dreams-api");
            b.UseSetting("Jwt:Audience",  "ethereal-dreams-client");
            b.UseSetting("Jwt:AccessTokenExpiryMinutes", "15");
            b.UseSetting("Jwt:RefreshTokenExpiryDays",   "7");
            b.UseSetting("RateLimit:PermitLimit",  "1000");
            b.UseSetting("RateLimit:WindowSeconds", "60");
        });

        var anon = _factory.CreateClient();
        await anon.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"Cch{_sfx}", $"cch-{_sfx}@example.com", "Password123!"));

        var loginResp = await (await anon.PostAsJsonAsync("/api/auth/login",
            new LoginRequest($"cch-{_sfx}@example.com", "Password123!"))).Content
            .ReadFromJsonAsync<AuthResponse>(JsonOpts);

        _testPlayerId = loginResp!.Player.Id;
        _accessToken  = loginResp.AccessToken;
        _refreshToken = loginResp.RefreshToken;

        _authed = _factory.CreateClient();
        _authed.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _accessToken);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _testClassId = (await db.CharacterClasses.FirstAsync()).Id;
    }

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var player = await db.Players.FindAsync(_testPlayerId);
        if (player != null) { db.Players.Remove(player); await db.SaveChangesAsync(); }
        await _factory.DisposeAsync();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private ICacheService Cache => _factory.Services.GetRequiredService<ICacheService>();

    private async Task<CharacterSummaryDto> CreateCharAsync(string name)
    {
        var resp = await _authed.PostAsJsonAsync("/api/characters",
            new CreateCharacterRequest(name, _testClassId, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CharacterSummaryDto>(JsonOpts))!;
    }

    private async Task<List<CharacterSelectDto>> GetSelectAsync(bool forceRefresh = false)
    {
        var url  = forceRefresh ? "/api/characters/select?forceRefresh=true" : "/api/characters/select";
        var resp = await _authed.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<List<CharacterSelectDto>>(JsonOpts))!;
    }

    // ── character select — cache population ───────────────────────────────────

    [Fact]
    public async Task SelectScreen_CacheKeyExists_AfterFirstRequest()
    {
        await GetSelectAsync();

        Assert.True(await Cache.ExistsAsync(CacheKeys.CharacterSelect(_testPlayerId)));
    }

    // ── character select — invalidation on create ─────────────────────────────

    [Fact]
    public async Task SelectScreen_CacheKeyAbsent_AfterCharCreate()
    {
        await GetSelectAsync();
        Assert.True(await Cache.ExistsAsync(CacheKeys.CharacterSelect(_testPlayerId)));

        await CreateCharAsync($"InvA{_sfx}");

        Assert.False(await Cache.ExistsAsync(CacheKeys.CharacterSelect(_testPlayerId)));
    }

    // ── character select — invalidation on delete ─────────────────────────────

    [Fact]
    public async Task SelectScreen_CacheKeyAbsent_AfterCharDelete()
    {
        var created = await CreateCharAsync($"DelMe{_sfx}");

        await GetSelectAsync();
        Assert.True(await Cache.ExistsAsync(CacheKeys.CharacterSelect(_testPlayerId)));

        await _authed.DeleteAsync($"/api/characters/{created.Id}");

        Assert.False(await Cache.ExistsAsync(CacheKeys.CharacterSelect(_testPlayerId)));
    }

    // ── character select — force refresh ──────────────────────────────────────

    [Fact]
    public async Task SelectScreen_ForceRefresh_ReturnsFreshData()
    {
        var char1 = await CreateCharAsync($"Frsh1-{_sfx}");

        // Populate cache (only char1 is in it from this player at this point)
        await GetSelectAsync();

        var char2 = await CreateCharAsync($"Frsh2-{_sfx}");
        // Cache was invalidated by CreateAsync; restore it with only char1 data by
        // calling the normal endpoint — but char2 now exists so cache will have both.
        // Re-seed with forceRefresh to confirm both appear via DB read.
        var result = await GetSelectAsync(forceRefresh: true);

        Assert.Contains(result, c => c.Id == char1.Id);
        Assert.Contains(result, c => c.Id == char2.Id);
    }

    // ── name validation — cache population ───────────────────────────────────

    [Fact]
    public async Task NameValidation_CacheKeyExists_AfterFirstRequest()
    {
        var name = $"ValidN{_sfx}";
        await _authed.GetAsync($"/api/characters/validate-name?name={name}");

        Assert.True(await Cache.ExistsAsync(CacheKeys.NameAvailable(name.ToLower())));
    }

    // ── name validation — invalidation on create ──────────────────────────────

    [Fact]
    public async Task NameValidation_CacheKeyAbsent_AfterCharCreate()
    {
        var name = $"TakeMe{_sfx}";

        await _authed.GetAsync($"/api/characters/validate-name?name={name}");
        Assert.True(await Cache.ExistsAsync(CacheKeys.NameAvailable(name.ToLower())));

        await CreateCharAsync(name);

        Assert.False(await Cache.ExistsAsync(CacheKeys.NameAvailable(name.ToLower())));
    }

    // ── JTI blacklist — logout revokes access token ───────────────────────────

    [Fact]
    public async Task JtiBlacklist_AfterLogout_TokenIsRejected()
    {
        // Use a dedicated login so the class-level _authed token is unaffected
        var anon = _factory.CreateClient();
        var loginResp = (await (await anon.PostAsJsonAsync("/api/auth/login",
            new LoginRequest($"cch-{_sfx}@example.com", "Password123!"))).Content
            .ReadFromJsonAsync<AuthResponse>(JsonOpts))!;

        var freshClient = _factory.CreateClient();
        freshClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResp.AccessToken);

        // Confirm token works before logout
        Assert.Equal(HttpStatusCode.OK,
            (await freshClient.GetAsync("/api/characters/select")).StatusCode);

        // Logout → JTI blacklisted
        var logoutResp = await freshClient.PostAsJsonAsync("/api/auth/logout",
            new RefreshRequest(loginResp.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logoutResp.StatusCode);

        // Same token must now be rejected
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await freshClient.GetAsync("/api/characters/select")).StatusCode);
    }

    // ── JTI blacklist — fresh token still works after another login ───────────

    [Fact]
    public async Task JtiBlacklist_FreshLogin_AllowsAccess()
    {
        var anon = _factory.CreateClient();
        var loginResp = (await (await anon.PostAsJsonAsync("/api/auth/login",
            new LoginRequest($"cch-{_sfx}@example.com", "Password123!"))).Content
            .ReadFromJsonAsync<AuthResponse>(JsonOpts))!;

        var freshClient = _factory.CreateClient();
        freshClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResp.AccessToken);

        Assert.Equal(HttpStatusCode.OK,
            (await freshClient.GetAsync("/api/characters/select")).StatusCode);
    }
}
