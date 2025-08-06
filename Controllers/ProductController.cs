using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GroceryMateApi.Data;
using GroceryMateApi.Models;

namespace GroceryMateApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Employee,Manager")]
    public class ProductController : ControllerBase
    {
        private readonly GroceryStoreContext _context;
        private readonly ILogger<ProductController> _logger;

        public ProductController(GroceryStoreContext context, ILogger<ProductController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts([FromQuery] string? search, [FromQuery] int? categoryId, [FromQuery] int? brandId)
        {
            try
            {
                var query = _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Brand)
                    .Include(p => p.Supplier)
                    .Include(p => p.ProductBatches)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(search))
                {
                    search = search.ToLower();
                    query = query.Where(p => 
                        p.ProductName.ToLower().Contains(search) ||
                        (p.Barcode != null && p.Barcode.ToLower().Contains(search)));
                }

                if (categoryId.HasValue)
                {
                    query = query.Where(p => p.CategoryID == categoryId.Value);
                }

                if (brandId.HasValue)
                {
                    query = query.Where(p => p.BrandID == brandId.Value);
                }

                var products = await query
                    .OrderBy(p => p.ProductName)
                    .Select(p => new
                    {
                        id = p.ProductID,
                        name = p.ProductName,
                        brand = p.Brand.BrandName,
                        category = p.Category.CategoryName,
                        supplier = p.Supplier.SupplierName,
                        unitPrice = p.UnitPrice,
                        discountPercentage = p.DiscountPercentage,
                        stockQuantity = p.ProductBatches.Sum(pb => pb.StockQuantity), // FIX: use batches
                        reorderLevel = p.ReorderLevel,
                        barcode = p.Barcode,
                        imageUrl = p.ImageUrl,
                        createdAt = p.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = products });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching products");
                return StatusCode(500, new { success = false, error = "Error loading products" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProduct(int id)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Brand)
                    .Include(p => p.Supplier)
                    .Include(p => p.ProductBatches)
                    .Where(p => p.ProductID == id)
                    .Select(p => new
                    {
                        id = p.ProductID,
                        name = p.ProductName,
                        categoryId = p.CategoryID,
                        brandId = p.BrandID,
                        supplierId = p.SupplierID,
                        unitPrice = p.UnitPrice,
                        discountPercentage = p.DiscountPercentage,
                        stockQuantity = p.ProductBatches.Sum(pb => pb.StockQuantity), // FIX
                        reorderLevel = p.ReorderLevel,
                        barcode = p.Barcode,
                        imageUrl = p.ImageUrl,
                        brand = p.Brand.BrandName,
                        category = p.Category.CategoryName,
                        supplier = p.Supplier.SupplierName
                    })
                    .FirstOrDefaultAsync();

                if (product == null)
                    return NotFound(new { success = false, message = "Product not found" });

                return Ok(new { success = true, data = product });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching product");
                return StatusCode(500, new { success = false, error = "Error retrieving product" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> CreateProduct([FromBody] ProductRequest request)
        {
            try
            {
                var product = new Product
                {
                    ProductName = request.ProductName,
                    CategoryID = request.CategoryID,
                    BrandID = request.BrandID ?? 1,
                    SupplierID = request.SupplierID,
                    UnitPrice = request.UnitPrice,
                    DiscountPercentage = request.DiscountPercentage,
                    ReorderLevel = request.ReorderLevel,
                    Barcode = request.Barcode,
                    ImageUrl = request.ImageUrl,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                // Optionally, create initial ProductBatch here if needed

                return Ok(new { success = true, message = "Product created successfully", productId = product.ProductID });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Error creating product" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] ProductRequest request)
        {
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                    return NotFound(new { success = false, message = "Product not found" });

                product.ProductName = request.ProductName;
                product.CategoryID = request.CategoryID;
                product.BrandID = request.BrandID ?? product.BrandID;
                product.SupplierID = request.SupplierID;
                product.UnitPrice = request.UnitPrice;
                product.DiscountPercentage = request.DiscountPercentage;
                product.ReorderLevel = request.ReorderLevel;
                product.Barcode = request.Barcode;
                product.ImageUrl = request.ImageUrl;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Product updated successfully" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Error updating product" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                    return NotFound(new { success = false, message = "Product not found" });

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Product deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product");
                return StatusCode(500, new { success = false, error = "Cannot delete product. It may have related records." });
            }
        }

        [HttpGet("barcode/{barcode}")]
        public async Task<IActionResult> GetProductByBarcode(string barcode)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.Brand)
                    .Include(p => p.ProductBatches)
                    .Where(p => p.Barcode == barcode && p.ProductBatches.Sum(pb => pb.StockQuantity) > 0)
                    .Select(p => new
                    {
                        productId = p.ProductID,
                        productName = p.ProductName,
                        brand = p.Brand.BrandName,
                        unitPrice = p.UnitPrice,
                        discountPercentage = p.DiscountPercentage,
                        stockQuantity = p.ProductBatches.Sum(pb => pb.StockQuantity),
                        imageUrl = p.ImageUrl
                    })
                    .FirstOrDefaultAsync();

                if (product == null)
                    return NotFound(new { success = false, message = "Product not found" });

                return Ok(new { success = true, data = product });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching product by barcode");
                return StatusCode(500, new { success = false, error = "Error retrieving product" });
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchProducts([FromQuery] string q)
        {
            try
            {
                var products = await _context.Products
                    .Include(p => p.ProductBatches)
                    .Where(p => p.ProductName.Contains(q) && p.ProductBatches.Sum(pb => pb.StockQuantity) > 0)
                    .Select(p => new
                    {
                        p.ProductID,
                        p.ProductName,
                        p.UnitPrice,
                        stockQuantity = p.ProductBatches.Sum(pb => pb.StockQuantity),
                        p.DiscountPercentage
                    })
                    .Take(10)
                    .ToListAsync();

                return Ok(new { success = true, data = products });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products");
                return StatusCode(500, new { success = false, error = "Error searching products" });
            }
        }
    }

    public class ProductRequest
    {
        public string ProductName { get; set; } = "";
        public int CategoryID { get; set; }
        public int? BrandID { get; set; } = 1;
        public int SupplierID { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercentage { get; set; }
        public int StockQuantity { get; set; }
        public int ReorderLevel { get; set; }
        public string? Barcode { get; set; }
        public string? ImageUrl { get; set; }
    }
}

