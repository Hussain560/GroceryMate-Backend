using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GroceryMateApi.Data;
using GroceryMateApi.Models;

namespace GroceryMateApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BrandsController : ControllerBase
    {
        private readonly GroceryStoreContext _context;

        public BrandsController(GroceryStoreContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetBrands()
        {
            try
            {
                var brands = await _context.Brands
                    .OrderBy(b => b.BrandName)
                    .Select(b => new { 
                        id = b.BrandID, 
                        name = b.BrandName,
                        imageUrl = b.ImageUrl
                    })
                    .ToListAsync();
                
                return Ok(new { success = true, data = brands });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Error retrieving brands" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> CreateBrand([FromBody] BrandRequest request)
        {
            try
            {
                var brand = new Brand
                {
                    BrandName = request.BrandName,
                    ImageUrl = request.ImageUrl
                };

                _context.Brands.Add(brand);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Brand created successfully", brandId = brand.BrandID });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Error creating brand" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> UpdateBrand(int id, [FromBody] BrandRequest request)
        {
            try
            {
                var brand = await _context.Brands.FindAsync(id);
                if (brand == null)
                    return NotFound(new { success = false, message = "Brand not found" });

                brand.BrandName = request.BrandName;
                brand.ImageUrl = request.ImageUrl;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Brand updated successfully" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Error updating brand" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> DeleteBrand(int id)
        {
            try
            {
                var brand = await _context.Brands.FindAsync(id);
                if (brand == null)
                    return NotFound(new { success = false, message = "Brand not found" });

                _context.Brands.Remove(brand);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Brand deleted successfully" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Cannot delete brand. It may have related products." });
            }
        }
    }

    public class BrandRequest
    {
        public string BrandName { get; set; } = "";
        public string? ImageUrl { get; set; }
    }
}
