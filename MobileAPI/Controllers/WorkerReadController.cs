using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileAPI.Auth;
using MobileAPI.Domain;
using MobileAPI.Services;
using System.Security.Claims;

namespace MobileAPI.Controllers;

[ApiController]
[Route("api/worker")]
[Authorize(Policy = "OwnerOrWorkerRead")] // Owners can also call these
public class WorkerReadController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public WorkerReadController(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<List<Guid>> GetAssignedFarmIdsAsync()
    {
        // Only applies to Workers; Owners can see everything for their OwnerId (global filter already applied)
        if (_current.IsOwner) return await _db.Farms.Select(f => f.Id).ToListAsync();

        return await _db.WorkerFarmAssignments
            .Where(a => !a.IsDeleted && a.WorkerUserId == CurrentUserId)
            .Select(a => a.FarmId)
            .ToListAsync();
    }

    // GET /api/worker/equipment
    // Rule: workers can see all equipment for the Owner (owner-wide)
    [HttpGet("equipment")]
    public async Task<IActionResult> GetEquipment()
    {
        var data = await _db.Equipment
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .Select(e => new { e.Id, e.Name, e.Status, e.FarmId })
            .ToListAsync();
        return Ok(data);
    }

    // GET /api/worker/plantings?farmId=...
    [HttpGet("plantings")]
    public async Task<IActionResult> GetPlantings([FromQuery] Guid? farmId)
    {
        var allowed = await GetAssignedFarmIdsAsync();
        if (_current.IsWorker && allowed.Count == 0)
            return Ok(Array.Empty<object>());

        var query = _db.Plantings.AsNoTracking();
        if (_current.IsWorker) query = query.Where(p => allowed.Contains(p.FarmId));
        if (farmId.HasValue) query = query.Where(p => p.FarmId == farmId.Value);

        var data = await query
            .OrderByDescending(p => p.PlantDate)
            .Select(p => new { p.Id, p.FarmId, p.CropName, p.PlantDate, p.ExpectedHarvestDate })
            .ToListAsync();
        return Ok(data);
    }

    // GET /api/worker/harvests?farmId=...
    [HttpGet("harvests")]
    public async Task<IActionResult> GetHarvests([FromQuery] Guid? farmId)
    {
        var allowed = await GetAssignedFarmIdsAsync();
        if (_current.IsWorker && allowed.Count == 0)
            return Ok(Array.Empty<object>());

        // Join Planting to enforce farm restriction
        var query = _db.Harvests
            .AsNoTracking()
            .Join(_db.Plantings, h => h.PlantingId, p => p.Id, (h, p) => new { h, p });

        if (_current.IsWorker) query = query.Where(x => allowed.Contains(x.p.FarmId));
        if (farmId.HasValue) query = query.Where(x => x.p.FarmId == farmId.Value);

        var data = await query
            .OrderByDescending(x => x.h.Date)
            .Select(x => new { x.h.Id, x.p.FarmId, x.p.CropName, x.h.Date, x.h.QuantityKg, x.h.Notes })
            .ToListAsync();
        return Ok(data);
    }

    // GET /api/worker/tasks?farmId=...
    [HttpGet("tasks")]
    public async Task<IActionResult> GetTasks([FromQuery] Guid? farmId)
    {
        var allowed = await GetAssignedFarmIdsAsync();
        if (_current.IsWorker && allowed.Count == 0)
            return Ok(Array.Empty<object>());

        var query = _db.Tasks.AsNoTracking();
        if (_current.IsWorker) query = query.Where(t => t.FarmId == null || allowed.Contains(t.FarmId.Value));
        if (farmId.HasValue) query = query.Where(t => t.FarmId == farmId.Value);

        var data = await query
            .OrderBy(t => t.DueDate)
            .Select(t => new { t.Id, t.Title, t.Description, t.FarmId, t.DueDate, t.Status })
            .ToListAsync();
        return Ok(data);
    }
}
