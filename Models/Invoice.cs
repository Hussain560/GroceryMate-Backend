using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroceryMateApi.Models
{
    public class Invoice
    {
        [Key]
        public int InvoiceID { get; set; }

        [Required]
        [StringLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        [Column("SaleId")]  // Add explicit column name
        public int SaleID { get; set; }

        [ForeignKey("SaleID")]
        public virtual Sale Sale { get; set; } = null!;

        public DateTime CreatedDate { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = string.Empty;

        [StringLength(255)]
        public string? PdfPath { get; set; }
    }
}

