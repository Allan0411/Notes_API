using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesAPI.Data;
using NotesAPI.Models;
using System.Security.Claims;

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

     
        // DTO for request
        public class ChangeNameRequest
        {
            public string NewUsername { get; set; } = string.Empty;
        }


    }
}
