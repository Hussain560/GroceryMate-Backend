using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroceryMateApi.Models
{
    public class ApplicationUser : IdentityUser<int>
    {
        public string FullName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
        public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();

        [ForeignKey("Role")]
        public int? RoleID { get; set; } // Nullable if not all users have roles
        public ApplicationRole? Role { get; set; } = null!;
    }
}
