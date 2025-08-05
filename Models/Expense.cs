using System.ComponentModel.DataAnnotations;

namespace GroceryMateApi.Models
{
    public class Expense
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }
    }
}
