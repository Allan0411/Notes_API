using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesAPI.Data;
using NotesAPI.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NotesAPI.Controllers
{
    public class UpdateRoleRequest
    {
        public string Role { get; set; } = "";
    }
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotesUserController : ControllerBase
    {
        private readonly NotesDbContext _context;



        public NotesUserController(NotesDbContext context)
        {
            _context = context;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            return int.Parse(userIdClaim.Value);
        }

        // GET: api/NotesUser/{noteId}/collaborators
        [HttpGet("{noteId}/collaborators")]
        public async Task<IActionResult> GetCollaborators(int noteId)
        {
            int userId = GetUserId();

            var note = await _context.Notes
                .FirstOrDefaultAsync(n => n.Id == noteId &&
                    (_context.NotesUser.Any(nu => nu.NoteId == n.Id && nu.UserId == userId) ||
                     n.CreatorUserId == userId));

            if (note == null) return NotFound("Note not found or not permitted.");

            var collaborators = await _context.NotesUser
                .Where(nu => nu.NoteId == noteId)
                .Select(nu => new
                {
                    nu.Id,
                    nu.NoteId,
                    nu.UserId,
                    nu.Role
                })
                .ToListAsync();

            return Ok(collaborators);

        }



        // POST: api/NotesUser/{noteId}/collaborators
        [HttpPost("{noteId}/collaborators")]
        public async Task<IActionResult> AddCollaborator(int noteId, [FromBody] AddCollaboratorRequest request)
        {
            int userId = GetUserId();

            var note = await _context.Notes
                .FirstOrDefaultAsync(n => n.Id == noteId && n.CreatorUserId == userId);

            if (note == null) return NotFound("Note not found or not owner");

            if (request.UserId == userId)
                return BadRequest("Cannot add yourself as collaborator.");

            var existing = await _context.NotesUser
                .FirstOrDefaultAsync(nu => nu.NoteId == noteId && nu.UserId == request.UserId);

            if (existing != null)
                return BadRequest("User is already a collaborator.");

            var newCollaborator = new NotesUser
            {
                NoteId = noteId,
                UserId = request.UserId,
                Role = request.Role ?? "editor"
            };

            _context.NotesUser.Add(newCollaborator);
            await _context.SaveChangesAsync();

            return Ok(newCollaborator);
        }

        // DELETE: api/NotesUser/{noteId}/collaborators/{collaboratorUserId}
        [HttpDelete("{noteId}/collaborators/{collaboratorUserId}")]
        public async Task<IActionResult> RemoveCollaborator(int noteId, int collaboratorUserId)
        {
            int userId = GetUserId();

            var note = await _context.Notes
                .FirstOrDefaultAsync(n => n.Id == noteId && n.CreatorUserId == userId);

            if (note == null) return NotFound("Note not found or not owner");

            var collab = await _context.NotesUser
                .FirstOrDefaultAsync(nu => nu.NoteId == noteId && nu.UserId == collaboratorUserId);

            if (collab == null) return NotFound("Collaborator not found.");
            _context.NotesUser.Remove(collab);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // PUT update collaborator role (new)
        [HttpPut("{noteId}/collaborators/{collaboratorUserId}/role")]
        public async Task<IActionResult> UpdateCollaboratorRole(int noteId, int collaboratorUserId, [FromBody] UpdateRoleRequest request)
        {
            int userId = GetUserId();

            // Ensure logged in user is the note owner
            var note = await _context.Notes
                .FirstOrDefaultAsync(n => n.Id == noteId && n.CreatorUserId == userId);

            if (note == null) return NotFound("Note not found or not owner");

            var collaborator = await _context.NotesUser
                .FirstOrDefaultAsync(nu => nu.NoteId == noteId && nu.UserId == collaboratorUserId);

            if (collaborator == null) return NotFound("Collaborator not found.");

            if (string.IsNullOrEmpty(request.Role))
                return BadRequest("Role must be provided.");

            collaborator.Role = request.Role.ToLower();

            _context.NotesUser.Update(collaborator);
            await _context.SaveChangesAsync();

            return Ok(collaborator);
        }

        [HttpGet("collaborations")]
        public async Task<IActionResult> GetCollaboratedNoteIds()
        {
            int userId = GetUserId();

            // Get IDs of notes where the user is collaborator but not the owner
            var collaboratedNoteIds = await _context.NotesUser
                .Where(nu => nu.UserId == userId)
                .Join(_context.Notes,
                      nu => nu.NoteId,
                      n => n.Id,
                      (nu, n) => new { nu.NoteId, n.CreatorUserId })
                .Where(x => x.CreatorUserId != userId)
                .Select(x => x.NoteId)
                .Distinct()
                .ToListAsync();

            return Ok(collaboratedNoteIds);
        }

    }

    // DTO for AddCollaboratorRequest since you are using it in AddCollaborator
    public class AddCollaboratorRequest
    {
        public int UserId { get; set; }
        public string? Role { get; set; }
    }
    
}
