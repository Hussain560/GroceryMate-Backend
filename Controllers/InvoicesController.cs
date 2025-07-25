using System.Globalization;
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
    public class InvoicesController : ControllerBase
    {
        private readonly GroceryStoreContext _context;
        private readonly ILogger<InvoicesController> _logger;

        public InvoicesController(GroceryStoreContext context, ILogger<InvoicesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetInvoices([FromQuery] string? search, [FromQuery] string? sortBy, [FromQuery] string? sortOrder)
        {
            try
            {
                _logger.LogInformation("Fetching invoices with search: {Search}, sortBy: {SortBy}, sortOrder: {SortOrder}", 
                    search, sortBy, sortOrder);

                var ksaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");
                
                IQueryable<Invoice> query = _context.Invoices
                    .Include(i => i.Sale);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.ToLower();
                    query = query.Where(i => 
                        i.InvoiceNumber.ToLower().Contains(search) ||
                        (i.Sale.CustomerName != null && i.Sale.CustomerName.ToLower().Contains(search))
                    );
                }

                query = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
                {
                    ("date", "desc") => query.OrderByDescending(i => i.CreatedDate),
                    ("date", _) => query.OrderBy(i => i.CreatedDate),
                    ("number", "desc") => query.OrderByDescending(i => i.InvoiceNumber),
                    ("number", _) => query.OrderBy(i => i.InvoiceNumber),
                    ("amount", "desc") => query.OrderByDescending(i => i.Sale.FinalTotal),
                    ("amount", _) => query.OrderBy(i => i.Sale.FinalTotal),
                    _ => query.OrderByDescending(i => i.CreatedDate)
                };

                var invoices = await query.Select(i => new
                {
                    saleId = i.Sale.SaleID,
                    invoiceNumber = i.InvoiceNumber,
                    createdDate = TimeZoneInfo.ConvertTimeFromUtc(
                        DateTime.SpecifyKind(i.CreatedDate, DateTimeKind.Utc),
                        ksaTimeZone).ToString("yyyy/MM/dd, h:mm:ss tt", CultureInfo.InvariantCulture),
                    customerName = i.Sale.CustomerName ?? "Walk-in Customer",
                    finalTotal = i.Sale.FinalTotal,
                    status = i.Status ?? "Generated"
                }).ToListAsync();

                return Ok(new { 
                    success = true,
                    data = invoices,
                    totalCount = invoices.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching invoices");
                return StatusCode(500, new { success = false, error = "Error retrieving invoices" });
            }
        }

        [HttpGet("{saleId}")]
        public async Task<IActionResult> GetInvoiceDetails(int saleId)
        {
            try
            {
                var ksaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");
                var sale = await _context.Sales
                    .Include(s => s.SaleDetails)
                        .ThenInclude(sd => sd.Product)
                            .ThenInclude(p => p.Brand)
                    .Include(s => s.Invoice)
                    .FirstOrDefaultAsync(s => s.SaleID == saleId);

                if (sale == null)
                    return NotFound(new { success = false, message = "Invoice not found" });

                var result = new
                {
                    success = true,
                    data = new
                    {
                        invoiceNumber = sale.InvoiceNumber,
                        saleDate = TimeZoneInfo.ConvertTimeFromUtc(
                            DateTime.SpecifyKind(sale.SaleDate, DateTimeKind.Utc),
                            ksaTimeZone).ToString("yyyy/MM/dd, h:mm:ss tt", CultureInfo.InvariantCulture),
                        customerName = sale.CustomerName ?? "Walk-in Customer",
                        paymentMethod = sale.PaymentMethod,
                        cashReceived = sale.CashReceived ?? 0,
                        change = sale.Change ?? 0,
                        items = sale.SaleDetails.Select(sd => new
                        {
                            name = sd.Product.ProductName,
                            brand = sd.Product.Brand.BrandName,
                            quantity = sd.Quantity,
                            unitPrice = sd.UnitPrice,
                            price = sd.UnitPrice,
                            discountPercentage = sd.DiscountPercentage,
                            subtotal = sd.LineFinalTotal
                        }).ToList()
                    }
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching invoice details");
                return StatusCode(500, new { success = false, error = "Error retrieving invoice details" });
            }
        }

        [HttpPost("scan")]
        public async Task<IActionResult> ScanInvoice([FromBody] string invoiceNumber)
        {
            if (string.IsNullOrEmpty(invoiceNumber))
                return BadRequest(new { success = false, error = "Invalid invoice number" });

            try
            {
                var invoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber.Trim());

                if (invoice == null)
                {
                    return NotFound(new { success = false, error = "Invoice not found" });
                }

                return Ok(new { 
                    success = true, 
                    invoiceNumber = invoice.InvoiceNumber,
                    saleId = invoice.SaleID
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}
