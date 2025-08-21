using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotesAPI.Models
{
    [Table("CollaborationInvites")]
    public class CollaborationInvite
    {
        [Key]
        public int InviteId { get; set; }

        [Required]
        public int NoteId { get; set; }

        [Required]
        public int InvitedUserId { get; set; }

        [Required]
        public int InviterUserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = "editor";

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        [Required]
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public DateTime? RespondedAt { get; set; }

        // Optional: Navigation properties if you want relationships
        // public virtual Note Note { get; set; }
        // public virtual User InvitedUser { get; set; }
        // public virtual User InviterUser { get; set; }
    }
}
