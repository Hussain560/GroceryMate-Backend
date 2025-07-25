using System.ComponentModel.DataAnnotations;

namespace GroceryMateApi.Models
{
    public class Supplier
    {
        [Key]
        public int SupplierID { get; set; }

        [Required]
        [StringLength(100)]
        public string SupplierName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? ContactName { get; set; }

        [Phone]
        public string? Phone { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
