using GroceryMateApi.Models;

namespace GroceryMateApi.ViewModels
{
    public class DashboardViewModel
    {
        public int Sales { get; set; }
        public decimal NetSales { get; set; }
        public decimal NetIncome { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal ReturnAmount { get; set; }
        public int LowStockCount { get; set; }
        public decimal TodaysSales { get; set; }
        public List<InventoryTransaction> RecentTransactions { get; set; } = new();
    }
}

