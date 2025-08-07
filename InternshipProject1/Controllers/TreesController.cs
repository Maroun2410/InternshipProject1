using System;
using InternshipProject1.Data;
using InternshipProject1.Models;
using InternshipProject1.Models.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InternshipProject1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TreesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TreesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Trees
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TreeDto>>> GetTrees()
        {
            var trees = await _context.TreeSpecies
                .Select(t => new TreeDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    HarvestMonth = t.HarvestMonth
                })
                .ToListAsync();

            return Ok(trees);
        }

        // GET: api/Trees/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TreeDto>> GetTree(int id)
        {
            var tree = await _context.TreeSpecies.FindAsync(id);

            if (tree == null)
                return NotFound();

            var treeDto = new TreeDto
            {
                Id = tree.Id,
                Name = tree.Name,
                Description = tree.Description,
                HarvestMonth = tree.HarvestMonth
            };

            return Ok(treeDto);
        }

        // POST: api/Trees
        [HttpPost]
        public async Task<ActionResult<TreeDto>> PostTree(TreeCreateDto treeDto)
        {
            var tree = new TreeSpecies
            {
                Name = treeDto.Name,
                Description = treeDto.Description,
                HarvestMonth = treeDto.HarvestMonth
            };

            _context.TreeSpecies.Add(tree);
            await _context.SaveChangesAsync();

            var resultDto = new TreeDto
            {
                Id = tree.Id,
                Name = tree.Name,
                Description = tree.Description,
                HarvestMonth = tree.HarvestMonth
            };

            return CreatedAtAction(nameof(GetTree), new { id = tree.Id }, resultDto);
        }


        // PUT: api/Trees/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTreeSpecies(int id, TreeCreateDto dto)
        {
            var existing = await _context.TreeSpecies.FindAsync(id);
            if (existing == null)
                return NotFound();

            existing.Name = dto.Name;
            existing.Description = dto.Description;
            existing.HarvestMonth = dto.HarvestMonth;

            await _context.SaveChangesAsync();

            return NoContent();
        }


        // DELETE: api/Trees/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTree(int id)
        {
            var tree = await _context.TreeSpecies.FindAsync(id);

            if (tree == null)
                return NotFound();

            _context.TreeSpecies.Remove(tree);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}


