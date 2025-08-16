using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroceryMateApi.Models
{
    public class InventoryTransaction
    {
        [Key]
        public int TransactionID { get; set; }

        [Required]
        public int ProductID { get; set; } // Now references Product directly
        public Product Product { get; set; } = null!;

        [Required]
        [StringLength(20)]
        public string TransactionType { get; set; } = string.Empty;

        public int Quantity { get; set; }

        [StringLength(50)]
        public string? ReferenceNumber { get; set; }

        public DateTime TransactionDate { get; set; }

        [StringLength(255)]
        public string? Notes { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;
    }
}