using System.Security.Claims;

namespace MobileAPI.Auth;

public interface ITokenService
{
    Task<(string AccessToken, DateTime ExpiresAt)> CreateAccessTokenAsync(ApplicationUser user);
    string GenerateSecureToken(int bytes = 64);      // raw refresh token string
    string Sha256(string input);                     // hash for DB storage
}
