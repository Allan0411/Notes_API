using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotesAPI.Data;
using NotesAPI.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CollaborationInvitesController : ControllerBase
{
    private readonly NotesDbContext _context;

    public CollaborationInvitesController(NotesDbContext context)
    {
        _context = context;
    }
    public static DateTime GetIndianStandardTime()
    {
        // IST is UTC + 5 hours 30 minutes
        return DateTime.UtcNow.AddHours(5).AddMinutes(30);
    }

    private int GetUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

    [HttpPost]
    public async Task<IActionResult> SendInvite([FromBody] CollaborationInvite invite)
    {
        int userId = GetUserId();

        // Check permission: user owns the note
        var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == invite.NoteId && n.CreatorUserId == userId);
        if (note == null)
            return Forbid("You do not have permission to invite collaborators for this note.");

        // Check if an invite already exists for this user and note
        var existingInvite = await _context.CollaborationInvites
            .FirstOrDefaultAsync(ci => ci.NoteId == invite.NoteId && ci.InvitedUserId == invite.InvitedUserId);

        if (existingInvite != null)
        {
            if (existingInvite.Status == "Pending")
            {
                return BadRequest("An invite is already pending for this user.");
            }
            else if (existingInvite.Status == "Accepted")
            {
                return BadRequest("This user has already accepted the collaboration invite.");
            }
            else if (existingInvite.Status == "Declined")
            {
                // Allow resending if previously declined
                // Update the existing invite instead of creating a new one
                existingInvite.InviterUserId = userId;
                existingInvite.Status = "Pending";
                existingInvite.SentAt = GetIndianStandardTime();
                existingInvite.RespondedAt = null; // Clear previous response timestamp
                existingInvite.Role = invite.Role; // Update role if needed

                await _context.SaveChangesAsync();
                return Ok(existingInvite);
            }
        }

        // Create new invite if none exists
        invite.InviterUserId = userId;
        invite.Status = "Pending";
        invite.SentAt = GetIndianStandardTime();
        invite.RespondedAt = null; // Ensure it's null for new invites

        _context.CollaborationInvites.Add(invite);
        await _context.SaveChangesAsync();

        return Ok(invite);
    }


    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingInvites()
    {
        int userId = GetUserId();

        var invites = await _context.CollaborationInvites
            .Where(ci => ci.InvitedUserId == userId && ci.Status == "Pending")
            .ToListAsync();

        return Ok(invites);
    }

    [HttpPost("{inviteId}/respond")]
    public async Task<IActionResult> RespondToInvite(int inviteId, [FromBody] bool accept)
    {
        int userId = GetUserId();

        var invite = await _context.CollaborationInvites.FirstOrDefaultAsync(ci => ci.InviteId == inviteId && ci.InvitedUserId == userId);
        if (invite == null) return NotFound();

        if (invite.Status != "Pending") return BadRequest("Invite already responded to.");

        invite.Status = accept ? "Accepted" : "Declined";
        invite.RespondedAt = GetIndianStandardTime();

        if (accept)
        {
            // Add collaborator record
            _context.NotesUser.Add(new NotesUser
            {
                NoteId = invite.NoteId,
                UserId = userId,
                Role = invite.Role ?? "editor"
            });
        }

        await _context.SaveChangesAsync();
        return Ok(invite);
    }
}
