using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace NotesAPI.Models
{
    [Table("notes")]
    public class Note
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("title")]
        [MaxLength(255)]
        public string? Title { get; set; }

        [Column("textContents")]
        public string? TextContents { get; set; }

        [Column("drawings")]
        public string? Drawings { get; set; } // JSON string

        [Column("checklistItems")]
        public string? ChecklistItems { get; set; } // JSON string

        [Column("formatting")]
        public string? Formatting { get; set; } // JSON string

        [Column("createdAt")]
     
        public DateTime? CreatedAt { get; set; }

        [Column("lastAccessed")]

        public DateTime? LastAccessed { get; set; }

        [Column("isArchived")]
        public bool IsArchived { get; set; }

        [Column("isPrivate")]
        public bool IsPrivate { get; set; }

        [Column("creatorUserId")]
        public int CreatorUserId { get; set; }
    }
}
