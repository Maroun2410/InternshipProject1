using InternshipProject1.Data;
using InternshipProject1.DTOs;
using InternshipProject1.Models;
using InternshipProject1.Mappers;
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
            var harvests = await _context.Harvests.ToListAsync();
            return Ok(harvests);
        }

        // GET: api/Harvest/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<HarvestDTO>> GetHarvest(int id)
        {
            var harvest = await _context.Harvests
                                .Include(h => h.Inventories)
                                .FirstOrDefaultAsync(h => h.Id == id);

            if (harvest == null)
            {
                return NotFound();
            }

            var harvestDto = HarvestMapper.MapHarvestToDTO(harvest);

            return Ok(harvestDto);
        }



        // POST: api/Harvest
        [HttpPost]
        public async Task<IActionResult> CreateHarvest([FromBody] HarvestDTO harvestDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

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

            return CreatedAtAction(nameof(GetHarvest), new { id = harvest.Id }, harvest);
        }

        // PUT: api/Harvest/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateHarvest(int id, [FromBody] HarvestDTO harvestDto)
        {
            var harvest = await _context.Harvests.FindAsync(id);
            if (harvest == null)
                return NotFound();

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
                return NotFound();

            _context.Harvests.Remove(harvest);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
