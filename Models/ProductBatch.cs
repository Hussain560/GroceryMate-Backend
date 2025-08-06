using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroceryMateApi.Models
{
    public class ProductBatch
    {
        [Key]
        public int BatchID { get; set; }

        [Required]
        public int ProductID { get; set; }
        public Product Product { get; set; } = null!;

        [Required]
        public int StockQuantity { get; set; }

        [Required]
        public DateTime ExpirationDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    }
}