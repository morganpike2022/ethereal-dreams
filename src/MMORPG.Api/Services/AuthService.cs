using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MMORPG.Api.Data;
using MMORPG.Api.DTOs;
using MMORPG.Api.Models;

namespace MMORPG.Api.Services;

public class AuthService(ApplicationDbContext db, IConfiguration config) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (await db.Players.AnyAsync(p => p.Email == request.Email.ToLower()))
            throw new InvalidOperationException("Email already in use.");

        if (await db.Players.AnyAsync(p => p.Username == request.Username))
            throw new InvalidOperationException("Username already taken.");

        var player = new Player
        {
            Id           = Guid.NewGuid(),
            Username     = request.Username,
            Email        = request.Email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            CreatedAt    = DateTimeOffset.UtcNow,
            UpdatedAt    = DateTimeOffset.UtcNow
        };

        db.Players.Add(player);
        await db.SaveChangesAsync();

        return await IssueTokenPairAsync(player);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Email == request.Email.ToLower() && p.DeletedAt == null)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, player.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        player.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return await IssueTokenPairAsync(player);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken)
    {
        var tokenHash = HashToken(refreshToken);
        var stored = await db.RefreshTokens
            .Include(rt => rt.Player)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        if (!stored.IsActive)
            throw new UnauthorizedAccessException("Refresh token is expired or revoked.");

        stored.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return await IssueTokenPairAsync(stored.Player);
    }

    public async Task RevokeAsync(string refreshToken)
    {
        var tokenHash = HashToken(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);
        if (stored is null) return;

        stored.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task<AuthResponse> IssueTokenPairAsync(Player player)
    {
        var expiryMinutes = config.GetValue<int>("Jwt:AccessTokenExpiryMinutes");
        var expiry = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);
        var accessToken = BuildJwt(player, expiry);

        var rawRefresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshExpiryDays = config.GetValue<int>("Jwt:RefreshTokenExpiryDays");

        db.RefreshTokens.Add(new RefreshToken
        {
            Id         = Guid.NewGuid(),
            PlayerId   = player.Id,
            TokenHash  = HashToken(rawRefresh),
            ExpiresAt  = DateTimeOffset.UtcNow.AddDays(refreshExpiryDays),
            CreatedAt  = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        return new AuthResponse(
            accessToken,
            rawRefresh,
            expiry,
            new PlayerDto(player.Id, player.Username, player.Email, player.EmailVerified, player.CreatedAt)
        );
    }

    private string BuildJwt(Player player, DateTimeOffset expiry)
    {
        var secret = config["Jwt:SecretKey"]!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, player.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, player.Email),
            new Claim("username", player.Username),
            new Claim("is_admin", player.IsAdmin.ToString().ToLower()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer:    config["Jwt:Issuer"],
            audience:  config["Jwt:Audience"],
            claims:    claims,
            expires:   expiry.UtcDateTime,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
