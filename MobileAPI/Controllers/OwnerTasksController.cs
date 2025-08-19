using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileAPI.Auth;
using MobileAPI.Domain;
using MobileAPI.Services;
// Alias our enum to avoid conflict with System.Threading.Tasks.TaskStatus
using DomainTaskStatus = MobileAPI.Domain.TaskStatus;

namespace MobileAPI.Controllers;

[ApiController]
[Route("api/owner/tasks")]
[Authorize(Policy = "OwnerOnlyWrite")]
public class OwnerTasksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public OwnerTasksController(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public record TaskCreateRequest(string Title, string? Description, Guid? FarmId, DateTime? DueDate, DomainTaskStatus Status);
    public record TaskUpdateRequest(string Title, string? Description, Guid? FarmId, DateTime? DueDate, DomainTaskStatus Status);
    public record TaskResponse(Guid Id, string Title, string? Description, Guid? FarmId, DateTime? DueDate, DomainTaskStatus Status, DateTime CreatedAt);

    // GET /api/owner/tasks?farmId=&status=&q=&from=&to=&page=&pageSize=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskResponse>>> List(
        [FromQuery] Guid? farmId,
        [FromQuery] DomainTaskStatus? status,
        [FromQuery] string? q,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!_current.OwnerId.HasValue) return Forbid();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        // Normalize filter boundaries to UTC (Npgsql timestamptz requires UTC)
        DateTime? fromUtc = from.HasValue ? DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc) : null;
        DateTime? toUtc = to.HasValue ? DateTime.SpecifyKind(to.Value.Date, DateTimeKind.Utc) : null;

        var query = _db.Tasks.AsNoTracking();

        if (farmId.HasValue) query = query.Where(t => t.FarmId == farmId.Value);
        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(t => EF.Functions.ILike(t.Title, $"%{q}%") ||
                                     EF.Functions.ILike(t.Description ?? "", $"%{q}%"));
        if (fromUtc.HasValue) query = query.Where(t => t.DueDate == null || t.DueDate >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(t => t.DueDate == null || t.DueDate <= toUtc.Value);

        var items = await query
            .OrderBy(t => t.DueDate ?? DateTime.MaxValue)
            .ThenBy(t => t.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TaskResponse(t.Id, t.Title, t.Description, t.FarmId, t.DueDate, t.Status, t.CreatedAt))
            .ToListAsync();

        return Ok(items);
    }

    // GET /api/owner/tasks/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskResponse>> Get(Guid id)
    {
        if (!_current.OwnerId.HasValue) return Forbid();

        var t = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();

        return new TaskResponse(t.Id, t.Title, t.Description, t.FarmId, t.DueDate, t.Status, t.CreatedAt);
    }

    // POST /api/owner/tasks
    [HttpPost]
    public async Task<ActionResult<TaskResponse>> Create([FromBody] TaskCreateRequest req)
    {
        if (!_current.OwnerId.HasValue) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Title)) return BadRequest(new { message = "Title is required." });

        if (req.FarmId.HasValue)
        {
            var farmOk = await _db.Farms.AnyAsync(f => f.Id == req.FarmId.Value);
            if (!farmOk) return BadRequest(new { message = "Farm not found." });
        }

        // Force UTC for timestamptz
        DateTime? dueUtc = req.DueDate.HasValue
            ? DateTime.SpecifyKind(req.DueDate.Value.Date, DateTimeKind.Utc)
            : null;

        var t = new TaskItem
        {
            OwnerId = _current.OwnerId!.Value,
            Title = req.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            FarmId = req.FarmId,
            DueDate = dueUtc,
            Status = req.Status
        };

        _db.Tasks.Add(t);
        await _db.SaveChangesAsync();

        var res = new TaskResponse(t.Id, t.Title, t.Description, t.FarmId, t.DueDate, t.Status, t.CreatedAt);
        return CreatedAtAction(nameof(Get), new { id = t.Id }, res);
    }

    // PUT /api/owner/tasks/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] TaskUpdateRequest req)
    {
        if (!_current.OwnerId.HasValue) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Title)) return BadRequest(new { message = "Title is required." });

        var t = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();

        if (req.FarmId.HasValue)
        {
            var farmOk = await _db.Farms.AnyAsync(f => f.Id == req.FarmId.Value);
            if (!farmOk) return BadRequest(new { message = "Farm not found." });
        }

        // Force UTC for timestamptz
        DateTime? dueUtc = req.DueDate.HasValue
            ? DateTime.SpecifyKind(req.DueDate.Value.Date, DateTimeKind.Utc)
            : null;

        t.Title = req.Title.Trim();
        t.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        t.FarmId = req.FarmId;
        t.DueDate = dueUtc;
        t.Status = req.Status;
        t.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/owner/tasks/{id} (soft delete)
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!_current.OwnerId.HasValue) return Forbid();

        var t = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();

        if (!t.IsDeleted)
        {
            t.IsDeleted = true;
            t.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }
}
