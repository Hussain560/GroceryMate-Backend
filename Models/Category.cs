using System.ComponentModel.DataAnnotations;

namespace GroceryMateApi.Models
{
    public class Category
    {
        public int CategoryID { get; set; }
        
        [Required]
        [StringLength(50)]
        public string CategoryName { get; set; } = string.Empty;
        
        [StringLength(200)]
        public string? Description { get; set; }

        public string? ImagePath { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
