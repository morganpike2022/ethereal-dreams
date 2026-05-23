using System.ComponentModel.DataAnnotations;

namespace MMORPG.Api.DTOs;

public record RegisterRequest(
    [Required, MinLength(3), MaxLength(32)] string Username,
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password
);

public record LoginRequest(
    [Required] string Email,
    [Required] string Password
);

public record RefreshRequest(
    [Required] string RefreshToken
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiry,
    PlayerDto Player
);

public record PlayerDto(
    Guid Id,
    string Username,
    string Email,
    bool EmailVerified,
    DateTimeOffset CreatedAt
);
