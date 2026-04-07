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
        public DbSet<GameSimulation> GameSimulations { get; set; }

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
        }
    }
}