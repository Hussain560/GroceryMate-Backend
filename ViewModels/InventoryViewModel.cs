using GroceryMateApi.Models;

namespace GroceryMateApi.ViewModels
{
    public class InventoryViewModel
    {
        public List<Product> Products { get; set; } = new();
        public List<InventoryTransaction> RecentTransactions { get; set; } = new();
    }
}
