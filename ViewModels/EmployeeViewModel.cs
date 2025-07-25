using GroceryMateApi.Models;

namespace GroceryMateApi.ViewModels
{
    public class EmployeeViewModel
    {
        public string FullName { get; set; } = string.Empty;
        public int TodaysSalesCount { get; set; }
        public decimal TodaysSalesAmount { get; set; }
        public List<RecentSaleViewModel> RecentSales { get; set; } = new();
    }
}
