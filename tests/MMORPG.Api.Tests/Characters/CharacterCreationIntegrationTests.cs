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
/// End-to-end integration tests for POST /api/characters against real PostgreSQL.
/// Covers the 3-table transaction, stat seeding, skill seeding, inventory seeding,
/// and the chaos rollback scenario.
/// </summary>
public class CharacterCreationIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _authed = null!;
    private Guid _testPlayerId;
    private int _testClassId;
    private string _sfx = null!;

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
            new RegisterRequest($"Creator{_sfx}", $"creator-{_sfx}@example.com", "Password123!"));

        var loginResp = await anon.PostAsJsonAsync("/api/auth/login",
            new LoginRequest($"creator-{_sfx}@example.com", "Password123!"));
        var auth = (await loginResp.Content.ReadFromJsonAsync<AuthResponse>())!;
        _testPlayerId = auth.Player.Id;

        _authed = _factory.CreateClient();
        _authed.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

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

    // ── happy path: all 3 tables written atomically ───────────────────────────

    [Fact]
    public async Task Create_Returns201_WithCharacterDto()
    {
        var name = $"Hero{_sfx}";
        var resp = await _authed.PostAsJsonAsync("/api/characters",
            new CreateCharacterRequest(name, _testClassId, null));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = (await resp.Content.ReadFromJsonAsync<CharacterSummaryDto>())!;
        Assert.Equal(name, dto.Name);
        Assert.Equal(1, dto.Level);
    }

    [Fact]
    public async Task Create_AllThreeTables_Populated()
    {
        var name = $"Triton{_sfx}";
        var resp = await _authed.PostAsJsonAsync("/api/characters",
            new CreateCharacterRequest(name, _testClassId, null));
        resp.EnsureSuccessStatusCode();
        var dto = (await resp.Content.ReadFromJsonAsync<CharacterSummaryDto>())!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Table 1 — characters
        var character = await db.Characters.FindAsync(dto.Id);
        Assert.NotNull(character);
        Assert.Equal(name, character!.Name);

        // Table 2 — character_skills (starter skill for class)
        var skills = await db.CharacterSkills
            .Where(cs => cs.CharacterId == dto.Id)
            .ToListAsync();
        Assert.NotEmpty(skills);
        Assert.All(skills, s => Assert.Equal(1, s.CurrentRank));

        // Table 3 — inventory (5x Novice Health Potion in slot 0)
        var slot = await db.Inventory.FirstOrDefaultAsync(i => i.CharacterId == dto.Id);
        Assert.NotNull(slot);
        Assert.Equal(0, slot!.SlotIndex);
        Assert.Equal(5, slot.Quantity);
    }

    [Fact]
    public async Task Create_StatsMatchClass()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var charClass = await db.CharacterClasses.FindAsync(_testClassId);

        var resp = await _authed.PostAsJsonAsync("/api/characters",
            new CreateCharacterRequest($"Statman{_sfx}", _testClassId, null));
        var dto = (await resp.Content.ReadFromJsonAsync<CharacterSummaryDto>())!;

        var character = await db.Characters.FindAsync(dto.Id);
        Assert.Equal(charClass!.BaseHp,   character!.MaxHp);
        Assert.Equal(charClass.BaseMana,  character.MaxMana);
        Assert.Equal(charClass.BaseHp,    character.CurrentHp);
        Assert.Equal(charClass.BaseMana,  character.CurrentMana);
    }

    // ── name taken → 409, no orphan ───────────────────────────────────────────

    [Fact]
    public async Task Create_DuplicateName_Returns409()
    {
        var name = $"Dupe{_sfx}";
        var first = await _authed.PostAsJsonAsync("/api/characters",
            new CreateCharacterRequest(name, _testClassId, null));
        first.EnsureSuccessStatusCode();

        var second = await _authed.PostAsJsonAsync("/api/characters",
            new CreateCharacterRequest(name, _testClassId, null));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateName_CaseInsensitive_Returns409()
    {
        var name = $"Cased{_sfx}";
        await _authed.PostAsJsonAsync("/api/characters",
            new CreateCharacterRequest(name, _testClassId, null));

        var resp = await _authed.PostAsJsonAsync("/api/characters",
            new CreateCharacterRequest(name.ToUpper(), _testClassId, null));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    // ── chaos / rollback: duplicate name leaves no orphaned character row ──────

    [Fact]
    public async Task Create_DuplicateNameFailure_LeavesNoOrphanInCharacters()
    {
        var name = $"Orphan{_sfx}";

        // First creation must succeed
        (await _authed.PostAsJsonAsync("/api/characters",
            new CreateCharacterRequest(name, _testClassId, null))).EnsureSuccessStatusCode();

        // Second creation must fail
        await _authed.PostAsJsonAsync("/api/characters",
            new CreateCharacterRequest(name, _testClassId, null));

        // Exactly one character with that name must exist
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count = await db.Characters
            .IgnoreQueryFilters()
            .CountAsync(c => c.PlayerId == _testPlayerId && c.Name == name);

        Assert.Equal(1, count);
    }

    // ── character limit ───────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ExceedingLimit_Returns409()
    {
        // The test player may already have chars from other tests in this class;
        // use a fresh player so the limit is clean.
        var sfx2 = Guid.NewGuid().ToString("N")[..8];
        var anon = _factory.CreateClient();
        await anon.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"Limit{sfx2}", $"limit-{sfx2}@example.com", "Password123!"));
        var login = (await (await anon.PostAsJsonAsync("/api/auth/login",
            new LoginRequest($"limit-{sfx2}@example.com", "Password123!"))).Content
            .ReadFromJsonAsync<AuthResponse>())!;

        var limitClient = _factory.CreateClient();
        limitClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        for (int i = 0; i < 5; i++)
            (await limitClient.PostAsJsonAsync("/api/characters",
                new CreateCharacterRequest($"Slot{i:D2}{sfx2}", _testClassId, null)))
                .EnsureSuccessStatusCode();

        var sixth = await limitClient.PostAsJsonAsync("/api/characters",
            new CreateCharacterRequest($"Slot05{sfx2}", _testClassId, null));
        Assert.Equal(HttpStatusCode.Conflict, sixth.StatusCode);

        // Clean up this extra player
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var player = await db.Players.FindAsync(login.Player.Id);
        if (player != null) { db.Players.Remove(player); await db.SaveChangesAsync(); }
    }

    // ── input validation → 400 ────────────────────────────────────────────────

    [Fact]
    public async Task Create_InvalidClassId_Returns400()
    {
        var resp = await _authed.PostAsJsonAsync("/api/characters",
            new CreateCharacterRequest($"Valid{_sfx}", 99999, null));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Create_NameTooShort_Returns400()
    {
        var resp = await _authed.PostAsJsonAsync("/api/characters",
            new CreateCharacterRequest("A", _testClassId, null));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Create_NameStartsWithDigit_Returns400()
    {
        var resp = await _authed.PostAsJsonAsync("/api/characters",
            new CreateCharacterRequest("1Hero", _testClassId, null));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401()
    {
        var anon = _factory.CreateClient();
        var resp = await anon.PostAsJsonAsync("/api/characters",
            new CreateCharacterRequest($"Ghost{_sfx}", _testClassId, null));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
