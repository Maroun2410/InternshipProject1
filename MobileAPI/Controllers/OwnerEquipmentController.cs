using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileAPI.Auth;
using MobileAPI.Domain;
using MobileAPI.Services;

namespace MobileAPI.Controllers;

[ApiController]
[Route("api/owner/equipment")]
[Authorize(Policy = "OwnerOnlyWrite")]
public class OwnerEquipmentController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public OwnerEquipmentController(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public record EquipmentCreateRequest(string Name, EquipmentStatus Status, Guid? FarmId);
    public record EquipmentUpdateRequest(string Name, EquipmentStatus Status, Guid? FarmId);
    public record EquipmentResponse(Guid Id, string Name, EquipmentStatus Status, Guid? FarmId, DateTime CreatedAt);

    // GET /api/owner/equipment?status=&farmId=&q=&page=&pageSize=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EquipmentResponse>>> List(
        [FromQuery] EquipmentStatus? status,
        [FromQuery] Guid? farmId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!_current.OwnerId.HasValue) return Forbid();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Equipment.AsNoTracking();
        if (status.HasValue) query = query.Where(e => e.Status == status.Value);
        if (farmId.HasValue) query = query.Where(e => e.FarmId == farmId.Value);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(e => EF.Functions.ILike(e.Name, $"%{q}%"));

        var items = await query
            .OrderBy(e => e.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EquipmentResponse(e.Id, e.Name, e.Status, e.FarmId, e.CreatedAt))
            .ToListAsync();

        return Ok(items);
    }

    // GET /api/owner/equipment/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EquipmentResponse>> Get(Guid id)
    {
        if (!_current.OwnerId.HasValue) return Forbid();

        var e = await _db.Equipment.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();

        return new EquipmentResponse(e.Id, e.Name, e.Status, e.FarmId, e.CreatedAt);
    }

    // POST /api/owner/equipment
    [HttpPost]
    public async Task<ActionResult<EquipmentResponse>> Create([FromBody] EquipmentCreateRequest req)
    {
        if (!_current.OwnerId.HasValue) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { message = "Name is required." });

        // if FarmId is provided, ensure it belongs to the owner (global filter already scopes by owner)
        if (req.FarmId.HasValue)
        {
            var farmOk = await _db.Farms.AnyAsync(f => f.Id == req.FarmId.Value);
            if (!farmOk) return BadRequest(new { message = "Farm not found." });
        }

        var e = new Equipment
        {
            OwnerId = _current.OwnerId!.Value,
            Name = req.Name.Trim(),
            Status = req.Status,
            FarmId = req.FarmId
        };

        _db.Equipment.Add(e);
        await _db.SaveChangesAsync();

        var res = new EquipmentResponse(e.Id, e.Name, e.Status, e.FarmId, e.CreatedAt);
        return CreatedAtAction(nameof(Get), new { id = e.Id }, res);
    }

    // PUT /api/owner/equipment/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] EquipmentUpdateRequest req)
    {
        if (!_current.OwnerId.HasValue) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { message = "Name is required." });

        var e = await _db.Equipment.FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();

        if (req.FarmId.HasValue)
        {
            var farmOk = await _db.Farms.AnyAsync(f => f.Id == req.FarmId.Value);
            if (!farmOk) return BadRequest(new { message = "Farm not found." });
        }

        e.Name = req.Name.Trim();
        e.Status = req.Status;
        e.FarmId = req.FarmId;
        e.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/owner/equipment/{id} (soft delete)
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!_current.OwnerId.HasValue) return Forbid();

        var e = await _db.Equipment.FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();

        if (!e.IsDeleted)
        {
            e.IsDeleted = true;
            e.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }
}
