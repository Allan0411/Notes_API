using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesAPI.Data;
using NotesAPI.Models;
using System.Security.Claims;
using System.Text.Json;

namespace NotesAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotesController : ControllerBase
    {
        private readonly NotesDbContext _context;

        private int GetUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.Parse(userIdClaim);
        }

        public NotesController(NotesDbContext context)
        {
            _context = context;
        }

        // 1. List all notes user can access (owned/shared, not archived)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Note>>> GetNotes()
        {
            int userId = GetUserId();

            // Owned or shared with user
            var notes = await _context.Notes
                .Where(note =>
                    !note.IsArchived &&
                    (_context.NotesUser.Any(nu => nu.NoteId == note.Id && nu.UserId == userId) ||
                    note.CreatorUserId == userId)
                )
                .ToListAsync();

            return notes;
        }

        // 2. Get single note (if user has access)
        [HttpGet("{id}")]
        public async Task<ActionResult<Note>> GetNote(int id)
        {
            int userId = GetUserId();

            var note = await _context.Notes
                .FirstOrDefaultAsync(n =>
                    n.Id == id &&
                    (_context.NotesUser.Any(nu => nu.NoteId == n.Id && nu.UserId == userId) ||
                     n.CreatorUserId == userId)
                );

            if (note == null)
                return NotFound();

            note.LastAccessed = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return note;
        }
        // Modified CreateNote
        [HttpPost]
        public async Task<ActionResult<Note>> CreateNote([FromBody] Note note)
        {
            int userId = GetUserId();
            note.CreatorUserId = userId;
            note.CreatedAt = DateTime.UtcNow;
            note.LastAccessed = DateTime.UtcNow;

            // Serialize checklistItems, drawings, and formatting if not already a string
            if (note.ChecklistItems != null && !IsJsonString(note.ChecklistItems))
            {
                note.ChecklistItems = JsonSerializer.Serialize(note.ChecklistItems);
            }
            if (note.Drawings != null && !IsJsonString(note.Drawings))
            {
                note.Drawings = JsonSerializer.Serialize(note.Drawings);
            }
            if (note.Formatting != null && !IsJsonString(note.Formatting))
            {
                note.Formatting = JsonSerializer.Serialize(note.Formatting);
            }

            _context.Notes.Add(note);
            await _context.SaveChangesAsync(); // SAVE FIRST! Now note.Id is available

            _context.NotesUser.Add(new NotesUser
            {
                NoteId = note.Id,      // Real, DB-assigned note id
                UserId = userId,
                Role = "owner"
            });
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetNote), new { id = note.Id }, note);
        }

        // Modified UpdateNote
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateNote(int id, [FromBody] Note updatedNote)
        {
            int userId = GetUserId();
            var note = await _context.Notes
                .FirstOrDefaultAsync(n =>
                    n.Id == id &&
                    (_context.NotesUser.Any(nu => nu.NoteId == n.Id && nu.UserId == userId) ||
                    n.CreatorUserId == userId)
                );
            if (note == null) return NotFound();

            note.Title = updatedNote.Title;
            note.TextContents = updatedNote.TextContents;
            note.LastAccessed = DateTime.UtcNow;
            note.IsArchived = updatedNote.IsArchived;
            note.IsPrivate = updatedNote.IsPrivate;

            // Serialize checklistItems, drawings, and formatting if not already a string
            if (updatedNote.ChecklistItems != null && !IsJsonString(updatedNote.ChecklistItems))
            {
                note.ChecklistItems = JsonSerializer.Serialize(updatedNote.ChecklistItems);
            }
            else
            {
                note.ChecklistItems = updatedNote.ChecklistItems;
            }
            if (updatedNote.Drawings != null && !IsJsonString(updatedNote.Drawings))
            {
                note.Drawings = JsonSerializer.Serialize(updatedNote.Drawings);
            }
            else
            {
                note.Drawings = updatedNote.Drawings;
            }
            if (updatedNote.Formatting != null && !IsJsonString(updatedNote.Formatting))
            {
                note.Formatting = JsonSerializer.Serialize(updatedNote.Formatting);
            }
            else
            {
                note.Formatting = updatedNote.Formatting;
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // Helper function
        private bool IsJsonString(string value)
        {
            value = value?.Trim();
            if (string.IsNullOrEmpty(value)) return false;
            return (value.StartsWith("{") && value.EndsWith("}")) ||
                   (value.StartsWith("[") && value.EndsWith("]"));
        }

        //random
        // PATCH api/notes/123/archive
        [HttpPatch("{id}/archive")]
        public async Task<IActionResult> UpdateIsArchived(int id, [FromBody] bool isArchived)
        {
            // Update the field only, e.g. via EF Core's .Property("IsArchived").IsModified = true;
            var note = await _context.Notes.FindAsync(id);
            if (note == null) return NotFound();
            note.IsArchived = isArchived;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // PATCH api/notes/123/private
        [HttpPatch("{id}/private")]
        public async Task<IActionResult> UpdateIsPrivate(int id, [FromBody] bool isPrivate)
        {
            var note = await _context.Notes.FindAsync(id);
            if (note == null) return NotFound();
            note.IsPrivate = isPrivate;
            await _context.SaveChangesAsync();
            return NoContent();
        }



        // 5. Delete a note (only creator allowed)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNote(int id)
        {
            int userId = GetUserId();

            var note = await _context.Notes
                .FirstOrDefaultAsync(n => n.Id == id && n.CreatorUserId == userId);

            if (note == null) return NotFound();

            _context.Notes.Remove(note);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // 6. Add collaborator
        [HttpPost("{noteId}/collaborators")]
        public async Task<IActionResult> AddCollaborator(int noteId, [FromBody] AddCollaboratorRequest request)
        {
            int userId = GetUserId();

            var note = await _context.Notes
                .FirstOrDefaultAsync(n => n.Id == noteId && n.CreatorUserId == userId);

            if (note == null) return NotFound("Note not found or not owner");

            // Prevent adding self as collaborator
            if (request.UserId == userId)
                return BadRequest("Cannot add yourself as collaborator.");

            // Optionally check for existing collaboration
            var existing = await _context.NotesUser
                .FirstOrDefaultAsync(nu => nu.NoteId == noteId && nu.UserId == request.UserId);

            if (existing != null)
                return BadRequest("User is already a collaborator.");

            // Add to join table
            _context.NotesUser.Add(new NotesUser
            {
                NoteId = noteId,
                UserId = request.UserId,
                Role = request.Role ?? "editor"
            });

            await _context.SaveChangesAsync();
            return Ok();
        }


        // 7. Remove collaborator
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

        

        // 10. Export a note (example stub)
        [HttpGet("{id}/export")]
        public async Task<IActionResult> ExportNote(int id)
        {
            int userId = GetUserId();
            var note = await _context.Notes
                .FirstOrDefaultAsync(n => n.Id == id &&
                    (_context.NotesUser.Any(nu => nu.NoteId == n.Id && nu.UserId == userId) ||
                    n.CreatorUserId == userId)
                );

            if (note == null) return NotFound();

            // You'd implement actual export logic—PDF, docx, etc.
            var exportContent = $"Title: {note.Title}\n\nContent:\n{note.TextContents}";
            return File(System.Text.Encoding.UTF8.GetBytes(exportContent), "text/plain", $"note-{id}.txt");
        }
    }
}
