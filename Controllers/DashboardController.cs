using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using GroceryMateApi.Data;
using GroceryMateApi.Models;
using GroceryMateApi.ViewModels;

namespace GroceryMateApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly GroceryStoreContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardController(GroceryStoreContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        [HttpGet("overview")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetManagerOverview(
            [FromQuery] string period = "month",
            [FromQuery] int? year = null,
            [FromQuery] int? month = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] DateTime? date = null,
            [FromQuery] string stockFilter = "All Time")
        {
            try
            {
                var today = DateTime.UtcNow;
                var (periodStart, periodEnd) = period.ToLower() switch
                {
                    "week" => (today.AddDays(-7), today.AddDays(1)),
                    "month" when year.HasValue && month.HasValue => (
                        new DateTime(year.Value, month.Value, 1),
                        new DateTime(year.Value, month.Value, 1).AddMonths(1)),
                    "custom" when startDate.HasValue && endDate.HasValue => (startDate.Value, endDate.Value.AddDays(1)),
                    _ => (date.HasValue ? date.Value.Date : today.Date, (date.HasValue ? date.Value.Date : today.Date).AddDays(1))
                };

                var batchQuery = _context.ProductBatches
                    .Include(pb => pb.Product).ThenInclude(p => p.Category)
                    .AsQueryable();

                if (stockFilter != "All Time")
                {
                    if (stockFilter == "Last 30 Days")
                        batchQuery = batchQuery.Where(pb => EF.Functions.DateDiffDay(pb.CreatedAt, today) <= 30);
                    else if (stockFilter == "30-60 Days")
                        batchQuery = batchQuery.Where(pb => EF.Functions.DateDiffDay(pb.CreatedAt, today) >= 31 && EF.Functions.DateDiffDay(pb.CreatedAt, today) <= 60);
                    else if (stockFilter == "60-90 Days")
                        batchQuery = batchQuery.Where(pb => EF.Functions.DateDiffDay(pb.CreatedAt, today) >= 61 && EF.Functions.DateDiffDay(pb.CreatedAt, today) <= 90);
                    else if (stockFilter == "Near Expiry (<30 Days)")
                        batchQuery = batchQuery.Where(pb => EF.Functions.DateDiffDay(today, pb.ExpirationDate) <= 30);
                    else if (stockFilter == "Expired")
                        batchQuery = batchQuery.Where(pb => pb.ExpirationDate < today);
                }

                var salesQuery = _context.SaleDetails
                    .Include(sd => sd.Sale)
                    .Include(sd => sd.Product).ThenInclude(p => p.Category)
                    .Where(sd => sd.Sale.SaleDate >= periodStart && sd.Sale.SaleDate < periodEnd);

                // Sales metrics
                var netSales = await salesQuery.SumAsync(sd => sd.UnitPrice * sd.Quantity);
                var discountAmount = await salesQuery.SumAsync(sd => sd.UnitPrice * sd.Quantity * (sd.Product.DiscountPercentage / 100));
                var grossProfit = netSales - discountAmount;
                var returnAmount = 0m; // If you have a Returns table, sum here
                var salesTransactions = await _context.Sales
                    .Where(s => s.SaleDate >= periodStart && s.SaleDate < periodEnd)
                    .CountAsync();
                var averageTransactionValue = salesTransactions > 0
                    ? netSales / salesTransactions
                    : 0;
                var previousPeriodStart = periodStart.AddDays(-(periodEnd - periodStart).TotalDays);
                var previousNetSales = await _context.SaleDetails
                    .Include(sd => sd.Sale)
                    .Where(sd => sd.Sale.SaleDate >= previousPeriodStart && sd.Sale.SaleDate < periodStart)
                    .SumAsync(sd => sd.UnitPrice * sd.Quantity);
                var salesGrowthRate = previousNetSales > 0
                    ? ((netSales - previousNetSales) / previousNetSales) * 100
                    : 0;
                var operationalEfficiencyRatio = 85.0m; // Mock value

                // Hourly sales
                var hourlySales = await salesQuery
                    .GroupBy(sd => sd.Sale.SaleDate.Hour)
                    .Select(g => new { Hour = g.Key, Amount = g.Sum(sd => sd.UnitPrice * sd.Quantity) })
                    .ToListAsync();
                var hourlySalesArray = Enumerable.Range(0, 24)
                    .Select(h => hourlySales.FirstOrDefault(x => x.Hour == h) != null
                        ? new { Hour = h, Amount = hourlySales.First(x => x.Hour == h).Amount }
                        : new { Hour = h, Amount = 0m })
                    .OrderBy(x => x.Hour)
                    .ToList();

                var topProductsByNetSales = await salesQuery
                    .GroupBy(sd => sd.ProductID)
                    .Select(g => new { ProductName = g.First().Product.ProductName, Total = g.Sum(sd => sd.UnitPrice * sd.Quantity) })
                    .OrderByDescending(g => g.Total)
                    .Take(5)
                    .ToListAsync();

                var topPaymentByNetIncome = await _context.Sales
                    .Where(s => s.SaleDate >= periodStart && s.SaleDate < periodEnd)
                    .GroupBy(s => s.PaymentMethod)
                    .Select(g => new { PaymentMethod = g.Key, Total = g.Sum(s => s.FinalTotal) })
                    .OrderByDescending(g => g.Total)
                    .Take(3)
                    .ToListAsync();

                var topCategoriesBySalesVolume = await salesQuery
                    .GroupBy(sd => sd.Product.Category.CategoryName)
                    .Select(g => new { CategoryName = g.Key, Volume = g.Sum(sd => sd.Quantity) })
                    .OrderByDescending(g => g.Volume)
                    .Take(5)
                    .ToListAsync();

                // Inventory metrics
                var lowStockCount = await batchQuery.CountAsync(pb => pb.StockQuantity < pb.Product.ReorderLevel);
                var overstockCount = await batchQuery.CountAsync(pb => pb.StockQuantity > 2 * pb.Product.ReorderLevel);
                var expiredValue = await batchQuery.Where(pb => pb.ExpirationDate < today)
                    .SumAsync(pb => pb.StockQuantity * pb.Product.UnitPrice);
                var avgStockAge = await batchQuery.AnyAsync()
                    ? await batchQuery.AverageAsync(pb => EF.Functions.DateDiffDay(pb.CreatedAt, today))
                    : 0;
                var restockGroups = await _context.InventoryTransactions
                    .Where(it => it.TransactionDate >= today.AddMonths(-3))
                    .GroupBy(it => new { it.TransactionDate.Year, it.TransactionDate.Month })
                    .ToListAsync();
                var restockFrequency = restockGroups.Count > 0
                    ? restockGroups.Average(g => g.Count())
                    : 0;
                var topSlowMovingProducts = await salesQuery
                    .GroupBy(sd => sd.ProductID)
                    .Select(g => new
                    {
                        ProductName = g.First().Product.ProductName,
                        LastSoldDate = (DateTime?)g.Max(sd => (DateTime?)sd.Sale.SaleDate),
                        ExpirationDate = batchQuery.First(pb => pb.ProductID == g.Key).ExpirationDate
                    })
                    .OrderBy(g => g.LastSoldDate ?? (DateTime?)DateTime.MaxValue)
                    .Take(5)
                    .ToListAsync();

                var overviewData = new
                {
                    NetSales = netSales,
                    GrossProfit = grossProfit,
                    DiscountAmount = discountAmount,
                    ReturnAmount = returnAmount,
                    AverageTransactionValue = averageTransactionValue,
                    SalesGrowthRate = salesGrowthRate,
                    OperationalEfficiencyRatio = operationalEfficiencyRatio,
                    HourlySales = hourlySalesArray,
                    TopProductsByNetSales = topProductsByNetSales,
                    TopPaymentByNetIncome = topPaymentByNetIncome,
                    TopCategoriesBySalesVolume = topCategoriesBySalesVolume,
                    LowStockCount = lowStockCount,
                    OverstockCount = overstockCount,
                    ExpiredValue = expiredValue,
                    AvgStockAge = avgStockAge,
                    RestockFrequency = restockFrequency,
                    TopSlowMovingProducts = topSlowMovingProducts
                };

                return Ok(new { success = true, data = overviewData });
            }
            catch (Exception ex)
            {
                var errorMsg = "Error retrieving dashboard overview";
                if (ex is InvalidOperationException && ex.Message.Contains("could not be translated"))
                {
                    errorMsg += " (EF Core query translation error: check for unsupported LINQ in your queries)";
                }
                return StatusCode(500, new {
                    success = false,
                    error = errorMsg,
                    details = ex.Message
                });
            }
        }

        [HttpGet("employee")]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> GetEmployeeDashboard()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                    return Unauthorized(new { success = false, error = "User not authenticated" });

                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return NotFound(new { success = false, error = "User not found" });

                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);

                var todaysSales = await _context.Sales
                    .Where(s => s.UserID == user.Id && s.SaleDate >= today && s.SaleDate < tomorrow)
                    .ToListAsync();

                var recentSales = await _context.Sales
                    .Include(s => s.SaleDetails)
                    .Where(s => s.UserID == user.Id)
                    .OrderByDescending(s => s.SaleDate)
                    .Take(5)
                    .Select(s => new RecentSaleViewModel
                    {
                        SaleID = s.SaleID,
                        SaleDate = s.SaleDate,
                        FinalTotal = s.FinalTotal,
                        PaymentMethod = s.PaymentMethod ?? "Unknown",
                        ItemCount = s.SaleDetails.Count
                    })
                    .ToListAsync();

                var viewModel = new EmployeeViewModel
                {
                    FullName = user.FullName ?? user.UserName ?? "Unknown User",
                    TodaysSalesCount = todaysSales.Count,
                    TodaysSalesAmount = todaysSales.Sum(s => s.FinalTotal),
                    RecentSales = recentSales
                };

                return Ok(new { success = true, data = viewModel });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = "Error retrieving employee dashboard data", details = ex.Message });
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId) ? userId : 0;
        }
    }
}