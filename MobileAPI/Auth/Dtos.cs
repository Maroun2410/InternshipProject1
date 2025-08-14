using System.ComponentModel.DataAnnotations;

namespace MobileAPI.Auth;

public record RegisterOwnerRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string Password,
    [Required] string FullName);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    string? Device,
    string? UserAgent);

public record LoginResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string RefreshToken,
    string Role,
    Guid? OwnerId);

public record RefreshRequest([Required] string RefreshToken, string? Device, string? UserAgent);
public record LogoutRequest([Required] string RefreshToken);

public record ForgotPasswordRequest([Required, EmailAddress] string Email);
public record ResetPasswordRequest([Required] Guid UserId, [Required] string Token, [Required, MinLength(6)] string NewPassword);
