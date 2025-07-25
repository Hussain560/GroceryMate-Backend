using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroceryMateApi.Models
{
    public class SaleDetail
    {
        [Key]
        public int SaleDetailID { get; set; }

        [ForeignKey("Sale")]
        public int SaleID { get; set; }
        public Sale Sale { get; set; } = null!;

        [ForeignKey("Product")]
        public int ProductID { get; set; }
        public Product Product { get; set; } = null!;

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OriginalUnitPrice { get; set; }  // Price before any discounts

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPriceAfterDiscount { get; set; }  // Add this

        [Column(TypeName = "decimal(5,2)")]
        public decimal DiscountPercentage { get; set; }  // Add this

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineSubtotalBeforeDiscount { get; set; }  // Keep this one

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineDiscountAmount { get; set; }  // Add this

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineSubtotalAfterDiscount { get; set; }  // Add this

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineVATAmount { get; set; }  // Rename from VATAmount

        [Column(TypeName = "decimal(5,2)")]
        public decimal VATPercentage { get; set; } = 15.00M;

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineFinalTotal { get; set; }  // Rename from FinalPrice
    }
}
