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

                // Materialize batches and sales for efficient querying
                var batches = await batchQuery.ToListAsync();
                var sales = await salesQuery.ToListAsync();

                // Group transactions for restock frequency
                var restockGroups = await _context.InventoryTransactions
                    .Where(it => it.TransactionDate >= today.AddMonths(-3))
                    .ToListAsync();

                var netSales = (await salesQuery.SumAsync(sd => (decimal?)sd.UnitPrice * sd.Quantity) ?? 0);
                var discountAmount = await salesQuery.SumAsync(sd => (decimal?)sd.UnitPrice * sd.Quantity * (sd.Product.DiscountPercentage / 100)) ?? 0;
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
                    .SumAsync(sd => (decimal?)sd.UnitPrice * sd.Quantity) ?? 0;
                var salesGrowthRate = previousNetSales > 0
                    ? ((netSales - previousNetSales) / previousNetSales) * 100
                    : 0;
                var operationalEfficiencyRatio = 85.0m; // Mock value

                // Hourly sales
                var hourlySales = Enumerable.Range(0, 24).Select(i => new
                {
                    Hour = i,
                    Amount = sales
                        .Where(sd => sd.Sale.SaleDate.Hour == i)
                        .Sum(sd => (decimal?)sd.UnitPrice * sd.Quantity) ?? 0
                }).ToList();

                var topProductsByNetSales = sales
                    .GroupBy(sd => sd.ProductID)
                    .Select(g => new { ProductName = g.First().Product.ProductName, Total = g.Sum(sd => (decimal?)sd.UnitPrice * sd.Quantity) ?? 0 })
                    .OrderByDescending(g => g.Total)
                    .Take(5)
                    .ToList();

                var topPaymentByNetIncome = await _context.Sales
                    .Where(s => s.SaleDate >= periodStart && s.SaleDate < periodEnd)
                    .GroupBy(s => s.PaymentMethod)
                    .Select(g => new { PaymentMethod = g.Key, Total = g.Sum(s => (decimal?)s.FinalTotal) ?? 0 })
                    .OrderByDescending(g => g.Total)
                    .Take(3)
                    .ToListAsync();

                var topCategoriesBySalesVolume = sales
                    .GroupBy(sd => sd.Product.Category.CategoryName)
                    .Select(g => new { CategoryName = g.Key, Volume = g.Sum(sd => sd.Quantity) })
                    .OrderByDescending(g => g.Volume)
                    .Take(5)
                    .ToList();

                var overviewData = new
                {
                    // Sales metrics (existing)
                    NetSales = netSales,
                    GrossProfit = grossProfit,
                    DiscountAmount = discountAmount,
                    ReturnAmount = returnAmount,
                    AverageTransactionValue = averageTransactionValue,
                    SalesGrowthRate = salesGrowthRate,
                    OperationalEfficiencyRatio = operationalEfficiencyRatio,
                    HourlySales = hourlySales,
                    TopProductsByNetSales = topProductsByNetSales,
                    TopPaymentByNetIncome = topPaymentByNetIncome,
                    TopCategoriesBySalesVolume = topCategoriesBySalesVolume,

                    // Inventory metrics (merged from InventoryController)
                    LowStockCount = batches.Count(pb => pb.StockQuantity < pb.Product.ReorderLevel),
                    TurnoverRate = sales.Any()
                        ? sales.GroupBy(sd => sd.ProductID)
                            .Select(g =>
                            {
                                var stock = batches.Where(pb => pb.ProductID == g.Key).Sum(pb => pb.StockQuantity);
                                return stock > 0 ? (decimal)g.Sum(sd => sd.Quantity) / stock : 0;
                            })
                            .DefaultIfEmpty(0)
                            .Average()
                        : 0,
                    OverstockCount = batches.Count(pb => pb.StockQuantity > 2 * (pb.Product.ReorderLevel > 0 ? pb.Product.ReorderLevel : 10)),
                    AvgStockAge = batches.Any()
                        ? batches.Average(pb => (today - pb.CreatedAt).TotalDays)
                        : 0,
                    StockValue = batches
                        .GroupBy(pb => pb.Product.Category.CategoryName)
                        .ToDictionary(g => g.Key, g => g.Sum(pb => pb.StockQuantity * pb.Product.UnitPrice)),
                    RestockFrequency = restockGroups
                        .GroupBy(it => new { it.TransactionDate.Year, it.TransactionDate.Month })
                        .Select(g => g.Count())
                        .DefaultIfEmpty(0)
                        .Average(),
                    TopSlowMovingProducts = sales
                        .GroupBy(sd => sd.ProductID)
                        .Select(g => new
                        {
                            ProductName = g.First().Product.ProductName,
                            LastSoldDate = g.Max(sd => (DateTime?)sd.Sale.SaleDate),
                            ExpirationDate = batches.FirstOrDefault(pb => pb.ProductID == g.Key)?.ExpirationDate
                        })
                        .OrderBy(g => g.LastSoldDate ?? DateTime.MaxValue)
                        .Take(5)
                        .ToList(),
                    OutageRiskCount = batches.Any() && sales.Any()
                        ? batches
                            .Join(sales, pb => pb.ProductID, sd => sd.ProductID, (pb, sd) => new { pb, sd })
                            .GroupBy(x => x.pb.ProductID)
                            .Select(g =>
                            {
                                var avgDailySales = g.Sum(x => x.sd.Quantity) / 30m;
                                return new { ProductID = g.Key, AvgDailySales = avgDailySales };
                            })
                            .Join(batches, g => g.ProductID, pb => pb.ProductID, (g, pb) => new { pb, g.AvgDailySales })
                            .Count(x => x.pb.StockQuantity < x.AvgDailySales)
                        : 0,
                    ExpiredValue = batches.Where(pb => pb.ExpirationDate < today)
                        .Sum(pb => pb.StockQuantity * pb.Product.UnitPrice)
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
                return StatusCode(500, new
                {
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