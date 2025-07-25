using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GroceryMateApi.Models;

namespace GroceryMateApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;

        public AuthController(UserManager<ApplicationUser> userManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password))
                    return BadRequest(new { success = false, message = "Username and password are required" });

                var user = await _userManager.FindByEmailAsync(model.Username);
                if (user == null)
                    return Unauthorized(new { success = false, message = "Invalid credentials" });

                if (!await _userManager.CheckPasswordAsync(user, model.Password))
                    return Unauthorized(new { success = false, message = "Invalid credentials" });

                var roles = await _userManager.GetRolesAsync(user);
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.UserName ?? ""),
                    new Claim(ClaimTypes.Email, user.Email ?? ""),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
                };

                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }

                var jwtKey = _configuration["Jwt:Key"] ??
                    throw new InvalidOperationException("JWT Key is not configured");
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var expires = DateTime.Now.AddDays(7); // Extend token validity to 7 days for testing

                var token = new JwtSecurityToken(
                    issuer: _configuration["Jwt:Issuer"],
                    audience: _configuration["Jwt:Audience"],
                    claims: claims,
                    expires: expires,
                    signingCredentials: creds);

                return Ok(new
                {
                    success = true,
                    token = new JwtSecurityTokenHandler().WriteToken(token),
                    user = new
                    {
                        id = user.Id,
                        username = user.UserName,
                        email = user.Email,
                        fullName = user.FullName,
                        roles = roles
                    }
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "Error processing login" });
            }
        }

        [HttpGet("validate")]
        public IActionResult ValidateToken()
        {
            try
            {
                var user = User.Identity;
                if (user == null || !user.IsAuthenticated)
                    return Unauthorized(new { success = false, message = "Invalid token" });

                return Ok(new { success = true, message = "Token is valid" });
            }
            catch
            {
                return Unauthorized(new { success = false, message = "Invalid token" });
            }
        }
    }

    public class LoginModel
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
