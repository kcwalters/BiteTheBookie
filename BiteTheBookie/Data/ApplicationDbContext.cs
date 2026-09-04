using BiteTheBookie.Models;
using BiteTheBookie.Models.Fantasy;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BiteTheBookie.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
        public DbSet<ExpertPick> ExpertPicks { get; set; }
        public DbSet<ExpertPost> ExpertPosts { get; set; }
        public DbSet<GameSimulation> GameSimulations { get; set; }
        public DbSet<SiteVideo> SiteVideos { get; set; }
        public DbSet<FantasyContest> FantasyContests { get; set; }
        public DbSet<FantasyPlayer> FantasyPlayers { get; set; }
        public DbSet<FantasyEntry> FantasyEntries { get; set; }
        public DbSet<FantasyEntrySlot> FantasyEntrySlots { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(e => e.FirstName).HasMaxLength(100);
                entity.Property(e => e.LastName).HasMaxLength(100);
                entity.Property(e => e.SubscriptionTier)
                      .HasConversion<int>()
                      .HasDefaultValue(SubscriptionTier.Free);
                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("GETUTCDATE()");
            });

            builder.Entity<ExpertPick>(entity =>
            {
                entity.HasIndex(e => e.GameId);
                entity.HasIndex(e => e.League);
                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("GETUTCDATE()");
            });

            builder.Entity<GameSimulation>(entity =>
            {
                entity.HasIndex(e => e.GameId);
                entity.HasIndex(e => e.GeneratedAt);
                entity.Property(e => e.SimulationContent).HasColumnType("nvarchar(max)");
                entity.Property(e => e.GeneratedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            builder.Entity<SiteVideo>(entity =>
            {
                entity.HasIndex(e => e.IsPublished);
                entity.HasIndex(e => e.IsFeatured);
                entity.HasIndex(e => e.SortOrder);
                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("GETUTCDATE()");
            });

            // Expert posts for our experts/authors
            builder.Entity<ExpertPost>(entity =>
            {
                entity.HasIndex(e => e.IsPublished);
                entity.HasIndex(e => e.AuthorId);
                entity.HasOne(e => e.Author)
                      .WithMany()
                      .HasForeignKey(e => e.AuthorId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.Content).HasColumnType("nvarchar(max)");
            });

            builder.Entity<FantasyContest>(entity =>
            {
                entity.HasIndex(e => e.SlateKey);
                entity.HasIndex(e => e.League);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            builder.Entity<FantasyPlayer>(entity =>
            {
                entity.HasIndex(e => e.FantasyContestId);
                entity.HasIndex(e => e.Position);
                entity.Property(e => e.FantasyPoints).HasColumnType("decimal(8,2)");
                entity.HasOne(e => e.FantasyContest)
                      .WithMany(c => c.Players)
                      .HasForeignKey(e => e.FantasyContestId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<FantasyEntry>(entity =>
            {
                entity.HasIndex(e => new { e.FantasyContestId, e.UserId });
                entity.Property(e => e.TotalPoints).HasColumnType("decimal(8,2)");
                entity.Property(e => e.SubmittedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasOne(e => e.FantasyContest)
                      .WithMany(c => c.Entries)
                      .HasForeignKey(e => e.FantasyContestId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<FantasyEntrySlot>(entity =>
            {
                entity.HasIndex(e => e.FantasyEntryId);
                entity.HasOne(e => e.FantasyEntry)
                      .WithMany(en => en.Slots)
                      .HasForeignKey(e => e.FantasyEntryId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.FantasyPlayer)
                      .WithMany()
                      .HasForeignKey(e => e.FantasyPlayerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}