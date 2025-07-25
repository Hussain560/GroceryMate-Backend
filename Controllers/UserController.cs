using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GroceryMateApi.Data;
using GroceryMateApi.Models;
using GroceryMateApi.ViewModels;

namespace GroceryMateApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Manager")]
    public class UserController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly GroceryStoreContext _context;

        public UserController(UserManager<ApplicationUser> userManager, 
                             RoleManager<ApplicationRole> roleManager,
                             GroceryStoreContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                var users = await _userManager.Users.ToListAsync();
                var userViewModels = new List<UserViewModel>();

                foreach (var user in users)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    userViewModels.Add(new UserViewModel
                    {
                        UserId = user.Id,
                        Username = user.UserName ?? string.Empty,
                        FullName = user.FullName,
                        Email = user.Email ?? string.Empty,
                        Phone = user.PhoneNumber ?? string.Empty,
                        Role = roles.FirstOrDefault() ?? string.Empty
                    });
                }

                return Ok(new { success = true, data = userViewModels });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Error retrieving users" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id.ToString());
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                var roles = await _userManager.GetRolesAsync(user);
                var viewModel = new UserViewModel
                {
                    UserId = user.Id,
                    Username = user.UserName ?? string.Empty,
                    FullName = user.FullName,
                    Email = user.Email ?? string.Empty,
                    Phone = user.PhoneNumber ?? string.Empty,
                    Role = roles.FirstOrDefault() ?? string.Empty
                };

                return Ok(new { success = true, data = viewModel });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Error retrieving user" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                var user = new ApplicationUser
                {
                    UserName = request.Email,
                    Email = request.Email,
                    FullName = request.FullName,
                    PhoneNumber = request.Phone,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, request.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, request.Role);
                    return Ok(new { success = true, message = "User created successfully" });
                }

                return BadRequest(new { success = false, errors = result.Errors.Select(e => e.Description) });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Error creating user" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id.ToString());
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                user.FullName = request.FullName;
                user.Email = request.Email;
                user.PhoneNumber = request.Phone;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    return BadRequest(new { success = false, errors = result.Errors.Select(e => e.Description) });
                }

                // Update role if changed
                var currentRoles = await _userManager.GetRolesAsync(user);
                if (!currentRoles.Contains(request.Role))
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    await _userManager.AddToRoleAsync(user, request.Role);
                }

                return Ok(new { success = true, message = "User updated successfully" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Error updating user" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id.ToString());
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    return BadRequest(new { success = false, errors = result.Errors.Select(e => e.Description) });
                }

                return Ok(new { success = true, message = "User deleted successfully" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Error deleting user" });
            }
        }

        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            try
            {
                var roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                return Ok(new { success = true, data = roles });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Error retrieving roles" });
            }
        }
    }

    public class CreateUserRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Role { get; set; } = "";
    }

    public class UpdateUserRequest
    {
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Role { get; set; } = "";
    }
}
