// Controllers/SalesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InternshipProject1.Data;
using InternshipProject1.Models;
using InternshipProject1.Dtos;

namespace InternshipProject1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SalesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Sales
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SaleDto>>> GetSales()
        {
            var sales = await _context.Sales.ToListAsync();

            var dtos = sales.Select(s => new SaleDto
            {
                Id = s.Id,
                InventoryId = s.InventoryId,
                Date = s.Date,
                Quantity = s.Quantity,
                UnitQuantity = s.UnitQuantity,
                UnitPrice = s.UnitPrice,
                BuyerName = s.BuyerName
            });

            return Ok(dtos);
        }

        // GET: api/Sales/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SaleDto>> GetSale(int id)
        {
            var sale = await _context.Sales.FindAsync(id);
            if (sale == null)
                return NotFound();

            var dto = new SaleDto
            {
                Id = sale.Id,
                InventoryId = sale.InventoryId,
                Date = sale.Date,
                Quantity = sale.Quantity,
                UnitQuantity = sale.UnitQuantity,
                UnitPrice = sale.UnitPrice,
                BuyerName = sale.BuyerName
            };

            return Ok(dto);
        }

        // POST: api/Sales
        [HttpPost]
        public async Task<ActionResult<SaleDto>> CreateSale(SaleDto dto)
        {
            var sale = new Sale
            {
                InventoryId = dto.InventoryId,
                Date = dto.Date,
                Quantity = dto.Quantity,
                UnitQuantity = dto.UnitQuantity,
                UnitPrice = dto.UnitPrice,
                BuyerName = dto.BuyerName
            };

            _context.Sales.Add(sale);
            await _context.SaveChangesAsync();

            dto.Id = sale.Id; // assign generated ID

            return CreatedAtAction(nameof(GetSale), new { id = dto.Id }, dto);
        }

        // PUT: api/Sales/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSale(int id, SaleDto dto)
        {
            if (id != dto.Id)
                return BadRequest("ID mismatch.");

            var sale = await _context.Sales.FindAsync(id);
            if (sale == null)
                return NotFound();

            sale.InventoryId = dto.InventoryId;
            sale.Date = dto.Date;
            sale.Quantity = dto.Quantity;
            sale.UnitQuantity = dto.UnitQuantity;
            sale.UnitPrice = dto.UnitPrice;
            sale.BuyerName = dto.BuyerName;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/Sales/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSale(int id)
        {
            var sale = await _context.Sales.FindAsync(id);
            if (sale == null)
                return NotFound();

            _context.Sales.Remove(sale);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
