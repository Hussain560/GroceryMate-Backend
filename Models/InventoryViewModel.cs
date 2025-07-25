using System.Collections.Generic;

namespace GroceryMateApi.Models
{
    public class InventoryViewModel
    {
        public IEnumerable<Product> Products { get; set; } = new List<Product>();
        public IEnumerable<InventoryTransaction> RecentTransactions { get; set; } = new List<InventoryTransaction>();
    }
}
