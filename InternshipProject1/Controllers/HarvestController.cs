using InternshipProject1.Data;
using InternshipProject1.DTOs;
using InternshipProject1.Mappers;
using InternshipProject1.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InternshipProject1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HarvestController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public HarvestController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Harvest
        [HttpGet]
        public async Task<IActionResult> GetHarvests()
        {
            // Returns a list of all harvests.
            var harvests = await _context.Harvests.ToListAsync();
            return Ok(harvests);
        }

        // GET: api/Harvest/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Harvest>> GetHarvest(int id)
        {
            // Finds a harvest by its ID.
            // The Include for Inventories has been removed.
            var harvest = await _context.Harvests.FirstOrDefaultAsync(h => h.Id == id);

            if (harvest == null)
            {
                return NotFound();
            }

            // Returns the harvest object directly.
            // The mapping to a DTO has been removed for simplicity.
            return Ok(harvest);
        }

        // POST: api/Harvest
        [HttpPost]
        public async Task<ActionResult<HarvestResponseDto>> PostHarvest(HarvestCreateDto harvestDto)
        {
            var harvest = new Harvest
            {
                LandId = harvestDto.LandId,
                Date = harvestDto.Date,
                Quantity = harvestDto.Quantity,
                UnitQuantity = harvestDto.UnitQuantity,
                Notes = harvestDto.Notes
            };

            _context.Harvests.Add(harvest);
            await _context.SaveChangesAsync();

            var responseDto = new HarvestResponseDto
            {
                Id = harvest.Id,
                LandId = harvest.LandId,
                Date = harvest.Date,
                Quantity = harvest.Quantity,
                UnitQuantity = harvest.UnitQuantity,
                Notes = harvest.Notes
            };

            return CreatedAtAction(nameof(GetHarvest), new { id = harvest.Id }, responseDto);
        }




        // PUT: api/Harvest/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateHarvest(int id, [FromBody] HarvestCreateDto harvestDto)
        {
            var harvest = await _context.Harvests.FindAsync(id);
            if (harvest == null)
            {
                return NotFound();
            }

            // Update harvest properties from the DTO.
            harvest.LandId = harvestDto.LandId;
            harvest.Date = harvestDto.Date;
            harvest.Quantity = harvestDto.Quantity;
            harvest.UnitQuantity = harvestDto.UnitQuantity;
            harvest.Notes = harvestDto.Notes;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/Harvest/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHarvest(int id)
        {
            var harvest = await _context.Harvests.FindAsync(id);
            if (harvest == null)
            {
                return NotFound();
            }

            _context.Harvests.Remove(harvest);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}