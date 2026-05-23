using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MMORPG.Api.Data;
using MMORPG.Api.DTOs;

namespace MMORPG.Api.Tests.Auth;

public class RateLimitingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RateLimitingTests(WebApplicationFactory<Program> factory)
    {
        // Override config: lower rate limit to 5 req/window and use InMemory DB
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimit:PermitLimit", "5");
            builder.UseSetting("RateLimit:WindowSeconds", "60");
            builder.UseSetting("Jwt:SecretKey", "test-secret-key-that-is-long-enough-for-hmacsha256-256bits!");
            builder.UseSetting("Jwt:Issuer", "test-issuer");
            builder.UseSetting("Jwt:Audience", "test-audience");
            builder.UseSetting("Jwt:AccessTokenExpiryMinutes", "15");
            builder.UseSetting("Jwt:RefreshTokenExpiryDays", "7");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(opts =>
                    opts.UseInMemoryDatabase("RateLimitTestDb"));
            });
        });
    }

    [Fact]
    public async Task Login_Returns429AfterExceedingRateLimit()
    {
        var client = _factory.CreateClient();
        var payload = new LoginRequest("nobody@example.com", "wrongpassword");

        HttpStatusCode? lastStatus = null;
        for (int i = 0; i < 10; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", payload);
            lastStatus = response.StatusCode;
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                break;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
    }

    [Fact]
    public async Task Login_First5RequestsAreNotRateLimited()
    {
        // Use a fresh factory/client so the window counter starts at 0
        var isolatedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(opts =>
                    opts.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            });
        });

        var client = isolatedFactory.CreateClient();
        var payload = new LoginRequest("nobody@example.com", "wrongpassword");

        var statuses = new List<HttpStatusCode>();
        for (int i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", payload);
            statuses.Add(response.StatusCode);
        }

        // All 5 should pass rate limit (may be 401 Unauthorized — that's fine, means the request got through)
        Assert.All(statuses, s => Assert.NotEqual(HttpStatusCode.TooManyRequests, s));
    }
}
