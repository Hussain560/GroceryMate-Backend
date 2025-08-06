using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroceryMateApi.Models
{
    public class Product
    {
        [Key]
        public int ProductID { get; set; }

        [Required]
        [StringLength(100)]
        public string ProductName { get; set; } = string.Empty;

        [ForeignKey("Category")]
        public int CategoryID { get; set; }
        public Category Category { get; set; } = null!;

        [ForeignKey("Supplier")]
        public int SupplierID { get; set; }
        public Supplier Supplier { get; set; } = null!;

        [ForeignKey("Brand")]
        public int BrandID { get; set; }
        public Brand Brand { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal DiscountPercentage { get; set; }

        public int? TotalStockQuantity { get; set; }

        public int ReorderLevel { get; set; }

        [StringLength(50)]
        public string? Barcode { get; set; }

        [StringLength(255)]
        public string? ImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<SaleDetail> SaleDetails { get; set; } = new List<SaleDetail>();
        public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
        public ICollection<ProductBatch> ProductBatches { get; set; } = new List<ProductBatch>();
    }
}
