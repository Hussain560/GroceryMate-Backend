using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GroceryMateApi.Data;
using GroceryMateApi.Models;

namespace GroceryMateApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SuppliersController : ControllerBase
    {
        private readonly GroceryStoreContext _context;

        public SuppliersController(GroceryStoreContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetSuppliers()
        {
            try
            {
                var suppliers = await _context.Suppliers
                    .OrderBy(s => s.SupplierName)
                    .Select(s => new { 
                        id = s.SupplierID, 
                        name = s.SupplierName,
                        contactName = s.ContactName,
                        phone = s.Phone,
                        email = s.Email,
                        address = s.Address
                    })
                    .ToListAsync();
                
                return Ok(new { success = true, data = suppliers });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Error retrieving suppliers" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> CreateSupplier([FromBody] SupplierRequest request)
        {
            try
            {
                var supplier = new Supplier
                {
                    SupplierName = request.SupplierName,
                    ContactName = request.ContactName,
                    Phone = request.Phone,
                    Email = request.Email,
                    Address = request.Address
                };

                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Supplier created successfully", supplierId = supplier.SupplierID });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Error creating supplier" });
            }
        }
    }

    public class SupplierRequest
    {
        public string SupplierName { get; set; } = "";
        public string? ContactName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
    }
}
