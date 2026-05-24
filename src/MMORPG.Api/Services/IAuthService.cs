using MMORPG.Api.DTOs;

namespace MMORPG.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshAsync(string refreshToken);
    Task RevokeAsync(string refreshToken);
    Task BlacklistJtiAsync(string jti, DateTimeOffset tokenExpiry);
    Task<bool> IsJtiRevokedAsync(string jti);
}
