using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesAPI.Data;
using NotesAPI.Models;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;


namespace NotesAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly NotesDbContext _context;

        public UsersController(NotesDbContext context)
        {
            _context = context;
        }

        // GET api/users/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUserById(int id)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Select(u => new {
                    u.Id,
                    u.Username,
                    u.Email
                    // Add other public fields if needed
                })
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return NotFound("User not found");

            return Ok(user);
        }

        // GET api/users/byEmail/{email}
        [HttpGet("byEmail/{email}")]
        public async Task<ActionResult> GetUserByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email is required");

            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.Email == email)
                .Select(u => new {
                    u.Id,
                    u.Username,
                    u.Email
                    // Add other public fields if needed
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound("User not found");

            return Ok(user);
        }


        [HttpPatch("changeName")]
        public async Task<ActionResult> ChangeName([FromBody] ChangeNameRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.NewUsername))
                return BadRequest("New username is required");

            // Get the logged-in user's ID from JWT claims
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound("User not found");

            user.Username = request.NewUsername;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Username updated successfully", user.Username });
        }





        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password)));
        }

        private bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }


        [HttpPatch("changePassword")]
    
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.OldPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest("Both old password and new password are required");

            // Get the logged-in user's ID from JWT claims
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
                return NotFound("User not found");

            // Verify old password using the same method from AuthController
            if (!VerifyPassword(request.OldPassword, user.PasswordHash))
                return BadRequest("Current password is incorrect");

            // Hash the new password using the same method from AuthController
            user.PasswordHash = HashPassword(request.NewPassword);

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Password changed successfully" });
        }




        public class ChangePasswordRequest
        {
            public string OldPassword { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }

        // DTO for request
        public class ChangeNameRequest
        {
            public string NewUsername { get; set; } = string.Empty;
        }


    }
}
