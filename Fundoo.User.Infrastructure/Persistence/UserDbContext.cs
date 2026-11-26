using Fundoo.User.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fundoo.User.Infrastructure.Persistence;

public class UserDbContext(DbContextOptions<UserDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.HasIndex(u => u.NormalizedEmail).IsUnique();
            e.Property(u => u.Email).HasMaxLength(256);
            e.Property(u => u.NormalizedEmail).HasMaxLength(256);
        });

        b.Entity<UserSession>(e =>
        {
            e.HasKey(s => s.Id);

            e.HasIndex(s => s.RefreshTokenHash)
                .IsUnique();

            e.HasIndex(s => s.PreviousRefreshTokenHash);

            e.HasIndex(s => s.UserId);

            e.Property(s => s.RefreshTokenHash)
                .IsRequired();

            e.Property(s => s.Version)
                .IsConcurrencyToken();

            e.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
