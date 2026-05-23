using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MMORPG.Api.Data;
using MMORPG.Api.DTOs;

namespace MMORPG.Api.Tests.Characters;

/// <summary>
/// End-to-end integration tests for GET /api/characters/select against real PostgreSQL.
/// </summary>
public class CharacterSelectIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _authed = null!;
    private Guid _testPlayerId;
    private int _testClassId;
    private string _sfx = null!;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

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
            b.UseSetting("RateLimit:PermitLimit", "1000");
            b.UseSetting("RateLimit:WindowSeconds", "60");
        });

        var anon = _factory.CreateClient();
        await anon.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"Sel{_sfx}", $"sel-{_sfx}@example.com", "Password123!"));
        var login = (await (await anon.PostAsJsonAsync("/api/auth/login",
            new LoginRequest($"sel-{_sfx}@example.com", "Password123!"))).Content
            .ReadFromJsonAsync<AuthResponse>())!;

        _testPlayerId = login.Player.Id;
        _authed = _factory.CreateClient();
        _authed.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

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

    // ── helper ────────────────────────────────────────────────────────────────

    private async Task<CharacterSummaryDto> CreateCharAsync(string name, AppearanceData? appearance = null)
    {
        var resp = await _authed.PostAsJsonAsync("/api/characters",
            new CreateCharacterRequest(name, _testClassId, appearance));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CharacterSummaryDto>())!;
    }

    private async Task<List<CharacterSelectDto>> GetSelectScreenAsync()
    {
        var resp = await _authed.GetAsync("/api/characters/select");
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<List<CharacterSelectDto>>(JsonOpts))!;
    }

    // ── auth ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SelectScreen_Returns401_WhenUnauthenticated()
    {
        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync("/api/characters/select");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── empty account ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SelectScreen_ReturnsEmptyList_ForFreshAccount()
    {
        var result = await GetSelectScreenAsync();
        // Fresh account might have chars from other tests in this class;
        // filter to make the assertion stable across parallel test methods.
        // (Each test in this class shares the same player — that's intentional
        // to match ETH-4's pattern, but empty-account test uses a fresh player.)
        var sfx2 = Guid.NewGuid().ToString("N")[..8];
        var anon  = _factory.CreateClient();
        await anon.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"Fresh{sfx2}", $"fresh-{sfx2}@example.com", "Password123!"));
        var login = (await (await anon.PostAsJsonAsync("/api/auth/login",
            new LoginRequest($"fresh-{sfx2}@example.com", "Password123!"))).Content
            .ReadFromJsonAsync<AuthResponse>())!;

        var freshClient = _factory.CreateClient();
        freshClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var freshResp = await freshClient.GetAsync("/api/characters/select");
        var freshList = (await freshResp.Content.ReadFromJsonAsync<List<CharacterSelectDto>>(JsonOpts))!;
        Assert.Empty(freshList);

        // Clean up extra player
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var p = await db.Players.FindAsync(login.Player.Id);
        if (p != null) { db.Players.Remove(p); await db.SaveChangesAsync(); }
    }

    // ── returns 200 with list ─────────────────────────────────────────────────

    [Fact]
    public async Task SelectScreen_Returns200_WithCharacterList()
    {
        await CreateCharAsync($"SelHero{_sfx}");
        var resp = await _authed.GetAsync("/api/characters/select");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var list = (await resp.Content.ReadFromJsonAsync<List<CharacterSelectDto>>(JsonOpts))!;
        Assert.NotEmpty(list);
    }

    // ── full stat fields present ───────────────────────────────────────────────

    [Fact]
    public async Task SelectScreen_ReturnsAllStatFields()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var charClass = await db.CharacterClasses.FindAsync(_testClassId);

        await CreateCharAsync($"StatChar{_sfx}");
        var result = await GetSelectScreenAsync();
        var c = result.First(x => x.Name == $"StatChar{_sfx}");

        Assert.Equal(charClass!.BaseHp,   c.MaxHp);
        Assert.Equal(charClass.BaseHp,    c.CurrentHp);
        Assert.Equal(charClass.BaseMana,  c.MaxMana);
        Assert.Equal(charClass.BaseMana,  c.CurrentMana);
        Assert.Equal(charClass.BaseStrength,     c.Attributes.Strength);
        Assert.Equal(charClass.BaseAgility,      c.Attributes.Agility);
        Assert.Equal(charClass.BaseIntelligence, c.Attributes.Intelligence);
        Assert.Equal(charClass.BaseEndurance,    c.Attributes.Endurance);
        Assert.Equal(charClass.BaseSpirit,       c.Attributes.Spirit);
    }

    // ── soft-deleted character disappears immediately ─────────────────────────

    [Fact]
    public async Task SelectScreen_ExcludesSoftDeletedChars()
    {
        var kept    = await CreateCharAsync($"Kept{_sfx}");
        var deleted = await CreateCharAsync($"Gone{_sfx}");

        var delResp = await _authed.DeleteAsync($"/api/characters/{deleted.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delResp.StatusCode);

        var result = await GetSelectScreenAsync();
        Assert.DoesNotContain(result, c => c.Id == deleted.Id);
        Assert.Contains(result, c => c.Id == kept.Id);
    }

    // ── ordered by createdAt ascending ────────────────────────────────────────

    [Fact]
    public async Task SelectScreen_OrderedByCreatedAtAscending()
    {
        var c1 = await CreateCharAsync($"OrdA{_sfx}");
        var c2 = await CreateCharAsync($"OrdB{_sfx}");
        var c3 = await CreateCharAsync($"OrdC{_sfx}");

        var result = await GetSelectScreenAsync();
        var ordered = result
            .Where(c => c.Name.StartsWith("Ord") && c.Name.EndsWith(_sfx))
            .ToList();

        Assert.Equal(3, ordered.Count);
        Assert.Equal($"OrdA{_sfx}", ordered[0].Name);
        Assert.Equal($"OrdB{_sfx}", ordered[1].Name);
        Assert.Equal($"OrdC{_sfx}", ordered[2].Name);
    }

    // ── appearance data roundtrip ─────────────────────────────────────────────

    [Fact]
    public async Task SelectScreen_AppearanceData_DefaultsToEmptyJson()
    {
        await CreateCharAsync($"Bare{_sfx}");
        var result = await GetSelectScreenAsync();
        var c = result.First(x => x.Name == $"Bare{_sfx}");
        Assert.Equal("{}", c.AppearanceData);
    }

    [Fact]
    public async Task SelectScreen_AppearanceData_RoundtripsFromCreate()
    {
        var appearance = new AppearanceData("dark", "mohawk", "silver", "scarred");
        await CreateCharAsync($"Punk{_sfx}", appearance);

        var result = await GetSelectScreenAsync();
        var c = result.First(x => x.Name == $"Punk{_sfx}");

        var stored = JsonSerializer.Deserialize<AppearanceData>(c.AppearanceData,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(stored);
        Assert.Equal("dark",    stored!.SkinTone);
        Assert.Equal("mohawk",  stored.HairStyle);
        Assert.Equal("silver",  stored.HairColor);
        Assert.Equal("scarred", stored.FaceType);
    }
}
