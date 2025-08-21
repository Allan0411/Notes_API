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

    }
}
