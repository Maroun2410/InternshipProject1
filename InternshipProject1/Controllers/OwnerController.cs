using InternshipProject1.Data;
using InternshipProject1.Dtos;
using InternshipProject1.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace InternshipProject1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OwnerController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public OwnerController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Owner>>> GetOwners()
        {
            return await _context.Owners.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Owner>> GetOwner(Guid id)
        {
            var owner = await _context.Owners.FindAsync(id);

            if (owner == null)
                return NotFound();

            return owner;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOwner([FromBody] OwnerDto dto)
        { 
            var owner = new Owner
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                Address = dto.Address,
                NationalId = dto.NationalId,
                Age = dto.Age,
                Type = dto.Type
            };

            _context.Owners.Add(owner);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOwner), new { id = owner.Id }, owner);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOwner(Guid id, OwnerDto dto)
        {
            var owner = await _context.Owners.FindAsync(id);

            if (owner == null)
                return NotFound();

            owner.Name = dto.Name;
            owner.Email = dto.Email;
            owner.PhoneNumber = dto.PhoneNumber;
            owner.Address = dto.Address;
            owner.NationalId = dto.NationalId;
            owner.Age = dto.Age;
            owner.Type = dto.Type;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOwner(Guid id)
        {
            var owner = await _context.Owners.FindAsync(id);

            if (owner == null)
                return NotFound();

            _context.Owners.Remove(owner);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
