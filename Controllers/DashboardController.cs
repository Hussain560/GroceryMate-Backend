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
        public async Task<IActionResult> GetManagerDashboard([FromQuery] string period = "Day")
        {
            try
            {
                var query = _context.Sales.AsQueryable();
                switch (period.ToLower())
                {
                    case "week":
                        query = query.Where(s => s.SaleDate >= DateTime.UtcNow.AddDays(-7));
                        break;
                    case "month":
                        query = query.Where(s => s.SaleDate >= DateTime.UtcNow.AddMonths(-1));
                        break;
                    case "custom":
                        // Add custom date range logic later
                        break;
                }

                var model = new DashboardViewModel
                {
                    Sales = await query.CountAsync(),
                    NetSales = await query.SumAsync(s => s.FinalTotal),
                    NetIncome = await query.SumAsync(s => s.FinalTotal - s.SaleDetails.Sum(d => d.OriginalUnitPrice * d.Quantity)), // Mock, add costs
                    DiscountAmount = await query.SumAsync(s => s.TotalDiscountAmount),
                    ReturnAmount = 0m, // Add Return model later
                    LowStockCount = await _context.Products.CountAsync(p => p.StockQuantity < p.ReorderLevel),
                    TodaysSales = await _context.Sales.Where(s => s.SaleDate.Date == DateTime.UtcNow.Date).SumAsync(s => s.FinalTotal)
                };

                return Ok(new { success = true, data = model });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = "Error retrieving dashboard data", details = ex.Message });
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