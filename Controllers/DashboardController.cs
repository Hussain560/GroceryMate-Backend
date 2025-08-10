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

                // Calculate granularity for custom periods
                var daysDiff = period.ToLower() == "custom" && startDate.HasValue && endDate.HasValue
                    ? (endDate.Value - startDate.Value).Days
                    : 0;

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

                // Calculate metrics
                var netSales = sales.Sum(sd => sd.UnitPrice * sd.Quantity);
                var discountAmount = sales.Sum(sd => sd.UnitPrice * sd.Quantity * (sd.Product.DiscountPercentage / 100));
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

                // Calculate history arrays based on period
                object netSalesHistory, grossProfitHistory, discountAmountHistory, returnAmountHistory, 
                       averageTransactionValueHistory, salesGrowthRateHistory, operationalEfficiencyRatioHistory, 
                       salesTransactionsHistory;

                if (period.ToLower() == "day")
                {
                    // 24 hourly values
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

                    salesGrowthRateHistory = Enumerable.Range(0, 24)
                        .Select(hour => new { hour = hour.ToString("00") + ":00", value = 0m })
                        .ToList();

                    operationalEfficiencyRatioHistory = Enumerable.Range(0, 24)
                        .Select(hour => new { hour = hour.ToString("00") + ":00", value = 85.0m })
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
                    // 7 daily values
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
                                salesGrowthRate = 0m,
                                operationalEfficiencyRatio = 85.0m,
                                salesTransactions = dailyTransactions
                            };
                        })
                        .ToList();

                    netSalesHistory = weekData.Select(d => new { date = d.date, value = d.netSales }).ToList();
                    grossProfitHistory = weekData.Select(d => new { date = d.date, value = d.grossProfit }).ToList();
                    discountAmountHistory = weekData.Select(d => new { date = d.date, value = d.discountAmount }).ToList();
                    returnAmountHistory = weekData.Select(d => new { date = d.date, value = d.returnAmount }).ToList();
                    averageTransactionValueHistory = weekData.Select(d => new { date = d.date, value = d.averageTransactionValue }).ToList();
                    salesGrowthRateHistory = weekData.Select(d => new { date = d.date, value = d.salesGrowthRate }).ToList();
                    operationalEfficiencyRatioHistory = weekData.Select(d => new { date = d.date, value = d.operationalEfficiencyRatio }).ToList();
                    salesTransactionsHistory = weekData.Select(d => new { date = d.date, value = d.salesTransactions }).ToList();
                }
                else if (period.ToLower() == "month")
                {
                    // Monthly daily values (28-31 days)
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
                                salesGrowthRate = 0m,
                                operationalEfficiencyRatio = 85.0m,
                                salesTransactions = dailyTransactions
                            };
                        })
                        .ToList();

                    netSalesHistory = monthData.Select(d => new { date = d.date, value = d.netSales }).ToList();
                    grossProfitHistory = monthData.Select(d => new { date = d.date, value = d.grossProfit }).ToList();
                    discountAmountHistory = monthData.Select(d => new { date = d.date, value = d.discountAmount }).ToList();
                    returnAmountHistory = monthData.Select(d => new { date = d.date, value = d.returnAmount }).ToList();
                    averageTransactionValueHistory = monthData.Select(d => new { date = d.date, value = d.averageTransactionValue }).ToList();
                    salesGrowthRateHistory = monthData.Select(d => new { date = d.date, value = d.salesGrowthRate }).ToList();
                    operationalEfficiencyRatioHistory = monthData.Select(d => new { date = d.date, value = d.operationalEfficiencyRatio }).ToList();
                    salesTransactionsHistory = monthData.Select(d => new { date = d.date, value = d.salesTransactions }).ToList();
                }
                else if (period.ToLower() == "custom")
                {
                    if (daysDiff <= 31)
                    {
                        // Daily values for short periods
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
                                    salesGrowthRate = 0m,
                                    operationalEfficiencyRatio = 85.0m,
                                    salesTransactions = dailyTransactions
                                };
                            })
                            .ToList();

                        netSalesHistory = customDailyData.Select(d => new { date = d.date, value = d.netSales }).ToList();
                        grossProfitHistory = customDailyData.Select(d => new { date = d.date, value = d.grossProfit }).ToList();
                        discountAmountHistory = customDailyData.Select(d => new { date = d.date, value = d.discountAmount }).ToList();
                        returnAmountHistory = customDailyData.Select(d => new { date = d.date, value = d.returnAmount }).ToList();
                        averageTransactionValueHistory = customDailyData.Select(d => new { date = d.date, value = d.averageTransactionValue }).ToList();
                        salesGrowthRateHistory = customDailyData.Select(d => new { date = d.date, value = d.salesGrowthRate }).ToList();
                        operationalEfficiencyRatioHistory = customDailyData.Select(d => new { date = d.date, value = d.operationalEfficiencyRatio }).ToList();
                        salesTransactionsHistory = customDailyData.Select(d => new { date = d.date, value = d.salesTransactions }).ToList();
                    }
                    else if (daysDiff <= 365)
                    {
                        // Monthly values for medium periods
                        var customMonthlyData = sales
                            .GroupBy(sd => new { sd.Sale.SaleDate.Year, sd.Sale.SaleDate.Month })
                            .Select(g =>
                            {
                                var monthlyNet = g.Sum(sd => sd.UnitPrice * sd.Quantity);
                                var monthlyDiscount = g.Sum(sd => sd.UnitPrice * sd.Quantity * (sd.Product.DiscountPercentage / 100));
                                var monthlyTransactions = g.Select(sd => sd.Sale.SaleID).Distinct().Count();

                                return new
                                {
                                    date = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("yyyy-MM"),
                                    netSales = monthlyNet,
                                    grossProfit = monthlyNet - monthlyDiscount,
                                    discountAmount = monthlyDiscount,
                                    returnAmount = 0m,
                                    averageTransactionValue = monthlyTransactions > 0 ? monthlyNet / monthlyTransactions : 0,
                                    salesGrowthRate = 0m,
                                    operationalEfficiencyRatio = 85.0m,
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
                        salesGrowthRateHistory = customMonthlyData.Select(d => new { date = d.date, value = d.salesGrowthRate }).ToList();
                        operationalEfficiencyRatioHistory = customMonthlyData.Select(d => new { date = d.date, value = d.operationalEfficiencyRatio }).ToList();
                        salesTransactionsHistory = customMonthlyData.Select(d => new { date = d.date, value = d.salesTransactions }).ToList();
                    }
                    else
                    {
                        // Yearly values for long periods
                        var customYearlyData = sales
                            .GroupBy(sd => sd.Sale.SaleDate.Year)
                            .Select(g =>
                            {
                                var yearlyNet = g.Sum(sd => sd.UnitPrice * sd.Quantity);
                                var yearlyDiscount = g.Sum(sd => sd.UnitPrice * sd.Quantity * (sd.Product.DiscountPercentage / 100));
                                var yearlyTransactions = g.Select(sd => sd.Sale.SaleID).Distinct().Count();

                                return new
                                {
                                    date = g.Key.ToString("yyyy"),
                                    netSales = yearlyNet,
                                    grossProfit = yearlyNet - yearlyDiscount,
                                    discountAmount = yearlyDiscount,
                                    returnAmount = 0m,
                                    averageTransactionValue = yearlyTransactions > 0 ? yearlyNet / yearlyTransactions : 0,
                                    salesGrowthRate = 0m,
                                    operationalEfficiencyRatio = 85.0m,
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
                        salesGrowthRateHistory = customYearlyData.Select(d => new { date = d.date, value = d.salesGrowthRate }).ToList();
                        operationalEfficiencyRatioHistory = customYearlyData.Select(d => new { date = d.date, value = d.operationalEfficiencyRatio }).ToList();
                        salesTransactionsHistory = customYearlyData.Select(d => new { date = d.date, value = d.salesTransactions }).ToList();
                    }
                }
                else
                {
                    // Default empty arrays
                    netSalesHistory = new List<object>();
                    grossProfitHistory = new List<object>();
                    discountAmountHistory = new List<object>();
                    returnAmountHistory = new List<object>();
                    averageTransactionValueHistory = new List<object>();
                    salesGrowthRateHistory = new List<object>();
                    operationalEfficiencyRatioHistory = new List<object>();
                    salesTransactionsHistory = new List<object>();
                }

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
                    // Sales metrics with history
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
                    SalesGrowthRate = salesGrowthRate,
                    SalesGrowthRateHistory = salesGrowthRateHistory,
                    OperationalEfficiencyRatio = operationalEfficiencyRatio,
                    OperationalEfficiencyRatioHistory = operationalEfficiencyRatioHistory,
                    SalesTransactions = salesTransactions,
                    SalesTransactionsHistory = salesTransactionsHistory,
                    HourlySales = hourlySales,
                    TopProductsByNetSales = topProductsByNetSales,
                    TopPaymentByNetIncome = topPaymentByNetIncome,
                    TopCategoriesBySalesVolume = topCategoriesBySalesVolume,

                    // Inventory metrics (existing)
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