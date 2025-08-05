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

        [HttpGet("manager")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetManagerDashboard(
            [FromQuery] string period = "day",
            [FromQuery] int? year = null,
            [FromQuery] int? month = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] DateTime? date = null)
        {
            try
            {
                // Initialize date range
                var currentDate = DateTime.UtcNow.Date;
                var (periodStart, periodEnd) = period.ToLower() switch
                {
                    "week" => (currentDate.AddDays(-7), currentDate.AddDays(1)),
                    "month" when year.HasValue && month.HasValue =>
                        (new DateTime(year.Value, month.Value, 1),
                         new DateTime(year.Value, month.Value, 1).AddMonths(1)),
                    "custom" when startDate.HasValue && endDate.HasValue =>
                        (startDate.Value.Date, endDate.Value.Date.AddDays(1)),
                    _ => (date?.Date ?? currentDate, (date?.Date ?? currentDate).AddDays(1))
                };

                // Get previous period for growth calculation
                var (prevPeriodStart, prevPeriodEnd) = period.ToLower() switch
                {
                    "week" => (periodStart.AddDays(-7), periodStart),
                    "month" => (periodStart.AddMonths(-1), periodStart),
                    "custom" => (
                        periodStart.AddDays(-(periodEnd - periodStart).Days),
                        periodStart),
                    _ => (periodStart.AddDays(-1), periodStart)
                };

                // Current period query
                var currentQuery = _context.Sales
                    .Include(s => s.SaleDetails)
                        .ThenInclude(sd => sd.Product)
                            .ThenInclude(p => p.Category)
                    .Where(s => s.SaleDate >= periodStart && s.SaleDate < periodEnd);

                // Previous period query for growth calculation
                var previousQuery = _context.Sales
                    .Where(s => s.SaleDate >= prevPeriodStart && s.SaleDate < prevPeriodEnd);

                // Calculate metrics
                var metrics = new DashboardMetricsViewModel
                {
                    SalesTransactions = await currentQuery.CountAsync(),
                    NetSales = await currentQuery.SumAsync(s => s.FinalTotal),
                    DiscountAmount = await currentQuery.SumAsync(s => s.TotalDiscountAmount),
                    ReturnAmount = await currentQuery.SumAsync(s => s.ReturnAmount ?? 0m)
                };

                // Calculate averages and ratios
                metrics.AverageTransactionValue = metrics.SalesTransactions > 0
                    ? metrics.NetSales / metrics.SalesTransactions
                    : 0;

                var previousNetSales = await previousQuery.SumAsync(s => s.FinalTotal);
                metrics.SalesGrowthRate = previousNetSales > 0
                    ? ((metrics.NetSales - previousNetSales) / previousNetSales) * 100
                    : 0;

                // Calculate gross profit
                var totalCost = await currentQuery
                    .SelectMany(s => s.SaleDetails)
                    .SumAsync(sd => sd.OriginalUnitPrice * sd.Quantity);
                metrics.GrossProfit = metrics.NetSales - totalCost;

                // Get top products
                metrics.TopProducts = await currentQuery
                    .SelectMany(s => s.SaleDetails)
                    .GroupBy(sd => sd.Product.ProductName)
                    .Select(g => new TopProductViewModel
                    {
                        ProductName = g.Key,
                        NetSales = g.Sum(sd => sd.LineFinalTotal),
                        Quantity = g.Sum(sd => sd.Quantity)
                    })
                    .OrderByDescending(p => p.NetSales)
                    .Take(5)
                    .ToListAsync();

                // Get top payment methods
                metrics.TopPayments = await currentQuery
                    .GroupBy(s => s.PaymentMethod)
                    .Select(g => new TopPaymentViewModel
                    {
                        PaymentMethod = g.Key ?? "Unknown",
                        NetIncome = g.Sum(s => s.FinalTotal - s.TotalDiscountAmount),
                        TransactionCount = g.Count()
                    })
                    .OrderByDescending(p => p.NetIncome)
                    .Take(5)
                    .ToListAsync();

                // Get top categories
                metrics.TopCategories = await currentQuery
                    .SelectMany(s => s.SaleDetails)
                    .GroupBy(sd => sd.Product.Category.CategoryName)
                    .Select(g => new TopCategoryViewModel
                    {
                        CategoryName = g.Key,
                        SalesVolume = g.Sum(sd => sd.Quantity),
                        NetSales = g.Sum(sd => sd.LineFinalTotal)
                    })
                    .OrderByDescending(c => c.SalesVolume)
                    .Take(5)
                    .ToListAsync();

                return Ok(new { success = true, data = metrics });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new {
                    success = false,
                    error = "Error retrieving dashboard data",
                    details = ex.Message
                });
            }
        }

        [HttpGet("metrics")]
        public async Task<IActionResult> GetMetrics()
        {
            try
            {
                var metrics = new
                {
                    totalSales = await _context.Sales.SumAsync(s => s.FinalTotal),
                    stockAlerts = await _context.Products.CountAsync(p => p.StockQuantity < p.ReorderLevel),
                    payments = new
                    {
                        cash = await _context.Sales.Where(s => s.PaymentMethod == "Cash").SumAsync(s => s.FinalTotal),
                        card = await _context.Sales.Where(s => s.PaymentMethod == "Card").SumAsync(s => s.FinalTotal)
                    }
                };
                return Ok(new { success = true, data = metrics });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = "Error retrieving metrics", details = ex.Message });
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