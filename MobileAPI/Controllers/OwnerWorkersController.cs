using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileAPI.Auth;
using MobileAPI.Domain;
using MobileAPI.Services;
using MobileAPI.Workers;

namespace MobileAPI.Controllers;

[ApiController]
[Route("api/owner/workers")]
[Authorize(Policy = "OwnerOnlyWrite")]
public class OwnerWorkersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IConfiguration _cfg;
    private readonly ICurrentUser _current;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly MobileAPI.Email.IEmailSender _email;

    public OwnerWorkersController(
        AppDbContext db,
        ITokenService tokens,
        IConfiguration cfg,
        ICurrentUser current,
        UserManager<ApplicationUser> userManager,
        MobileAPI.Email.IEmailSender email)
    {
        _db = db;
        _tokens = tokens;
        _cfg = cfg;
        _current = current;
        _userManager = userManager;
        _email = email;
    }

    // POST: /api/owner/workers/invite
    [HttpPost("invite")]
    public async Task<ActionResult<InviteWorkerResponse>> InviteWorker([FromBody] InviteWorkerRequest req)
    {
        if (!_current.OwnerId.HasValue) return Forbid();

        var ownerId = _current.OwnerId.Value;
        var expiresDays = req.ExpiresDays ?? 7;

        // Prevent inviting an email that is already a Worker for this owner
        var existingUser = await _userManager.FindByEmailAsync(req.Email);
        if (existingUser != null)
        {
            var roles = await _userManager.GetRolesAsync(existingUser);
            if (roles.Contains("Worker") && existingUser.EmployerOwnerId == ownerId)
                return Conflict(new { message = "This email already belongs to a worker under your account." });
        }

        // Invalidate any prior active invites for this email/owner
        var oldInvites = await _db.WorkerInvites
            .Where(x => x.OwnerId == ownerId && x.Email == req.Email && x.AcceptedAt == null && !x.IsRevoked)
            .ToListAsync();
        foreach (var oi in oldInvites) oi.IsRevoked = true;

        // Create new invite
        var rawToken = _tokens.GenerateSecureToken();
        var tokenHash = _tokens.Sha256(rawToken);

        var invite = new WorkerInvite
        {
            OwnerId = ownerId,
            Email = req.Email,
            FullName = req.FullName,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(expiresDays),
            IsRevoked = false
        };
        _db.WorkerInvites.Add(invite);
        await _db.SaveChangesAsync();

        // Build invite link to accept via API (front-end can deep-link later)
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var inviteUrl = $"{baseUrl}/api/auth/accept-invite?email={Uri.EscapeDataString(req.Email)}&token={Uri.EscapeDataString(rawToken)}";

        // Email the invite
        await _email.SendAsync(req.Email, "You are invited as a Worker",
            $"<p>Hello {System.Net.WebUtility.HtmlEncode(req.FullName ?? req.Email)},</p>" +
            $"<p>You have been invited to join as a Worker.</p>" +
            $"<p>Use this link to accept your invite and set a password:</p>" +
            $"<p><a href=\"{inviteUrl}\">Accept Invite</a></p>" +
            $"<p>This link expires on {invite.ExpiresAt:u} (UTC).</p>");

        // In DEV, include token/link in the response for easy testing
        var provider = _cfg["Email:Provider"] ?? "Dev";
        return Ok(new InviteWorkerResponse(
            invite.Id, req.Email, invite.ExpiresAt,
            provider.Equals("SES", StringComparison.OrdinalIgnoreCase) ? null : inviteUrl,
            provider.Equals("SES", StringComparison.OrdinalIgnoreCase) ? null : rawToken
        ));
    }

    // POST: /api/owner/workers/assign-farms
    [HttpPost("assign-farms")]
    public async Task<IActionResult> AssignFarms([FromBody] AssignFarmsRequest req)
    {
        if (!_current.OwnerId.HasValue) return Forbid();
        var ownerId = _current.OwnerId.Value;

        // Validate worker belongs to this owner
        var worker = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.WorkerUserId);
        if (worker == null) return NotFound(new { message = "Worker user not found." });
        var workerRoles = await _userManager.GetRolesAsync(worker);
        if (!workerRoles.Contains("Worker") || worker.EmployerOwnerId != ownerId)
            return Forbid();

        // Ensure farms belong to owner
        var validFarmIds = await _db.Farms.Where(f => f.OwnerId == ownerId && req.FarmIds.Contains(f.Id))
                                          .Select(f => f.Id).ToListAsync();

        // Remove assignments not in list
        var existing = await _db.WorkerFarmAssignments
            .Where(a => a.OwnerId == ownerId && a.WorkerUserId == req.WorkerUserId)
            .ToListAsync();

        var toRemove = existing.Where(e => !validFarmIds.Contains(e.FarmId)).ToList();
        var toKeep = existing.Select(e => e.FarmId).ToHashSet();

        foreach (var r in toRemove) r.IsDeleted = true; // soft delete

        // Add missing
        foreach (var farmId in validFarmIds.Where(id => !toKeep.Contains(id)))
        {
            _db.WorkerFarmAssignments.Add(new WorkerFarmAssignment
            {
                OwnerId = ownerId,
                WorkerUserId = req.WorkerUserId,
                FarmId = farmId
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Assignments updated." });
    }

    // GET: /api/owner/workers/{workerUserId}/assignments
    [HttpGet("{workerUserId:guid}/assignments")]
    public async Task<IActionResult> GetAssignments([FromRoute] Guid workerUserId)
    {
        if (!_current.OwnerId.HasValue) return Forbid();
        var ownerId = _current.OwnerId.Value;

        var list = await _db.WorkerFarmAssignments
            .Where(a => a.OwnerId == ownerId && a.WorkerUserId == workerUserId && !a.IsDeleted)
            .Select(a => new { a.FarmId })
            .ToListAsync();

        return Ok(list);
    }
}
