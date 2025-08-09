using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using GroceryMateApi.Data;
using GroceryMateApi.Models;

namespace GroceryMateApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InventoryController : ControllerBase
    {
        private readonly GroceryStoreContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(GroceryStoreContext context, UserManager<ApplicationUser> userManager, ILogger<InventoryController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetInventory([FromQuery] string? search, [FromQuery] int? categoryId, [FromQuery] int? brandId)
        {
            try
            {
                var query = _context.Products
                    .Include(p => p.Brand)
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .Include(p => p.ProductBatches) // Include batches for stock calculation
                    .AsQueryable();

                if (!string.IsNullOrEmpty(search))
                {
                    search = search.ToLower();
                    query = query.Where(p => 
                        p.ProductName.ToLower().Contains(search) ||
                        (p.Barcode != null && p.Barcode.ToLower().Contains(search)) ||
                        p.Brand.BrandName.ToLower().Contains(search));
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
                        quantity = p.ProductBatches.Sum(pb => pb.StockQuantity), // Use batches for stock
                        reorderLevel = p.ReorderLevel,
                        price = p.UnitPrice,
                        discountPercentage = p.DiscountPercentage,
                        barcode = p.Barcode,
                        imageUrl = p.ImageUrl ?? "/images/products/default.jpg"
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = products });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching inventory");
                return StatusCode(500, new { success = false, error = "Error loading inventory" });
            }
        }

        [HttpGet("lowstock")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetLowStock([FromQuery] string? search, [FromQuery] int? categoryId, [FromQuery] int? brandId)
        {
            try
            {
                const int LOW_STOCK_THRESHOLD = 10;
                var query = _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Brand)
                    .Include(p => p.ProductBatches)
                    .Where(p => p.ProductBatches.Sum(pb => pb.StockQuantity) <= LOW_STOCK_THRESHOLD);

                if (!string.IsNullOrEmpty(search))
                {
                    search = search.ToLower();
                    query = query.Where(p => 
                        (p.Barcode != null && p.Barcode.ToLower().Contains(search)) ||
                        p.ProductName.ToLower().Contains(search));
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
                    .Select(p => new
                    {
                        productId = p.ProductID,
                        productName = p.ProductName,
                        brand = p.Brand.BrandName,
                        category = p.Category.CategoryName,
                        price = p.UnitPrice,
                        stockQuantity = p.ProductBatches.Sum(pb => pb.StockQuantity),
                        barcode = p.Barcode,
                        lowStockThreshold = LOW_STOCK_THRESHOLD,
                        imageUrl = p.ImageUrl ?? "/images/products/default.jpg"
                    })
                    .ToListAsync();

                _logger.LogInformation($"Low stock query returned {products.Count} products");

                return Ok(new { success = true, data = products });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching low stock products");
                return StatusCode(500, new { success = false, error = "Error retrieving low stock products" });
            }
        }

        [HttpPost("restock")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> CreateRestock([FromBody] InventoryTransactionRequest request)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.ProductBatches)
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.ProductID == request.ProductID);

                if (product == null)
                {
                    return NotFound(new { success = false, message = "Product not found" });
                }

                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                // Find batch to restock (e.g., latest batch or create new batch logic)
                var batch = product.ProductBatches.OrderByDescending(pb => pb.ExpirationDate).FirstOrDefault();
                if (batch == null)
                {
                    return BadRequest(new { success = false, message = "No batch found for product. Please create a batch first." });
                }

                var transaction = new InventoryTransaction
                {
                    ProductBatchID = batch.BatchID,
                    Quantity = request.Quantity,
                    TransactionDate = DateTime.UtcNow,
                    TransactionType = "Restock",
                    UserId = userId,
                    Notes = request.Notes
                };

                _context.InventoryTransactions.Add(transaction);
                batch.StockQuantity += request.Quantity;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = $"Successfully restocked {request.Quantity} units of {product.ProductName}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing restock transaction");
                return StatusCode(500, new { success = false, message = "Error processing restock" });
            }
        }

        [HttpPost("spoilage")]
        [Authorize(Roles = "Employee,Manager")]
        public async Task<IActionResult> CreateSpoilage([FromBody] InventoryTransactionRequest request)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.ProductBatches)
                    .FirstOrDefaultAsync(p => p.ProductID == request.ProductID);

                if (product == null)
                    return NotFound(new { success = false, message = "Product not found" });

                var batch = product.ProductBatches.OrderByDescending(pb => pb.ExpirationDate).FirstOrDefault();
                if (batch == null)
                    return BadRequest(new { success = false, message = "No batch found for product. Please create a batch first." });

                if (batch.StockQuantity < request.Quantity)
                    return BadRequest(new { success = false, message = $"Insufficient stock. Current stock: {batch.StockQuantity}" });

                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var transaction = new InventoryTransaction
                {
                    ProductBatchID = batch.BatchID,
                    Quantity = request.Quantity,
                    TransactionDate = DateTime.UtcNow,
                    TransactionType = "Spoilage",
                    UserId = userId,
                    Notes = request.Notes
                };

                batch.StockQuantity -= request.Quantity;
                _context.InventoryTransactions.Add(transaction);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Spoilage recorded successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing spoilage transaction");
                return StatusCode(500, new { success = false, message = "Error processing spoilage" });
            }
        }

        [HttpGet("transactions")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetTransactions()
        {
            try
            {
                var transactions = await _context.InventoryTransactions
                    .Include(t => t.ProductBatch)
                        .ThenInclude(pb => pb.Product)
                    .Include(t => t.User)
                    .OrderByDescending(t => t.TransactionDate)
                    .Select(t => new
                    {
                        id = t.TransactionID,
                        productName = t.ProductBatch.Product.ProductName,
                        quantity = t.Quantity,
                        transactionType = t.TransactionType,
                        transactionDate = t.TransactionDate,
                        userName = t.User.FullName ?? t.User.UserName,
                        notes = t.Notes
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = transactions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching transactions");
                return StatusCode(500, new { success = false, error = "Error loading transactions" });
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId) ? userId : 0;
        }
    }

    public class InventoryTransactionRequest
    {
        public int ProductID { get; set; }
        public int Quantity { get; set; }
        public string? Notes { get; set; }
    }
}


