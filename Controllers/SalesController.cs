using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using GroceryMateApi.Data;
using GroceryMateApi.Models;
using GroceryMateApi.Services;

namespace GroceryMateApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Employee,Manager")]
    public class SalesController : ControllerBase
    {
        private readonly GroceryStoreContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<SalesController> _logger;
        private readonly InvoiceNumberGenerator _invoiceGenerator;

        public SalesController(
            GroceryStoreContext context, 
            UserManager<ApplicationUser> userManager,
            ILogger<SalesController> logger,
            InvoiceNumberGenerator invoiceGenerator)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _invoiceGenerator = invoiceGenerator;
        }

        [HttpGet]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetSales()
        {
            try
            {
                var sales = await _context.Sales
                    .Include(s => s.User)
                    .Include(s => s.SaleDetails)
                    .OrderByDescending(s => s.SaleDate)
                    .Select(s => new
                    {
                        id = s.SaleID,
                        invoiceNumber = s.InvoiceNumber,
                        saleDate = s.SaleDate,
                        customerName = s.CustomerName ?? "Walk-in Customer",
                        finalTotal = s.FinalTotal,
                        paymentMethod = s.PaymentMethod,
                        userName = s.User.FullName ?? s.User.UserName,
                        itemCount = s.SaleDetails.Count
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = sales });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching sales");
                return StatusCode(500, new { success = false, error = "Error retrieving sales" });
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetSaleDetails(int id)
        {
            try
            {
                var sale = await _context.Sales
                    .Include(s => s.User)
                    .Include(s => s.SaleDetails)
                    .ThenInclude(sd => sd.Product)
                    .ThenInclude(p => p.Brand)
                    .FirstOrDefaultAsync(s => s.SaleID == id);

                if (sale == null)
                    return NotFound(new { success = false, message = "Sale not found" });

                var result = new
                {
                    success = true,
                    data = new
                    {
                        id = sale.SaleID,
                        invoiceNumber = sale.InvoiceNumber,
                        saleDate = sale.SaleDate,
                        customerName = sale.CustomerName ?? "Walk-in Customer",
                        paymentMethod = sale.PaymentMethod,
                        cashReceived = sale.CashReceived,
                        change = sale.Change,
                        subtotalBeforeDiscount = sale.SubtotalBeforeDiscount,
                        totalDiscountAmount = sale.TotalDiscountAmount,
                        subtotalAfterDiscount = sale.SubtotalAfterDiscount,
                        totalVATAmount = sale.TotalVATAmount,
                        finalTotal = sale.FinalTotal,
                        userName = sale.User.FullName ?? sale.User.UserName,
                        items = sale.SaleDetails.Select(sd => new
                        {
                            productName = sd.Product.ProductName,
                            brand = sd.Product.Brand.BrandName,
                            quantity = sd.Quantity,
                            unitPrice = sd.UnitPrice,
                            discountPercentage = sd.DiscountPercentage,
                            lineSubtotalBeforeDiscount = sd.LineSubtotalBeforeDiscount,
                            lineDiscountAmount = sd.LineDiscountAmount,
                            lineSubtotalAfterDiscount = sd.LineSubtotalAfterDiscount,
                            lineVATAmount = sd.LineVATAmount,
                            lineFinalTotal = sd.LineFinalTotal
                        }).ToList()
                    }
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching sale details");
                return StatusCode(500, new { success = false, error = "Error retrieving sale details" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateSale([FromBody] SaleRequest request)
        {
            if (request?.Items == null || !request.Items.Any())
            {
                return BadRequest(new { success = false, error = "No items in sale" });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                    return Unauthorized(new { success = false, error = "User not authenticated" });

                var invoiceNumber = await _invoiceGenerator.GenerateInvoiceNumber();

                var sale = new Sale
                {
                    UserID = userId,
                    SaleDate = DateTime.UtcNow,
                    InvoiceNumber = invoiceNumber,
                    PaymentMethod = request.PaymentMethod ?? "Cash",
                    CashReceived = request.CashReceived,
                    Change = request.Change,
                    SubtotalBeforeDiscount = request.SubtotalBeforeDiscount,
                    TotalDiscountPercentage = request.TotalDiscountPercentage,
                    TotalDiscountAmount = request.TotalDiscountAmount,
                    SubtotalAfterDiscount = request.SubtotalAfterDiscount,
                    TotalVATAmount = request.TotalVATAmount,
                    VATPercentage = request.VATPercentage,
                    FinalTotal = request.FinalTotal,
                    CustomerName = request.CustomerName,
                    CustomerPhone = request.CustomerPhone,
                    SaleDetails = new List<SaleDetail>()
                };

                foreach (var item in request.Items)
                {
                    var product = await _context.Products
                        .FirstOrDefaultAsync(p => p.ProductID == item.ProductID);

                    if (product == null)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { success = false, error = $"Product not found: {item.ProductID}" });
                    }

                    if (product.StockQuantity < item.Quantity)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { success = false, error = $"Insufficient stock for {product.ProductName}" });
                    }

                    // Deduct quantity from product stock
                    product.StockQuantity -= item.Quantity;

                    decimal lineSubtotalBeforeDiscount = item.Subtotal;
                    decimal lineDiscountAmount = lineSubtotalBeforeDiscount * (item.DiscountPercentage / 100M);
                    decimal lineSubtotalAfterDiscount = lineSubtotalBeforeDiscount - lineDiscountAmount;
                    decimal lineVatAmount = lineSubtotalAfterDiscount * 0.15M;

                    sale.SaleDetails.Add(new SaleDetail
                    {
                        ProductID = item.ProductID,
                        Quantity = item.Quantity,
                        OriginalUnitPrice = item.UnitPrice,
                        UnitPrice = item.UnitPrice,
                        UnitPriceAfterDiscount = item.UnitPrice * (1 - (item.DiscountPercentage / 100M)),
                        DiscountPercentage = item.DiscountPercentage,
                        LineSubtotalBeforeDiscount = lineSubtotalBeforeDiscount,
                        LineDiscountAmount = lineDiscountAmount,
                        LineSubtotalAfterDiscount = lineSubtotalAfterDiscount,
                        LineVATAmount = lineVatAmount,
                        VATPercentage = 15.00M,
                        LineFinalTotal = lineSubtotalAfterDiscount + lineVatAmount
                    });
                }

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                var invoice = new Invoice
                {
                    InvoiceNumber = invoiceNumber,
                    SaleID = sale.SaleID,
                    CreatedDate = DateTime.UtcNow,
                    Status = "Generated"
                };

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new 
                { 
                    success = true, 
                    invoiceNumber = sale.InvoiceNumber,
                    saleId = sale.SaleID
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating sale");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost("scan")]
        public async Task<IActionResult> ScanBarcode([FromBody] string barcode)
        {
            try
            {
                _logger.LogInformation($"Scanning barcode: {barcode}");

                var product = await _context.Products
                    .Include(p => p.Brand)
                    .Where(p => p.Barcode == barcode && p.StockQuantity > 0)
                    .Select(p => new
                    {
                        productID = p.ProductID,
                        productName = p.ProductName,
                        name = p.ProductName,
                        brand = p.Brand.BrandName,
                        price = p.UnitPrice,
                        unitPrice = p.UnitPrice,
                        discountPercentage = p.DiscountPercentage,
                        stockQuantity = p.StockQuantity,
                        imageUrl = p.ImageUrl,
                        barcode = p.Barcode
                    })
                    .FirstOrDefaultAsync();

                if (product == null)
                    return NotFound(new { success = false, error = "Product not found" });

                return Ok(new { success = true, product = product });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing barcode scan");
                return StatusCode(500, new { success = false, error = "Error processing barcode" });
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId) ? userId : 0;
        }
    }

    public class SaleRequest
    {
        public List<SaleItemRequest> Items { get; set; } = new();
        public string PaymentMethod { get; set; } = "Cash";
        public decimal? CashReceived { get; set; }
        public decimal? Change { get; set; }
        public decimal SubtotalBeforeDiscount { get; set; }
        public decimal TotalDiscountPercentage { get; set; }
        public decimal TotalDiscountAmount { get; set; }
        public decimal SubtotalAfterDiscount { get; set; }
        public decimal TotalVATAmount { get; set; }
        public decimal VATPercentage { get; set; }
        public decimal FinalTotal { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
    }

    public class SaleItemRequest
    {
        public int ProductID { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercentage { get; set; }
        public decimal Subtotal { get; set; }
    }
}
