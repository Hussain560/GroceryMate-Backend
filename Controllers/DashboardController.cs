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

                var daysDiff = period.ToLower() == "custom" && startDate.HasValue && endDate.HasValue
                    ? (endDate.Value - startDate.Value).Days
                    : 0;

                // Use Products table only, no ProductBatches or expiration logic
                var productsQuery = _context.Products
                    .Include(p => p.Category)
                    .AsQueryable();

                if (stockFilter != "All Time")
                {
                    if (stockFilter == "Low Stock")
                        productsQuery = productsQuery.Where(p => p.StockQuantity < p.ReorderLevel);
                    else if (stockFilter == "Overstock")
                        productsQuery = productsQuery.Where(p => p.StockQuantity > 2 * (p.ReorderLevel > 0 ? p.ReorderLevel : 10));
                }

                var products = await productsQuery.ToListAsync();

                var salesQuery = _context.SaleDetails
                    .Include(sd => sd.Sale)
                    .Include(sd => sd.Product).ThenInclude(p => p.Category)
                    .Where(sd => sd.Sale.SaleDate >= periodStart && sd.Sale.SaleDate < periodEnd);

                var sales = await salesQuery.ToListAsync();

                var netSales = sales.Sum(sd => sd.UnitPrice * sd.Quantity);
                var discountAmount = sales.Sum(sd => sd.UnitPrice * sd.Quantity * (sd.Product.DiscountPercentage / 100));
                var grossProfit = netSales - discountAmount;
                var returnAmount = 0m;
                var salesTransactions = await _context.Sales
                    .Where(s => s.SaleDate >= periodStart && s.SaleDate < periodEnd)
                    .CountAsync();
                var averageTransactionValue = salesTransactions > 0
                    ? netSales / salesTransactions
                    : 0;

                // History arrays (sales only)
                object netSalesHistory, grossProfitHistory, discountAmountHistory, returnAmountHistory,
                       averageTransactionValueHistory, salesTransactionsHistory;

                if (period.ToLower() == "day")
                {
                    netSalesHistory = Enumerable.Range(0, 24)
                        .Select(hour => new
                        {
                            hour = hour.ToString("00") + ":00",
                            value = sales.Where(sd => sd.Sale.SaleDate.Hour == hour).Sum(sd => sd.UnitPrice * sd.Quantity)
                        })
                        .ToList();

                    grossProfitHistory = Enumerable.Range(0, 24)
                        .Select(hour =>
                        {
                            var hourlyData = sales.Where(sd => sd.Sale.SaleDate.Hour == hour);
                            var hourlyNet = hourlyData.Sum(sd => sd.UnitPrice * sd.Quantity);
                            var hourlyDiscount = hourlyData.Sum(sd => sd.UnitPrice * sd.Quantity * (sd.Product.DiscountPercentage / 100));
                            return new { hour = hour.ToString("00") + ":00", value = hourlyNet - hourlyDiscount };
                        })
                        .ToList();

                    discountAmountHistory = Enumerable.Range(0, 24)
                        .Select(hour => new
                        {
                            hour = hour.ToString("00") + ":00",
                            value = sales.Where(sd => sd.Sale.SaleDate.Hour == hour)
                                .Sum(sd => sd.UnitPrice * sd.Quantity * (sd.Product.DiscountPercentage / 100))
                        })
                        .ToList();

                    returnAmountHistory = Enumerable.Range(0, 24)
                        .Select(hour => new { hour = hour.ToString("00") + ":00", value = 0m })
                        .ToList();

                    averageTransactionValueHistory = Enumerable.Range(0, 24)
                        .Select(hour =>
                        {
                            var hourlyData = sales.Where(sd => sd.Sale.SaleDate.Hour == hour);
                            var hourlyTransactions = hourlyData.Select(sd => sd.Sale.SaleID).Distinct().Count();
                            var hourlyNet = hourlyData.Sum(sd => sd.UnitPrice * sd.Quantity);
                            return new { hour = hour.ToString("00") + ":00", value = hourlyTransactions > 0 ? hourlyNet / hourlyTransactions : 0 };
                        })
                        .ToList();

                    salesTransactionsHistory = Enumerable.Range(0, 24)
                        .Select(hour => new
                        {
                            hour = hour.ToString("00") + ":00",
                            value = sales.Where(sd => sd.Sale.SaleDate.Hour == hour).Select(sd => sd.Sale.SaleID).Distinct().Count()
                        })
                        .ToList();
                }
                else if (period.ToLower() == "week")
                {
                    var weekData = Enumerable.Range(0, 7)
                        .Select(day =>
                        {
                            var targetDate = periodStart.AddDays(day).Date;
                            var dailyData = sales.Where(sd => sd.Sale.SaleDate.Date == targetDate);
                            var dailyNet = dailyData.Sum(sd => sd.UnitPrice * sd.Quantity);
                            var dailyDiscount = dailyData.Sum(sd => sd.UnitPrice * sd.Quantity * (sd.Product.DiscountPercentage / 100));
                            var dailyTransactions = dailyData.Select(sd => sd.Sale.SaleID).Distinct().Count();

                            return new
                            {
                                date = targetDate.ToString("yyyy-MM-dd"),
                                netSales = dailyNet,
                                grossProfit = dailyNet - dailyDiscount,
                                discountAmount = dailyDiscount,
                                returnAmount = 0m,
                                averageTransactionValue = dailyTransactions > 0 ? dailyNet / dailyTransactions : 0,
                                salesTransactions = dailyTransactions
                            };
                        })
                        .ToList();

                    netSalesHistory = weekData.Select(d => new { date = d.date, value = d.netSales }).ToList();
                    grossProfitHistory = weekData.Select(d => new { date = d.date, value = d.grossProfit }).ToList();
                    discountAmountHistory = weekData.Select(d => new { date = d.date, value = d.discountAmount }).ToList();
                    returnAmountHistory = weekData.Select(d => new { date = d.date, value = d.returnAmount }).ToList();
                    averageTransactionValueHistory = weekData.Select(d => new { date = d.date, value = d.averageTransactionValue }).ToList();
                    salesTransactionsHistory = weekData.Select(d => new { date = d.date, value = d.salesTransactions }).ToList();
                }
                else if (period.ToLower() == "month")
                {
                    var daysInPeriod = (periodEnd - periodStart).Days;
                    var monthData = Enumerable.Range(0, daysInPeriod)
                        .Select(day =>
                        {
                            var targetDate = periodStart.AddDays(day).Date;
                            var dailyData = sales.Where(sd => sd.Sale.SaleDate.Date == targetDate);
                            var dailyNet = dailyData.Sum(sd => sd.UnitPrice * sd.Quantity);
                            var dailyDiscount = dailyData.Sum(sd => sd.UnitPrice * sd.Quantity * (sd.Product.DiscountPercentage / 100));
                            var dailyTransactions = dailyData.Select(sd => sd.Sale.SaleID).Distinct().Count();

                            return new
                            {
                                date = targetDate.ToString("yyyy-MM-dd"),
                                netSales = dailyNet,
                                grossProfit = dailyNet - dailyDiscount,
                                discountAmount = dailyDiscount,
                                returnAmount = 0m,
                                averageTransactionValue = dailyTransactions > 0 ? dailyNet / dailyTransactions : 0,
                                salesTransactions = dailyTransactions
                            };
                        })
                        .ToList();

                    netSalesHistory = monthData.Select(d => new { date = d.date, value = d.netSales }).ToList();
                    grossProfitHistory = monthData.Select(d => new { date = d.date, value = d.grossProfit }).ToList();
                    discountAmountHistory = monthData.Select(d => new { date = d.date, value = d.discountAmount }).ToList();
                    returnAmountHistory = monthData.Select(d => new { date = d.date, value = d.returnAmount }).ToList();
                    averageTransactionValueHistory = monthData.Select(d => new { date = d.date, value = d.averageTransactionValue }).ToList();
                    salesTransactionsHistory = monthData.Select(d => new { date = d.date, value = d.salesTransactions }).ToList();
                }
                else if (period.ToLower() == "custom")
                {
                    if (daysDiff <= 31)
                    {
                        var customDailyData = Enumerable.Range(0, daysDiff)
                            .Select(day =>
                            {
                                var targetDate = periodStart.AddDays(day).Date;
                                var dailyData = sales.Where(sd => sd.Sale.SaleDate.Date == targetDate);
                                var dailyNet = dailyData.Sum(sd => sd.UnitPrice * sd.Quantity);
                                var dailyDiscount = dailyData.Sum(sd => sd.UnitPrice * sd.Quantity * (sd.Product.DiscountPercentage / 100));
                                var dailyTransactions = dailyData.Select(sd => sd.Sale.SaleID).Distinct().Count();

                                return new
                                {
                                    date = targetDate.ToString("yyyy-MM-dd"),
                                    netSales = dailyNet,
                                    grossProfit = dailyNet - dailyDiscount,
                                    discountAmount = dailyDiscount,
                                    returnAmount = 0m,
                                    averageTransactionValue = dailyTransactions > 0 ? dailyNet / dailyTransactions : 0,
                                    salesTransactions = dailyTransactions
                                };
                            })
                            .ToList();

                        netSalesHistory = customDailyData.Select(d => new { date = d.date, value = d.netSales }).ToList();
                        grossProfitHistory = customDailyData.Select(d => new { date = d.date, value = d.grossProfit }).ToList();
                        discountAmountHistory = customDailyData.Select(d => new { date = d.date, value = d.discountAmount }).ToList();
                        returnAmountHistory = customDailyData.Select(d => new { date = d.date, value = d.returnAmount }).ToList();
                        averageTransactionValueHistory = customDailyData.Select(d => new { date = d.date, value = d.averageTransactionValue }).ToList();
                        salesTransactionsHistory = customDailyData.Select(d => new { date = d.date, value = d.salesTransactions }).ToList();
                    }
                    else if (daysDiff <= 365)
                    {
                        var customMonthlyData = sales
                            .GroupBy(sd => new { sd.Sale.SaleDate.Year, sd.Sale.SaleDate.Month })
                            .Select(g =>
                            {
                                var monthlyNet = g.Sum(sd => sd.UnitPrice * sd.Quantity);
                                var monthlyDiscount = g.Sum(sd => sd.UnitPrice * sd.Quantity * (sd.Product.DiscountPercentage / 100));
                                var monthlyTransactions = g.Select(sd => sd.Sale.SaleID).Distinct().Count();
                                var dateStr = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("yyyy-MM");
                                return new
                                {
                                    date = dateStr,
                                    netSales = monthlyNet,
                                    grossProfit = monthlyNet - monthlyDiscount,
                                    discountAmount = monthlyDiscount,
                                    returnAmount = 0m,
                                    averageTransactionValue = monthlyTransactions > 0 ? monthlyNet / monthlyTransactions : 0,
                                    salesTransactions = monthlyTransactions
                                };
                            })
                            .OrderBy(m => m.date)
                            .ToList();

                        netSalesHistory = customMonthlyData.Select(d => new { date = d.date, value = d.netSales }).ToList();
                        grossProfitHistory = customMonthlyData.Select(d => new { date = d.date, value = d.grossProfit }).ToList();
                        discountAmountHistory = customMonthlyData.Select(d => new { date = d.date, value = d.discountAmount }).ToList();
                        returnAmountHistory = customMonthlyData.Select(d => new { date = d.date, value = d.returnAmount }).ToList();
                        averageTransactionValueHistory = customMonthlyData.Select(d => new { date = d.date, value = d.averageTransactionValue }).ToList();
                        salesTransactionsHistory = customMonthlyData.Select(d => new { date = d.date, value = d.salesTransactions }).ToList();
                    }
                    else
                    {
                        var customYearlyData = sales
                            .GroupBy(sd => sd.Sale.SaleDate.Year)
                            .Select(g =>
                            {
                                var yearlyNet = g.Sum(sd => sd.UnitPrice * sd.Quantity);
                                var yearlyDiscount = g.Sum(sd => sd.UnitPrice * sd.Quantity * (sd.Product.DiscountPercentage / 100));
                                var yearlyTransactions = g.Select(sd => sd.Sale.SaleID).Distinct().Count();
                                var dateStr = g.Key.ToString();
                                return new
                                {
                                    date = dateStr,
                                    netSales = yearlyNet,
                                    grossProfit = yearlyNet - yearlyDiscount,
                                    discountAmount = yearlyDiscount,
                                    returnAmount = 0m,
                                    averageTransactionValue = yearlyTransactions > 0 ? yearlyNet / yearlyTransactions : 0,
                                    salesTransactions = yearlyTransactions
                                };
                            })
                            .OrderBy(y => y.date)
                            .ToList();

                        netSalesHistory = customYearlyData.Select(d => new { date = d.date, value = d.netSales }).ToList();
                        grossProfitHistory = customYearlyData.Select(d => new { date = d.date, value = d.grossProfit }).ToList();
                        discountAmountHistory = customYearlyData.Select(d => new { date = d.date, value = d.discountAmount }).ToList();
                        returnAmountHistory = customYearlyData.Select(d => new { date = d.date, value = d.returnAmount }).ToList();
                        averageTransactionValueHistory = customYearlyData.Select(d => new { date = d.date, value = d.averageTransactionValue }).ToList();
                        salesTransactionsHistory = customYearlyData.Select(d => new { date = d.date, value = d.salesTransactions }).ToList();
                    }
                }
                else
                {
                    netSalesHistory = new List<object>();
                    grossProfitHistory = new List<object>();
                    discountAmountHistory = new List<object>();
                    returnAmountHistory = new List<object>();
                    averageTransactionValueHistory = new List<object>();
                    salesTransactionsHistory = new List<object>();
                }

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
                    NetSales = netSales,
                    NetSalesHistory = netSalesHistory,
                    GrossProfit = grossProfit,
                    GrossProfitHistory = grossProfitHistory,
                    DiscountAmount = discountAmount,
                    DiscountAmountHistory = discountAmountHistory,
                    ReturnAmount = returnAmount,
                    ReturnAmountHistory = returnAmountHistory,
                    AverageTransactionValue = averageTransactionValue,
                    AverageTransactionValueHistory = averageTransactionValueHistory,
                    SalesTransactions = salesTransactions,
                    SalesTransactionsHistory = salesTransactionsHistory,
                    HourlySales = hourlySales,
                    TopProductsByNetSales = topProductsByNetSales,
                    TopPaymentByNetIncome = topPaymentByNetIncome,
                    TopCategoriesBySalesVolume = topCategoriesBySalesVolume
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