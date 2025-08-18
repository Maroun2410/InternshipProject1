using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileAPI.Auth;
using MobileAPI.Domain;
using MobileAPI.Services;

namespace MobileAPI.Controllers;

[ApiController]
[Route("api/owner/plantings")]
[Authorize(Policy = "OwnerOnlyWrite")]
public class OwnerPlantingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public OwnerPlantingsController(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public record PlantingCreateRequest(Guid FarmId, string CropName, DateTime PlantDate, DateTime? ExpectedHarvestDate);
    public record PlantingUpdateRequest(Guid FarmId, string CropName, DateTime PlantDate, DateTime? ExpectedHarvestDate);
    public record PlantingResponse(Guid Id, Guid FarmId, string CropName, DateTime PlantDate, DateTime? ExpectedHarvestDate, DateTime CreatedAt);

    // GET /api/owner/plantings?farmId=&from=&to=&page=&pageSize=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PlantingResponse>>> List(
        [FromQuery] Guid? farmId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!_current.OwnerId.HasValue) return Forbid();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = _db.Plantings.AsNoTracking();

        if (farmId.HasValue) q = q.Where(p => p.FarmId == farmId.Value);
        if (from.HasValue) q = q.Where(p => p.PlantDate >= from.Value.Date);
        if (to.HasValue) q = q.Where(p => p.PlantDate <= to.Value.Date);

        var items = await q
            .OrderByDescending(p => p.PlantDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PlantingResponse(p.Id, p.FarmId, p.CropName, p.PlantDate, p.ExpectedHarvestDate, p.CreatedAt))
            .ToListAsync();

        return Ok(items);
    }

    // GET /api/owner/plantings/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlantingResponse>> Get(Guid id)
    {
        if (!_current.OwnerId.HasValue) return Forbid();

        var p = await _db.Plantings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();

        return new PlantingResponse(p.Id, p.FarmId, p.CropName, p.PlantDate, p.ExpectedHarvestDate, p.CreatedAt);
    }

    // POST /api/owner/plantings
    [HttpPost]
    public async Task<ActionResult<PlantingResponse>> Create([FromBody] PlantingCreateRequest req)
    {
        if (!_current.OwnerId.HasValue) return Forbid();
        if (string.IsNullOrWhiteSpace(req.CropName)) return BadRequest(new { message = "CropName is required." });

        // ensure farm is yours (global filter already scopes by owner)
        var farmExists = await _db.Farms.AnyAsync(f => f.Id == req.FarmId);
        if (!farmExists) return BadRequest(new { message = "Farm not found." });

        var planting = new Planting
        {
            OwnerId = _current.OwnerId!.Value,
            FarmId = req.FarmId,
            CropName = req.CropName.Trim(),
            PlantDate = req.PlantDate.Date,
            ExpectedHarvestDate = req.ExpectedHarvestDate?.Date
        };

        _db.Plantings.Add(planting);
        await _db.SaveChangesAsync();

        var res = new PlantingResponse(planting.Id, planting.FarmId, planting.CropName, planting.PlantDate, planting.ExpectedHarvestDate, planting.CreatedAt);
        return CreatedAtAction(nameof(Get), new { id = planting.Id }, res);
    }

    // PUT /api/owner/plantings/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PlantingUpdateRequest req)
    {
        if (!_current.OwnerId.HasValue) return Forbid();
        if (string.IsNullOrWhiteSpace(req.CropName)) return BadRequest(new { message = "CropName is required." });

        var planting = await _db.Plantings.FirstOrDefaultAsync(x => x.Id == id);
        if (planting == null) return NotFound();

        var farmExists = await _db.Farms.AnyAsync(f => f.Id == req.FarmId);
        if (!farmExists) return BadRequest(new { message = "Farm not found." });

        planting.FarmId = req.FarmId;
        planting.CropName = req.CropName.Trim();
        planting.PlantDate = req.PlantDate.Date;
        planting.ExpectedHarvestDate = req.ExpectedHarvestDate?.Date;
        planting.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/owner/plantings/{id} (soft delete)
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!_current.OwnerId.HasValue) return Forbid();

        var planting = await _db.Plantings.FirstOrDefaultAsync(x => x.Id == id);
        if (planting == null) return NotFound();

        if (!planting.IsDeleted)
        {
            planting.IsDeleted = true;
            planting.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }
}
