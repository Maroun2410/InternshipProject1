using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.WebUtilities;
using System.Linq;
using System.Net;                 // WebUtility.HtmlEncode
using System.Security.Claims;
using System.Text;               // Base64Url encode/decode
using MobileAPI.Workers;         // AcceptInviteRequest from Workers/Dtos.cs
using IAppEmailSender = MobileAPI.Email.IEmailSender;

namespace MobileAPI.Auth;

[ApiController]
[Route("api/[controller]")] // => api/auth
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IAppEmailSender _email;
    private readonly IConfiguration _cfg;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        AppDbContext db,
        ITokenService tokens,
        IAppEmailSender email,
        IConfiguration cfg)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _db = db;
        _tokens = tokens;
        _email = email;
        _cfg = cfg;
    }

    // Builds an absolute API URL from PublicBaseUrl
    private string BuildApiUrl(string path, string query)
    {
        var baseUrl = _cfg["App:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = $"{Request.Scheme}://{Request.Host}";
        baseUrl = baseUrl.TrimEnd('/');

        var sep = string.IsNullOrEmpty(query) ? "" : (path.Contains('?') ? "&" : "?");
        return $"{baseUrl}{path}{sep}{query}";
    }

    // Backward-compatible safe decode: try Base64Url, else use as-is
    private static string SafeDecodeToken(string input)
    {
        try
        {
            var bytes = WebEncoders.Base64UrlDecode(input);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return input; // if someone passed the raw token, still works
        }
    }

    // Helper: original BuildUrl kept in case you use it elsewhere
    private string BuildUrl(string? configuredBase, string endpointWhenMissing, string query)
    {
        var baseUrl = !string.IsNullOrWhiteSpace(configuredBase)
            ? configuredBase!.TrimEnd()
            : $"{Request.Scheme}://{Request.Host}{endpointWhenMissing}";

        var sep = baseUrl.Contains('?') ? "&" : "?";
        return $"{baseUrl}{sep}{query}";
    }

    // ----------- Owner self-registration -----------
    [HttpPost("register-owner")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterOwner([FromBody] RegisterOwnerRequest req)
    {
        var existing = await _userManager.FindByEmailAsync(req.Email);
        if (existing != null) return Conflict(new { message = "Email already in use." });

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = req.Email,
            UserName = req.Email,
            FullName = req.FullName,
            EmailConfirmed = false,
            IsActive = true
        };

        var createRes = await _userManager.CreateAsync(user, req.Password);
        if (!createRes.Succeeded) return BadRequest(new { errors = createRes.Errors });

        await _userManager.AddToRoleAsync(user, "Owner");

        // Generate + Base64Url-encode the token
        var rawToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));

        // Build a DIRECT API link so the act of clicking flips the DB flag
        var confirmUrl = BuildApiUrl("/api/auth/confirm-email", $"userId={user.Id}&code={encoded}");

        await _email.SendAsync(
            user.Email!,
            "Confirm your email",
            $"<p>Hello {WebUtility.HtmlEncode(user.FullName ?? user.Email)},</p>" +
            $"<p>Please confirm your email by clicking the link below:</p>" +
            $"<p><a href=\"{confirmUrl}\">Confirm Email</a></p>"
        );

        var provider = _cfg["Email:Provider"] ?? "Dev";
        if (!provider.Equals("SES", StringComparison.OrdinalIgnoreCase))
            return Ok(new { message = "Registered (DEV). Use confirmUrl/code.", userId = user.Id, confirmUrl, code = encoded });

        return Ok(new { message = "Registered. Please check your email to confirm your account." });
    }

    // ----------- Confirm email -----------
    [HttpGet("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail([FromQuery] Guid userId, [FromQuery] string? token, [FromQuery] string? code)
    {
        if (userId == Guid.Empty) return BadRequest(new { message = "Invalid user id." });

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return NotFound(new { message = "User not found" });
        if (user.EmailConfirmed) return Ok(new { message = "Email already confirmed.", confirmed = true });

        var incoming = !string.IsNullOrWhiteSpace(code) ? code : token;
        if (string.IsNullOrWhiteSpace(incoming))
            return BadRequest(new { message = "Missing confirmation code." });

        var decoded = SafeDecodeToken(incoming);

        IdentityResult res;
        try
        {
            res = await _userManager.ConfirmEmailAsync(user, decoded);
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(500, new
            {
                message = "Database update failed while confirming email.",
                hint = "If you use Postgres RLS/interceptors, ensure AspNetUsers updates are allowed for this anonymous endpoint.",
                detail = ex.GetBaseException().Message
            });
        }

        if (!res.Succeeded)
            return BadRequest(new
            {
                message = "Invalid or expired confirmation code.",
                errors = res.Errors.Select(e => $"{e.Code}: {e.Description}"),
                hints = new[]
                {
                    "Click the newest email.",
                    "Ensure DataProtection keys are persisted (dpkeys folder).",
                    "Default token lifespan ~2h; request a new email if expired."
                }
            });

        // Re-fetch to prove the flag flipped
        var refreshed = await _userManager.FindByIdAsync(userId.ToString());
        return Ok(new { message = "Email confirmed.", confirmed = refreshed!.EmailConfirmed });
    }

    // ----------- Login -----------
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest req)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user == null) return Unauthorized(new { message = "Invalid credentials" });
        if (!user.IsActive) return Forbid();
        if (!user.EmailConfirmed) return Unauthorized(new { message = "Email not confirmed" });

        var pwdOk = await _signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!pwdOk.Succeeded) return Unauthorized(new { message = "Invalid credentials" });

        var roles = await _userManager.GetRolesAsync(user);

        Guid? ownerId = null;
        if (roles.Contains("Owner")) ownerId = user.Id;
        if (roles.Contains("Worker")) ownerId ??= user.EmployerOwnerId;
        if (!ownerId.HasValue) return Forbid();

        var (access, exp) = await _tokens.CreateAccessTokenAsync(user);

        var refreshRaw = _tokens.GenerateSecureToken();
        var refreshHash = _tokens.Sha256(refreshRaw);
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(14),
            Device = req.Device,
            UserAgent = req.UserAgent,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();

        var primaryRole = roles.Contains("Owner") ? "Owner" :
                          roles.Contains("Worker") ? "Worker" :
                          roles.FirstOrDefault() ?? "Owner";

        return new LoginResponse(access, exp, refreshRaw, primaryRole, ownerId);
    }

    // ----------- Refresh token (rotation + reuse detection) -----------
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshRequest req)
    {
        var hash = _tokens.Sha256(req.RefreshToken);

        var token = await _db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash);

        if (token == null || token.User == null)
            return Unauthorized(new { message = "Invalid refresh token" });

        // Reuse detection
        if (token.RevokedAt != null || !token.IsActive)
        {
            var all = await _db.RefreshTokens
                .Where(t => t.UserId == token.UserId && t.RevokedAt == null)
                .ToListAsync();
            foreach (var t in all) t.RevokedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Unauthorized(new { message = "Invalid or reused refresh token" });
        }

        // Rotate current token
        token.RevokedAt = DateTime.UtcNow;

        var newRaw = _tokens.GenerateSecureToken();
        var newHash = _tokens.Sha256(newRaw);

        var replacement = new RefreshToken
        {
            UserId = token.UserId,
            TokenHash = newHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(14),
            Device = req.Device ?? token.Device,
            UserAgent = req.UserAgent ?? token.UserAgent,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        token.ReplacedByTokenHash = newHash;
        _db.RefreshTokens.Add(replacement);

        var (access, exp) = await _tokens.CreateAccessTokenAsync(token.User);
        await _db.SaveChangesAsync();

        var roles = await _userManager.GetRolesAsync(token.User);
        Guid? ownerId = roles.Contains("Owner") ? token.User.Id
                         : roles.Contains("Worker") ? token.User.EmployerOwnerId
                         : null;

        var primaryRole = roles.Contains("Owner") ? "Owner"
                        : roles.Contains("Worker") ? "Worker"
                        : roles.FirstOrDefault() ?? "Owner";

        return new LoginResponse(access, exp, newRaw, primaryRole, ownerId);
    }

    // ----------- Logout -----------
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest req)
    {
        var hash = _tokens.Sha256(req.RefreshToken);
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var token = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash && x.UserId == userId);
        if (token == null) return NotFound(new { message = "Token not found" });

        token.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Logged out" });
    }

    // ----------- Forgot password -----------
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        // Hide account existence
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user == null)
            return Ok(new { message = "If an account exists, an email has been sent." });

        var raw = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(raw));
        var encCode = WebUtility.UrlEncode(encoded);

        // If ResetPasswordUrl is mistakenly set to the API POST endpoint, override to verify-reset.
        var uiResetBase = _cfg["App:ResetPasswordUrl"];
        var looksLikeApiResetPost = uiResetBase?.Contains("/api/auth/reset-password", StringComparison.OrdinalIgnoreCase) == true;

        var url = (!string.IsNullOrWhiteSpace(uiResetBase) && !looksLikeApiResetPost)
            ? $"{uiResetBase!.TrimEnd('/')}?userId={user.Id}&code={encCode}"
            : BuildApiUrl("/api/auth/verify-reset", $"userId={user.Id}&code={encCode}");

        await _email.SendAsync(
            user.Email!, "Reset your password",
            $"<p>Hello {WebUtility.HtmlEncode(user.FullName ?? user.Email)},</p>" +
            $"<p>Reset your password by clicking the link below:</p>" +
            $"<p><a href=\"{url}\">Reset Password</a></p>");

        return Ok(new { message = "If an account exists, an email has been sent." });
    }

    // Add a GET on /api/auth/reset-password that redirects to verify-reset (catches old emails)
    [HttpGet("reset-password")]
    [AllowAnonymous]
    public IActionResult ResetPasswordGet([FromQuery] Guid userId, [FromQuery] string code)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "Missing parameters." });

        var enc = WebUtility.UrlEncode(code);
        var url = BuildApiUrl("/api/auth/verify-reset", $"userId={userId}&code={enc}");
        return Redirect(url);
    }

    // ----------- Reset password (POST) -----------
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var user = await _userManager.FindByIdAsync(req.UserId.ToString());
        if (user == null) return BadRequest(new { message = "Invalid user" });

        // Accept either raw or Base64Url-encoded token
        var decoded = SafeDecodeToken(req.Token);

        var res = await _userManager.ResetPasswordAsync(user, decoded, req.NewPassword);
        if (!res.Succeeded) return BadRequest(new { message = "Invalid or expired token", errors = res.Errors });

        // Revoke all active refresh tokens
        var tokens = await _db.RefreshTokens.Where(x => x.UserId == user.Id && x.RevokedAt == null).ToListAsync();
        foreach (var t in tokens) t.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Password reset successful." });
    }

    // ----------- Who am I -----------
    [HttpGet("whoami")]
    [Authorize]
    public IActionResult WhoAmI()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ownerId = User.FindFirst("owner_id")?.Value;
        var roles = User.FindAll(ClaimTypes.Role).Select(r => r.Value).ToArray();
        var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToArray();
        return Ok(new { userId, ownerId, roles, claims });
    }

    // ----------- Logout ALL devices/sessions -----------
    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var tokens = await _db.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync();

        foreach (var t in tokens) t.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "All sessions revoked." });
    }

    // ----------- Invite verification & acceptance -----------
    [HttpGet("check-invite")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckInvite([FromQuery] string email, [FromQuery] string token)
    {
        var now = DateTime.UtcNow;
        var hash = _tokens.Sha256(token);

        var inv = await _db.WorkerInvites
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(i =>
                i.Email == email &&
                i.TokenHash == hash &&
                !i.IsRevoked &&
                i.AcceptedAt == null &&
                i.ExpiresAt > now);

        if (inv == null) return NotFound(new { message = "Invalid or expired invite." });

        return Ok(new { email = inv.Email, fullName = inv.FullName, expiresAt = inv.ExpiresAt });
    }

    [HttpPost("accept-invite")]
    [AllowAnonymous]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteRequest req)
    {
        var now = DateTime.UtcNow;
        var hash = _tokens.Sha256(req.Token);

        var inv = await _db.WorkerInvites
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i =>
                i.Email == req.Email &&
                i.TokenHash == hash &&
                !i.IsRevoked &&
                i.AcceptedAt == null &&
                i.ExpiresAt > now);

        if (inv == null) return BadRequest(new { message = "Invalid or expired invite." });

        var existing = await _userManager.FindByEmailAsync(req.Email);
        if (existing != null) return Conflict(new { message = "An account with this email already exists." });

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = req.Email,
            UserName = req.Email,
            FullName = string.IsNullOrWhiteSpace(req.FullName) ? inv.FullName : req.FullName,
            EmailConfirmed = true,
            IsActive = true,
            EmployerOwnerId = inv.OwnerId
        };

        var createRes = await _userManager.CreateAsync(user, req.Password);
        if (!createRes.Succeeded) return BadRequest(new { errors = createRes.Errors });

        await _userManager.AddToRoleAsync(user, "Worker");

        inv.AcceptedAt = now;
        inv.WorkerUserId = user.Id; // if your entity has this column
        await _db.SaveChangesAsync();

        return Ok(new { message = "Invite accepted. You can now log in as a worker." });
    }

    // ----------- Verify reset token (GET from email when no UI) -----------
    [HttpGet("verify-reset")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyReset([FromQuery] Guid userId, [FromQuery] string code)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "Missing parameters." });

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return NotFound(new { message = "User not found." });

        var decoded = SafeDecodeToken(code); // uses your helper

        var provider = _userManager.Options.Tokens.PasswordResetTokenProvider;
        var ok = await _userManager.VerifyUserTokenAsync(user, provider, "ResetPassword", decoded);
        if (!ok)
            return BadRequest(new
            {
                message = "Invalid or expired reset token.",
                hints = new[]
                {
                    "Request a new reset email.",
                    "Ensure server DataProtection keys are persisted.",
                    "Default token lifetime is ~2 hours."
                }
            });

        return Ok(new
        {
            message = "Token valid.",
            userId,
            // The UI should POST this 'code' back to /api/auth/reset-password with the new password
            code
        });
    }
}
