using GroceryMateApi.Models;

namespace GroceryMateApi.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalProducts { get; set; }
        public int LowStockCount { get; set; }
        public decimal TodaysSales { get; set; }
        public List<InventoryTransaction> RecentTransactions { get; set; } = new();
    }
}
