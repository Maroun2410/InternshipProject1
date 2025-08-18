using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileAPI.Auth;
using MobileAPI.Domain;
using MobileAPI.Services;

namespace MobileAPI.Controllers;

[ApiController]
[Route("api/owner/harvests")]
[Authorize(Policy = "OwnerOnlyWrite")]
public class OwnerHarvestsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public OwnerHarvestsController(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public record HarvestCreateRequest(Guid PlantingId, DateTime Date, decimal QuantityKg, string? Notes);
    public record HarvestUpdateRequest(Guid PlantingId, DateTime Date, decimal QuantityKg, string? Notes);
    public record HarvestResponse(Guid Id, Guid PlantingId, Guid FarmId, string CropName, DateTime Date, decimal QuantityKg, string? Notes, DateTime CreatedAt);

    // GET /api/owner/harvests?farmId=&from=&to=&page=&pageSize=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<HarvestResponse>>> List(
        [FromQuery] Guid? farmId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!_current.OwnerId.HasValue) return Forbid();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = _db.Harvests
            .AsNoTracking()
            .Join(_db.Plantings, h => h.PlantingId, p => p.Id, (h, p) => new { h, p });

        if (farmId.HasValue) q = q.Where(x => x.p.FarmId == farmId.Value);
        if (from.HasValue) q = q.Where(x => x.h.Date >= from.Value.Date);
        if (to.HasValue) q = q.Where(x => x.h.Date <= to.Value.Date);

        var items = await q
            .OrderByDescending(x => x.h.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new HarvestResponse(x.h.Id, x.h.PlantingId, x.p.FarmId, x.p.CropName, x.h.Date, x.h.QuantityKg, x.h.Notes, x.h.CreatedAt))
            .ToListAsync();

        return Ok(items);
    }

    // GET /api/owner/harvests/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<HarvestResponse>> Get(Guid id)
    {
        if (!_current.OwnerId.HasValue) return Forbid();

        var x = await _db.Harvests.AsNoTracking()
            .Join(_db.Plantings, h => h.PlantingId, p => p.Id, (h, p) => new { h, p })
            .FirstOrDefaultAsync(y => y.h.Id == id);

        if (x == null) return NotFound();

        return new HarvestResponse(x.h.Id, x.h.PlantingId, x.p.FarmId, x.p.CropName, x.h.Date, x.h.QuantityKg, x.h.Notes, x.h.CreatedAt);
    }

    // POST /api/owner/harvests
    [HttpPost]
    public async Task<ActionResult<HarvestResponse>> Create([FromBody] HarvestCreateRequest req)
    {
        if (!_current.OwnerId.HasValue) return Forbid();
        if (req.QuantityKg <= 0) return BadRequest(new { message = "QuantityKg must be > 0." });

        var planting = await _db.Plantings.FirstOrDefaultAsync(p => p.Id == req.PlantingId);
        if (planting == null) return BadRequest(new { message = "Planting not found." });

        // optional rule: harvest date should not be before plant date
        if (req.Date.Date < planting.PlantDate.Date)
            return BadRequest(new { message = "Harvest date cannot be before the planting date." });

        var h = new Harvest
        {
            OwnerId = _current.OwnerId!.Value,
            PlantingId = req.PlantingId,
            Date = req.Date.Date,
            QuantityKg = req.QuantityKg,
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim()
        };

        _db.Harvests.Add(h);
        await _db.SaveChangesAsync();

        // build response
        var res = new HarvestResponse(h.Id, h.PlantingId, planting.FarmId, planting.CropName, h.Date, h.QuantityKg, h.Notes, h.CreatedAt);
        return CreatedAtAction(nameof(Get), new { id = h.Id }, res);
    }

    // PUT /api/owner/harvests/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] HarvestUpdateRequest req)
    {
        if (!_current.OwnerId.HasValue) return Forbid();
        if (req.QuantityKg <= 0) return BadRequest(new { message = "QuantityKg must be > 0." });

        var h = await _db.Harvests.FirstOrDefaultAsync(x => x.Id == id);
        if (h == null) return NotFound();

        var planting = await _db.Plantings.FirstOrDefaultAsync(p => p.Id == req.PlantingId);
        if (planting == null) return BadRequest(new { message = "Planting not found." });

        if (req.Date.Date < planting.PlantDate.Date)
            return BadRequest(new { message = "Harvest date cannot be before the planting date." });

        h.PlantingId = req.PlantingId;
        h.Date = req.Date.Date;
        h.QuantityKg = req.QuantityKg;
        h.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
        h.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/owner/harvests/{id} (soft delete)
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!_current.OwnerId.HasValue) return Forbid();

        var h = await _db.Harvests.FirstOrDefaultAsync(x => x.Id == id);
        if (h == null) return NotFound();

        if (!h.IsDeleted)
        {
            h.IsDeleted = true;
            h.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }
}
