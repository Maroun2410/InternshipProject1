using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net;                 // WebUtility.HtmlEncode
using System.Security.Claims;
using IAppEmailSender = MobileAPI.Email.IEmailSender;

namespace MobileAPI.Auth;

[ApiController]
[Route("api/[controller]")]
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
        IAppEmailSender email,   // use our alias type
        IConfiguration cfg)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _db = db;
        _tokens = tokens;
        _email = email;          // assign to the field named _email
        _cfg = cfg;
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

        // Assign Owner role
        await _userManager.AddToRoleAsync(user, "Owner");

        // Build confirmation link (use current request's host/port)
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var baseUrl = $"{Request.Scheme}://{Request.Host}"; // e.g., https://localhost:7106
        var url = $"{baseUrl}/api/auth/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";

        // Send email (DevEmailSender logs to console; SES sends real email)
        await _email.SendAsync(
            user.Email!,
            "Confirm your email",
            $"<p>Hello {WebUtility.HtmlEncode(user.FullName ?? user.Email)},</p>" +
            $"<p>Please confirm your email by clicking the link below:</p>" +
            $"<p><a href=\"{url}\">Confirm Email</a></p>"
        );

        // In DEV, return the link and raw token to make testing easy
        var provider = _cfg["Email:Provider"] ?? "Dev";
        if (!provider.Equals("SES", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new
            {
                message = "Registered (DEV). Use confirmUrl or token to confirm.",
                userId = user.Id,
                confirmUrl = url,
                token
            });
        }

        // In SES mode (prod), only return a generic message
        return Ok(new { message = "Registered. Please check your email to confirm your account." });
    }


    // ----------- Confirm email -----------
    [HttpGet("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail([FromQuery] Guid userId, [FromQuery] string token)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return NotFound(new { message = "User not found" });

        var res = await _userManager.ConfirmEmailAsync(user, token);
        if (!res.Succeeded) return BadRequest(new { message = "Invalid or expired token", errors = res.Errors });

        return Ok(new { message = "Email confirmed." });
    }

    // ----------- Login (Owner or Worker) -----------
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

        // If Worker, must have EmployerOwnerId
        Guid? ownerId = null;
        if (roles.Contains("Owner")) ownerId = user.Id;
        if (roles.Contains("Worker")) ownerId ??= user.EmployerOwnerId;
        if (!ownerId.HasValue) return Forbid();

        // Access token
        var (access, exp) = await _tokens.CreateAccessTokenAsync(user);

        // Refresh token (rotate per login)
        var refreshRaw = _tokens.GenerateSecureToken();
        var refreshHash = _tokens.Sha256(refreshRaw);
        var refresh = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(14),
            Device = req.Device,
            UserAgent = req.UserAgent,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync();

        var primaryRole = roles.Contains("Owner") ? "Owner" :
                          roles.Contains("Worker") ? "Worker" :
                          roles.FirstOrDefault() ?? "Owner";

        return new LoginResponse(access, exp, refreshRaw, primaryRole, ownerId);
    }

    // ----------- Refresh token (rotation) -----------
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshRequest req)
    {
        var hash = _tokens.Sha256(req.RefreshToken);
        var token = await _db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash);

        if (token == null || !token.IsActive || token.User == null)
            return Unauthorized(new { message = "Invalid refresh token" });

        // Rotate: revoke old, issue new
        token.RevokedAt = DateTime.UtcNow;

        var newRaw = _tokens.GenerateSecureToken();
        var newHash = _tokens.Sha256(newRaw);
        var newDb = new RefreshToken
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

        _db.RefreshTokens.Add(newDb);

        var (access, exp) = await _tokens.CreateAccessTokenAsync(token.User);
        await _db.SaveChangesAsync();

        var roles = await _userManager.GetRolesAsync(token.User);
        Guid? ownerId = roles.Contains("Owner") ? token.User.Id
                         : roles.Contains("Worker") ? token.User.EmployerOwnerId
                         : null;

        var primaryRole = roles.Contains("Owner") ? "Owner" :
                          roles.Contains("Worker") ? "Worker" :
                          roles.FirstOrDefault() ?? "Owner";

        return new LoginResponse(access, exp, newRaw, primaryRole, ownerId);
    }

    // ----------- Logout (revoke one refresh token) -----------
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

    // ----------- Forgot password (email link) -----------
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        // Do not reveal if user does not exist
        if (user == null) return Ok(new { message = "If an account exists, an email has been sent." });

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var url = $"{baseUrl}/api/auth/reset-password?userId={user.Id}&token={Uri.EscapeDataString(token)}";

        await _email.SendAsync(user.Email!, "Reset your password",
            $"<p>Hello {WebUtility.HtmlEncode(user.FullName ?? user.Email)},</p>" +
            $"<p>Reset your password by clicking the link below:</p>" +
            $"<p><a href=\"{url}\">Reset Password</a></p>");

        return Ok(new { message = "If an account exists, an email has been sent." });
    }

    // ----------- Reset password -----------
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var user = await _userManager.FindByIdAsync(req.UserId.ToString());
        if (user == null) return BadRequest(new { message = "Invalid user" });

        var res = await _userManager.ResetPasswordAsync(user, req.Token, req.NewPassword);
        if (!res.Succeeded) return BadRequest(new { message = "Invalid or expired token", errors = res.Errors });

        // Optional: revoke all refresh tokens for this user
        var tokens = await _db.RefreshTokens.Where(x => x.UserId == user.Id && x.RevokedAt == null).ToListAsync();
        foreach (var t in tokens) t.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Password reset successful." });
    }
}
