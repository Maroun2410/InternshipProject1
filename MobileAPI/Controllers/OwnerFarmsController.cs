using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileAPI.Auth;
using MobileAPI.Domain;
using MobileAPI.Services;

namespace MobileAPI.Controllers;

[ApiController]
[Route("api/owner/farms")]
[Authorize(Policy = "OwnerOnlyWrite")] // Owner-only for all endpoints in this controller
public class OwnerFarmsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public OwnerFarmsController(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    // DTOs
    public record FarmCreateRequest(string Name, string? LocationText, decimal? AreaHa);
    public record FarmUpdateRequest(string Name, string? LocationText, decimal? AreaHa);
    public record FarmResponse(Guid Id, string Name, string? LocationText, decimal? AreaHa, DateTime CreatedAt);

    // GET /api/owner/farms
    [HttpGet]
    public async Task<ActionResult<IEnumerable<FarmResponse>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? q = null)
    {
        if (!_current.OwnerId.HasValue) return Forbid();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Farms.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(f => EF.Functions.ILike(f.Name, $"%{q}%") || EF.Functions.ILike(f.LocationText ?? "", $"%{q}%"));

        var items = await query
            .OrderBy(f => f.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FarmResponse(f.Id, f.Name, f.LocationText, f.AreaHa ?? 0, f.CreatedAt))
            .ToListAsync();

        return Ok(items);
    }

    // GET /api/owner/farms/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FarmResponse>> Get(Guid id)
    {
        if (!_current.OwnerId.HasValue) return Forbid();

        var farm = await _db.Farms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        if (farm == null) return NotFound();

        return new FarmResponse(farm.Id, farm.Name, farm.LocationText, farm.AreaHa ?? 0, farm.CreatedAt);
    }

    // POST /api/owner/farms
    [HttpPost]
    public async Task<ActionResult<FarmResponse>> Create([FromBody] FarmCreateRequest req)
    {
        if (!_current.OwnerId.HasValue) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { message = "Name is required." });

        var farm = new Farm
        {
            OwnerId = _current.OwnerId!.Value, // also set in SaveChanges, but set explicitly for clarity
            Name = req.Name.Trim(),
            LocationText = string.IsNullOrWhiteSpace(req.LocationText) ? null : req.LocationText.Trim(),
            AreaHa = req.AreaHa
        };

        _db.Farms.Add(farm);
        await _db.SaveChangesAsync();

        var res = new FarmResponse(farm.Id, farm.Name, farm.LocationText, farm.AreaHa ?? 0, farm.CreatedAt);
        return CreatedAtAction(nameof(Get), new { id = farm.Id }, res);
    }

    // PUT /api/owner/farms/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] FarmUpdateRequest req)
    {
        if (!_current.OwnerId.HasValue) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { message = "Name is required." });

        var farm = await _db.Farms.FirstOrDefaultAsync(f => f.Id == id);
        if (farm == null) return NotFound();

        farm.Name = req.Name.Trim();
        farm.LocationText = string.IsNullOrWhiteSpace(req.LocationText) ? null : req.LocationText.Trim();
        farm.AreaHa = req.AreaHa;
        farm.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/owner/farms/{id}  (soft delete)
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!_current.OwnerId.HasValue) return Forbid();

        var farm = await _db.Farms.FirstOrDefaultAsync(f => f.Id == id);
        if (farm == null) return NotFound();

        if (farm.IsDeleted) return NoContent(); // already gone

        farm.IsDeleted = true;
        farm.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
