using GroceryMateApi.Models;

namespace GroceryMateApi.ViewModels
{
    public class DashboardMetricsViewModel
    {
        public int SalesTransactions { get; set; }
        public decimal NetSales { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal ReturnAmount { get; set; }
        public decimal AverageTransactionValue { get; set; }
        public decimal SalesGrowthRate { get; set; }
        public decimal OperationalEfficiencyRatio { get; set; }

        public IEnumerable<TopProductViewModel> TopProducts { get; set; } = new List<TopProductViewModel>();
        public IEnumerable<TopPaymentViewModel> TopPayments { get; set; } = new List<TopPaymentViewModel>();
        public IEnumerable<TopCategoryViewModel> TopCategories { get; set; } = new List<TopCategoryViewModel>();
    }

    public class TopProductViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public decimal NetSales { get; set; }
        public int Quantity { get; set; }
    }

    public class TopPaymentViewModel
    {
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal NetIncome { get; set; }
        public int TransactionCount { get; set; }
    }

    public class TopCategoryViewModel
    {
        public string CategoryName { get; set; } = string.Empty;
        public int SalesVolume { get; set; }
        public decimal NetSales { get; set; }
    }
}
