using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GroceryMateApi.Data;
using GroceryMateApi.Models;

namespace GroceryMateApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly GroceryStoreContext _context;

        public CategoriesController(GroceryStoreContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _context.Categories
                    .OrderBy(c => c.CategoryName)
                    .Select(c => new { 
                        id = c.CategoryID, 
                        name = c.CategoryName,
                        imagePath = c.ImagePath ?? $"/images/categories/{c.CategoryName.ToLower().Replace(" ", "-")}.jpg"
                    })
                    .ToListAsync();
                
                return Ok(new { success = true, data = categories });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Error retrieving categories" });
            }
        }

        [HttpGet("{id}/products")]
        public async Task<IActionResult> GetCategoryProducts(int id)
        {
            try
            {
                var products = await _context.Products
                    .Include(p => p.Brand)
                    .Where(p => p.CategoryID == id)
                    .OrderBy(p => p.ProductName)
                    .Select(p => new { 
                        productID = p.ProductID,
                        name = p.ProductName, 
                        brand = p.Brand.BrandName,
                        price = p.UnitPrice, 
                        discountPercentage = p.DiscountPercentage,
                        stock = p.StockQuantity,
                        imageUrl = p.ImageUrl ?? "/images/products/default.jpg",
                        barcode = p.Barcode ?? "N/A"
                    })
                    .ToListAsync();
                
                return Ok(new { success = true, data = products });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Error retrieving category products" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> CreateCategory([FromBody] CategoryRequest request)
        {
            try
            {
                var category = new Category
                {
                    CategoryName = request.CategoryName,
                    ImagePath = request.ImagePath
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Category created successfully", categoryId = category.CategoryID });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Error creating category" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryRequest request)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                    return NotFound(new { success = false, message = "Category not found" });

                category.CategoryName = request.CategoryName;
                category.ImagePath = request.ImagePath;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Category updated successfully" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Error updating category" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                    return NotFound(new { success = false, message = "Category not found" });

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Category deleted successfully" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Cannot delete category. It may have related products." });
            }
        }
    }

    public class CategoryRequest
    {
        public string CategoryName { get; set; } = "";
        public string? ImagePath { get; set; }
    }
}
