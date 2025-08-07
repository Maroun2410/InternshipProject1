// Controllers/LandController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InternshipProject1.Data;
using InternshipProject1.Models;
using InternshipProject1.Dtos;

namespace InternshipProject1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LandController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LandController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<LandDto>>> GetLands()
        {
            var lands = await _context.Lands.ToListAsync();

            var landDtos = lands.Select(l => new LandDto
            {
                Id = l.Id,
                Name = l.Name,
                AreaInHectares = l.AreaInHectares,
                Location = l.Location,
                TreeSpeciesId = l.TreeSpeciesId,
                TotalTrees = l.TotalTrees,
                PlantingDate = l.PlantingDate
            });

            return Ok(landDtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<LandDto>> GetLand(int id)
        {
            var land = await _context.Lands.FindAsync(id);
            if (land == null)
                return NotFound();

            var dto = new LandDto
            {
                Id = land.Id,
                Name = land.Name,
                AreaInHectares = land.AreaInHectares,
                Location = land.Location,
                TreeSpeciesId = land.TreeSpeciesId,
                TotalTrees = land.TotalTrees,
                PlantingDate = land.PlantingDate
            };

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<LandDto>> PostLand(LandCreateDto landDto)
        {
            var land = new Land
            {
                Name = landDto.Name,
                AreaInHectares = landDto.AreaInHectares,
                Location = landDto.Location,
                TreeSpeciesId = landDto.TreeSpeciesId,
                TotalTrees = landDto.TotalTrees,
                PlantingDate = landDto.PlantingDate
            };

            _context.Lands.Add(land);
            await _context.SaveChangesAsync();

            var resultDto = new LandDto
            {
                Id = land.Id,
                Name = land.Name,
                AreaInHectares = land.AreaInHectares,
                Location = land.Location,
                TreeSpeciesId = land.TreeSpeciesId,
                TotalTrees = land.TotalTrees,
                PlantingDate = land.PlantingDate
            };

            return CreatedAtAction(nameof(GetLand), new { id = land.Id }, resultDto);
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateLand(int id, LandDto dto)
        {
            if (id != dto.Id)
                return BadRequest("ID mismatch.");

            var existing = await _context.Lands.FindAsync(id);
            if (existing == null)
                return NotFound();

            existing.Name = dto.Name;
            existing.AreaInHectares = dto.AreaInHectares;
            existing.Location = dto.Location;
            existing.TreeSpeciesId = dto.TreeSpeciesId;
            existing.TotalTrees = dto.TotalTrees;
            existing.PlantingDate = dto.PlantingDate;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLand(int id)
        {
            var land = await _context.Lands.FindAsync(id);
            if (land == null)
                return NotFound();

            _context.Lands.Remove(land);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
