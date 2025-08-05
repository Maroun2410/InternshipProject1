using InternshipProject1.Data;
using InternshipProject1.DTOs;
using InternshipProject1.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InternshipProject1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public InventoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Inventory
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Inventory>>> GetInventories()
        {
            return await _context.Inventories.Include(i => i.Harvest).ToListAsync();
        }

        // GET: api/Inventory/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Inventory>> GetInventory(int id)
        {
            var inventory = await _context.Inventories.Include(i => i.Harvest)
                                                      .FirstOrDefaultAsync(i => i.Id == id);

            if (inventory == null)
                return NotFound();

            return inventory;
        }

        // POST: api/Inventory
        [HttpPost]
        public async Task<ActionResult<Inventory>> PostInventory(InventoryDto dto)
        {
            var harvest = await _context.Harvests.FindAsync(dto.HarvestId);
            if (harvest == null)
                return BadRequest($"Harvest with Id {dto.HarvestId} not found.");

            var inventory = new Inventory
            {
                StoredDate = DateTime.SpecifyKind(dto.StoredDate, DateTimeKind.Utc),
                Quantity = dto.Quantity,
                UnitQuantity = dto.UnitQuantity,
                Status = dto.Status,
                HarvestId = dto.HarvestId,
                Harvest = harvest
            };

            _context.Inventories.Add(inventory);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetInventory), new { id = inventory.Id }, inventory);
        }

        // PUT: api/Inventory/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutInventory(int id, InventoryDto dto)
        {
            var inventory = await _context.Inventories.FindAsync(id);

            if (inventory == null)
                return NotFound($"Inventory with Id {id} not found.");

            // Update fields
            inventory.StoredDate = DateTime.SpecifyKind(dto.StoredDate, DateTimeKind.Utc);
            inventory.Quantity = dto.Quantity;
            inventory.UnitQuantity = dto.UnitQuantity;
            inventory.Status = dto.Status;

            // Optional: Check if HarvestId is being updated
            if (dto.HarvestId != inventory.HarvestId)
            {
                var harvest = await _context.Harvests.FindAsync(dto.HarvestId);
                if (harvest == null)
                    return BadRequest($"Harvest with Id {dto.HarvestId} not found.");

                inventory.HarvestId = dto.HarvestId;
                inventory.Harvest = harvest;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Inventories.Any(e => e.Id == id))
                    return NotFound();
                else
                    throw;
            }

            return NoContent();
        }


        // DELETE: api/Inventory/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInventory(int id)
        {
            var inventory = await _context.Inventories.FindAsync(id);
            if (inventory == null)
                return NotFound();

            _context.Inventories.Remove(inventory);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
