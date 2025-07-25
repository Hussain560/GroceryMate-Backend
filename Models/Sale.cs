using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroceryMateApi.Models
{
    public class Sale
    {
        [Key]
        public int SaleID { get; set; }

        [ForeignKey("User")]
        public int UserID { get; set; }
        public ApplicationUser User { get; set; } = null!;

        public DateTime SaleDate { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(18,2)")]
        public decimal FinalTotal { get; set; }  // Keep this one as it's more descriptive

        [Required]
        [StringLength(10)]
        public string PaymentMethod { get; set; } = "Cash"; // Default to Cash

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CashReceived { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Change { get; set; }

        [Required]
        [StringLength(20)]
        public string InvoiceNumber { get; set; } = string.Empty;  // Initialize with empty string

        public string? CustomerName { get; set; }

        public string? CustomerPhone { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubtotalBeforeDiscount { get; set; }  // Add this

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalDiscountPercentage { get; set; }  // Add this

        [Column(TypeName = "decimal(5,2)")]
        public decimal TotalDiscountAmount { get; set; }  // Rename from TotalDiscount

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubtotalAfterDiscount { get; set; }  // Add this

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalVATAmount { get; set; }  // Rename from TotalVAT

        [Column(TypeName = "decimal(5,2)")]
        public decimal VATPercentage { get; set; } = 15.00M;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<SaleDetail> SaleDetails { get; set; } = new List<SaleDetail>();

        public Invoice? Invoice { get; set; }
    }
}
