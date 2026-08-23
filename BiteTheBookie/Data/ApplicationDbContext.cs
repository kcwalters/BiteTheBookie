using BiteTheBookie.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BiteTheBookie.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<ExpertPick> ExpertPicks { get; set; }
        public DbSet<ExpertPickView> ExpertPickViews { get; set; }
        public DbSet<ExpertPost> ExpertPosts { get; set; }
        public DbSet<GameSimulation> GameSimulations { get; set; }
        public DbSet<SiteVideo> SiteVideos { get; set; }

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

            builder.Entity<ExpertPickView>(entity =>
            {
                entity.HasIndex(e => new { e.UserId, e.WeekStartUtc });
                entity.HasIndex(e => new { e.UserId, e.GameId, e.WeekStartUtc }).IsUnique();
                entity.Property(e => e.ViewedAtUtc)
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
        }
    }
}