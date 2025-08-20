using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileAPI.Auth;
using MobileAPI.Domain;
using MobileAPI.Services;
using System.Net;
// alias app email sender to avoid clash with Identity UI
using IAppEmailSender = MobileAPI.Email.IEmailSender;

namespace MobileAPI.Controllers;
[ApiController]
[Route("api/owner/workers/admin")]
[Authorize(Policy = "OwnerOnlyWrite")]
public class OwnerWorkersAdminController : ControllerBase

{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IConfiguration _cfg;
    private readonly ITokenService _tokens;
    private readonly IAppEmailSender _email;

    public OwnerWorkersAdminController(
        AppDbContext db,
        ICurrentUser current,
        IConfiguration cfg,
        ITokenService tokens,
        IAppEmailSender email)
    {
        _db = db;
        _current = current;
        _cfg = cfg;
        _tokens = tokens;
        _email = email;
    }

    // ===== DTOs =====
    public record WorkerSummary(
        Guid Id, string Email, string? FullName, bool IsActive,
        IEnumerable<FarmMini> Farms);

    public record FarmMini(Guid Id, string Name);

    public record InviteSummary(
        Guid Id, string Email, string? FullName,
        DateTime ExpiresAt, bool IsRevoked, DateTime? AcceptedAt, bool IsExpired);

    public record ResendInviteRequest(Guid InviteId);
    public record RevokeInviteRequest(Guid InviteId);
    public record ToggleActiveRequest(Guid WorkerUserId, bool IsActive);

    // ===== Workers =====

    // GET /api/owner/workers
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkerSummary>>> ListWorkers()
    {
        if (!_current.OwnerId.HasValue) return Forbid();
        var ownerId = _current.OwnerId!.Value;

        // find Worker role id
        var workerRoleId = await _db.Roles
            .Where(r => r.Name == "Worker")
            .Select(r => r.Id)
            .FirstAsync();

        // users who have Worker role and are linked to this owner
        var workers = await _db.Users
            .Where(u => u.EmployerOwnerId == ownerId)
            .Join(_db.UserRoles.Where(ur => ur.RoleId == workerRoleId),
                u => u.Id, ur => ur.UserId, (u, ur) => u)
            .Select(u => new WorkerSummary(
                u.Id,
                u.Email!,
                u.FullName,
                u.IsActive,
                _db.WorkerFarmAssignments
                    .Where(a => a.WorkerUserId == u.Id && !a.IsDeleted)
                    .Join(_db.Farms, a => a.FarmId, f => f.Id, (a, f) => new FarmMini(f.Id, f.Name))
                    .AsEnumerable()))
            .AsNoTracking()
            .ToListAsync();

        return Ok(workers);
    }

    // POST /api/owner/workers/active
    [HttpPost("active")]
    public async Task<IActionResult> ToggleActive([FromBody] ToggleActiveRequest req)
    {
        if (!_current.OwnerId.HasValue) return Forbid();

        var u = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == req.WorkerUserId && x.EmployerOwnerId == _current.OwnerId!.Value);

        if (u == null) return NotFound(new { message = "Worker not found." });

        u.IsActive = req.IsActive;
        await _db.SaveChangesAsync();
        return NoContent();
    }


    // ===== Invites =====

    // GET /api/owner/workers/invites
    [HttpGet("invites")]
    public async Task<ActionResult<IEnumerable<InviteSummary>>> ListInvites()
    {
        if (!_current.OwnerId.HasValue) return Forbid();
        var ownerId = _current.OwnerId!.Value;

        var list = await _db.WorkerInvites.AsNoTracking()
            .Where(i => i.OwnerId == ownerId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InviteSummary(
                i.Id, i.Email, i.FullName,
                i.ExpiresAt, i.IsRevoked, i.AcceptedAt,
                i.ExpiresAt <= DateTime.UtcNow))
            .ToListAsync();

        return Ok(list);
    }

    // POST /api/owner/workers/invites/revoke
    [HttpPost("invites/revoke")]
    public async Task<IActionResult> RevokeInvite([FromBody] RevokeInviteRequest req)
    {
        if (!_current.OwnerId.HasValue) return Forbid();
        var ownerId = _current.OwnerId!.Value;

        var inv = await _db.WorkerInvites.FirstOrDefaultAsync(i => i.Id == req.InviteId && i.OwnerId == ownerId);
        if (inv == null) return NotFound(new { message = "Invite not found." });

        if (!inv.IsRevoked && inv.AcceptedAt == null)
        {
            inv.IsRevoked = true;
            inv.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return NoContent();
    }

    // POST /api/owner/workers/invites/resend
    // Rotates the token (revokes the old invite, creates a new one) and emails a fresh link.
    [HttpPost("invites/resend")]
    public async Task<IActionResult> ResendInvite([FromBody] ResendInviteRequest req)
    {
        if (!_current.OwnerId.HasValue) return Forbid();
        var ownerId = _current.OwnerId!.Value;

        var old = await _db.WorkerInvites.FirstOrDefaultAsync(i => i.Id == req.InviteId && i.OwnerId == ownerId);
        if (old == null) return NotFound(new { message = "Invite not found." });
        if (old.AcceptedAt != null) return BadRequest(new { message = "Invite already accepted." });

        // revoke old
        old.IsRevoked = true;
        old.UpdatedAt = DateTime.UtcNow;

        // new token
        var raw = _tokens.GenerateSecureToken();
        var hash = _tokens.Sha256(raw);

        var expiresAt = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(7), DateTimeKind.Utc);

        var fresh = new WorkerInvite
        {
            OwnerId = ownerId,
            Email = old.Email,
            FullName = old.FullName,
            TokenHash = hash,
            ExpiresAt = expiresAt
        };
        _db.WorkerInvites.Add(fresh);
        await _db.SaveChangesAsync();

        // Email link
        var acceptBase = _cfg["App:AcceptInviteUrl"] ?? "https://localhost:5001/accept-invite";
        var url = $"{acceptBase}?email={WebUtility.UrlEncode(old.Email)}&token={WebUtility.UrlEncode(raw)}";

        await _email.SendAsync(old.Email, "Your invitation to join the farm",
            $"<p>Hello {(WebUtility.HtmlEncode(old.FullName ?? old.Email))},</p>" +
            $"<p>Your invitation was renewed. Click the link to accept:</p>" +
            $"<p><a href=\"{url}\">Accept Invitation</a> (valid 7 days)</p>");

        return Ok(new
        {
            message = "Invite resent.",
            // In DEV we also return the link to make testing easy
            acceptUrl = url
        });
    }
}
