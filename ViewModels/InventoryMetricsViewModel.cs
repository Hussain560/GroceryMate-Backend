using System;
using System.Collections.Generic;

namespace GroceryMateApi.ViewModels
{
    public class InventoryMetricsViewModel
    {
        public int LowStockCount { get; set; }
        public decimal TurnoverRate { get; set; }
        public int OverstockCount { get; set; }
        public double AvgStockAge { get; set; }
        public Dictionary<string, decimal> StockValue { get; set; } = new();
        public double RestockFrequency { get; set; }
        public List<ProductSummary> TopSlowMovingProducts { get; set; } = new();
        public int OutageRiskCount { get; set; }
        public decimal ExpiredValue { get; set; }
    }

    public class ProductSummary
    {
        public string ProductName { get; set; } = string.Empty;
        public DateTime ExpirationDate { get; set; }
        public DateTime? LastSoldDate { get; set; }
    }
}
