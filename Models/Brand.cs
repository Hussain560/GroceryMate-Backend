using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroceryMateApi.Models
{
    public class Brand
    {
        [Key]
        public int BrandID { get; set; }

        [Required]
        [StringLength(100)]
        public string BrandName { get; set; } = string.Empty;

        [StringLength(255)]
        public string? ImageUrl { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
