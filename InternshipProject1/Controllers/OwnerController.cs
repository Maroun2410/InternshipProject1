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
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var email = (dto.Email ?? string.Empty).Trim();
            var phone = (dto.PhoneNumber ?? string.Empty).Trim();
            var nid = (dto.NationalId ?? string.Empty).Trim();

            // Case-insensitive email check
            if (await _context.Owners.AnyAsync(o => o.Email.ToLower() == email.ToLower()))
                return BadRequest(new { Message = "Email already in use." });

            if (await _context.Owners.AnyAsync(o => o.PhoneNumber == phone))
                return BadRequest(new { Message = "Phone number already in use." });

            if (await _context.Owners.AnyAsync(o => o.NationalId == nid))
                return BadRequest(new { Message = "National ID already in use." });

            var owner = new Owner
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Email = email,
                PhoneNumber = phone,
                Address = dto.Address,
                NationalId = nid,
                Age = dto.Age,
                Type = dto.Type
            };

            _context.Owners.Add(owner);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Safety net if something slips past checks (race condition)
                return Conflict(new { Message = "Owner violates a unique constraint." });
            }

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
