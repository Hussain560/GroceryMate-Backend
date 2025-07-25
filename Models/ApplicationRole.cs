using Microsoft.AspNetCore.Identity;

namespace GroceryMateApi.Models
{
    public class ApplicationRole : IdentityRole<int>
    {
        public ApplicationRole() : base() { }

        public ApplicationRole(string roleName) : base(roleName) { }

        public virtual ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    }
}
