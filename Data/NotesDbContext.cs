using Microsoft.EntityFrameworkCore;
using NotesAPI.Models;
using System;

namespace NotesAPI.Data
{
    public partial class NotesDbContext : DbContext
    {
        public NotesDbContext() { }
        public NotesDbContext(DbContextOptions<NotesDbContext> options)
            : base(options) { }

        public virtual DbSet<Note> Notes { get; set; } = null!;
        public virtual DbSet<User> Users { get; set; } = null!;
        public virtual DbSet<NotesUser> NotesUser { get; set; } = null!;
        public virtual DbSet<Attachment> Attachments { get; set; } = null!;
        public virtual DbSet<AIAction> AIActions { get; set; } = null!;
        public virtual  DbSet<CollaborationInvite> CollaborationInvites { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Use configuration file or environment for connection string.
                // optionsBuilder.UseMySql(
                //     configuration.GetConnectionString("DefaultConnection"),
                //     ServerVersion.AutoDetect(configuration.GetConnectionString("DefaultConnection"))
                // );
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.UseCollation("utf8mb4_general_ci")
                        .HasCharSet("utf8mb4");

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id")
                    .HasColumnType("int(11)");
                entity.Property(e => e.Email).HasColumnName("email")
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.PasswordHash).HasColumnName("password_hash")
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.Username).HasColumnName("username")
                    .HasMaxLength(100);
            });

            modelBuilder.Entity<Note>(entity =>
            {
                entity.ToTable("notes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id")
                    .HasColumnType("int(11)");
                entity.Property(e => e.Title).HasColumnName("title")
                    .HasMaxLength(255);
                entity.Property(e => e.TextContents).HasColumnName("textContents")
                    .HasColumnType("text");

                // JSON columns (critical for dynamic JSON support)
                entity.Property(e => e.Drawings).HasColumnName("drawings").HasColumnType("json");
                entity.Property(e => e.ChecklistItems).HasColumnName("checklistItems").HasColumnType("json");
                entity.Property(e => e.Formatting).HasColumnName("formatting").HasColumnType("json");

                entity.Property(e => e.CreatedAt).HasColumnName("createdAt")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("current_timestamp()");
                entity.Property(e => e.LastAccessed).HasColumnName("lastAccessed")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("current_timestamp()");
                entity.Property(e => e.IsArchived).HasColumnName("isArchived")
                    .HasColumnType("bit")
                    .HasDefaultValue(false);
                entity.Property(e => e.IsPrivate).HasColumnName("isPrivate")
                    .HasColumnType("bit")
                    .HasDefaultValue(false);
                entity.Property(e => e.CreatorUserId).HasColumnName("creatorUserId")
                    .HasColumnType("int(11)");
            });

            modelBuilder.Entity<NotesUser>(entity =>
            {
                entity.ToTable("notes_users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id")
                    .HasColumnType("int(11)");
                entity.Property(e => e.NoteId).HasColumnName("noteId");
                entity.Property(e => e.UserId).HasColumnName("userId");
                entity.Property(e => e.Role).HasColumnName("role")
                    .HasMaxLength(20)
                    .HasDefaultValue("editor");
            });

            modelBuilder.Entity<Attachment>(entity =>
            {
                entity.ToTable("attachments");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id")
                    .HasColumnType("int(11)");
                entity.Property(e => e.NoteId).HasColumnName("noteId");
                entity.Property(e => e.AttachmentType).HasColumnName("attachmentType")
                    .HasMaxLength(50);
                entity.Property(e => e.StoragePath).HasColumnName("storagePath")
                    .HasMaxLength(500);
                entity.Property(e => e.CreatedAt).HasColumnName("createdAt")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("current_timestamp()");
                entity.Property(e => e.CreatedByUserId).HasColumnName("createdByUserId");
            });

            modelBuilder.Entity<AIAction>(entity =>
            {
                entity.ToTable("ai_actions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id")
                    .HasColumnType("int(11)");
                entity.Property(e => e.NoteId).HasColumnName("noteId");
                entity.Property(e => e.UserId).HasColumnName("userId");
                entity.Property(e => e.ActionType).HasColumnName("actionType")
                    .HasMaxLength(50);
                entity.Property(e => e.InputData).HasColumnName("inputData")
                    .HasColumnType("text");
                entity.Property(e => e.OutputData).HasColumnName("outputData")
                    .HasColumnType("text");
                entity.Property(e => e.CreatedAt).HasColumnName("createdAt")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("current_timestamp()");
            });

            modelBuilder.Entity<CollaborationInvite>(entity =>
            {
                entity.ToTable("CollaborationInvites");

                entity.HasKey(e => e.InviteId);

                entity.Property(e => e.InviteId).HasColumnName("InviteId").HasColumnType("int").ValueGeneratedOnAdd();

                entity.Property(e => e.NoteId).HasColumnName("NoteId").IsRequired();

                entity.Property(e => e.InvitedUserId).HasColumnName("InvitedUserId").IsRequired();

                entity.Property(e => e.InviterUserId).HasColumnName("InviterUserId").IsRequired();

                entity.Property(e => e.Role)
                      .HasColumnName("Role")
                      .HasMaxLength(50)
                      .IsRequired()
                      .HasDefaultValue("editor");

                entity.Property(e => e.Status)
                      .HasColumnName("Status")
                      .HasMaxLength(20)
                      .IsRequired()
                      .HasDefaultValue("Pending");

                entity.Property(e => e.SentAt)
                      .HasColumnName("SentAt")
                      .HasColumnType("timestamp")
                      .HasDefaultValueSql("CURRENT_TIMESTAMP")
                      .IsRequired();

                entity.Property(e => e.RespondedAt)
                      .HasColumnName("RespondedAt")
                      .HasColumnType("timestamp")
                      .IsRequired(false);

                // Add foreign keys if you want here, EF Core can infer based on navigation too
                // entity.HasOne<Note>().WithMany().HasForeignKey(e => e.NoteId);
                // entity.HasOne<User>().WithMany().HasForeignKey(e => e.InvitedUserId);
                // entity.HasOne<User>().WithMany().HasForeignKey(e => e.InviterUserId);
            });


            OnModelCreatingPartial(modelBuilder);
        }
        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
