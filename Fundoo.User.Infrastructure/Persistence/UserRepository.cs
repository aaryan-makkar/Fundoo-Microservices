using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fundoo.User.Infrastructure.Persistence;

public class UserRepository(UserDbContext db) : IUserRepository
{
    public Task<AppUser?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

    public Task<AppUser?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task AddAsync(AppUser user, CancellationToken ct = default) =>
        await db.Users.AddAsync(user, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);

    public async Task AddResetTokenAsync(
    PasswordResetToken token,
    CancellationToken ct = default)
    {
        await db.PasswordResetTokens.AddAsync(token, ct);
    }

    public Task<PasswordResetToken?> FindValidResetTokenAsync(
        string tokenHash,
        DateTime currentTime,
        CancellationToken ct = default)
    {
        return db.PasswordResetTokens.FirstOrDefaultAsync(
            t => t.TokenHash == tokenHash
                 && t.UsedAtUtc == null
                 && t.ExpiresAtUtc > currentTime,
            ct);
    }

    public async Task MarkUnusedResetTokensUsedAsync(
        Guid userId,
        DateTime currentTime,
        CancellationToken ct = default)
    {
        var tokens = await db.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedAtUtc == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.UsedAtUtc = currentTime;
        }
    }

    public async Task AddSessionAsync(
    UserSession session,
    CancellationToken ct = default)
    {
        await db.UserSessions.AddAsync(session, ct);
    }

    public Task<UserSession?> FindSessionByIdAsync(
        Guid sessionId,
        CancellationToken ct = default)
    {
        return db.UserSessions.FirstOrDefaultAsync(
            s => s.Id == sessionId,
            ct);
    }

    public Task<UserSession?> FindSessionByRefreshTokenHashAsync(
        string tokenHash,
        CancellationToken ct = default)
    {
        return db.UserSessions.FirstOrDefaultAsync(
            s => s.RefreshTokenHash == tokenHash,
            ct);
    }

    public Task<UserSession?> FindSessionByPreviousRefreshTokenHashAsync(
        string tokenHash,
        CancellationToken ct = default)
    {
        return db.UserSessions.FirstOrDefaultAsync(
            s => s.PreviousRefreshTokenHash == tokenHash,
            ct);
    }

    public async Task<bool> TryRotateRefreshTokenAsync(
        Guid sessionId,
        string expectedTokenHash,
        int expectedVersion,
        string replacementTokenHash,
        DateTime currentTime,
        CancellationToken ct = default)
    {
        var updated = await db.UserSessions
            .Where(s => s.Id == sessionId &&
                        s.RefreshTokenHash == expectedTokenHash &&
                        s.Version == expectedVersion &&
                        s.RevokedAtUtc == null &&
                        s.ExpiresAtUtc > currentTime)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.PreviousRefreshTokenHash, expectedTokenHash)
                .SetProperty(s => s.RefreshTokenHash, replacementTokenHash)
                .SetProperty(s => s.LastUsedAtUtc, currentTime)
                .SetProperty(s => s.Version, expectedVersion + 1), ct);

        return updated == 1;
    }

    public async Task<IReadOnlyList<UserSession>> GetSessionsByUserIdAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await db.UserSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.LastUsedAtUtc ?? s.CreatedAtUtc)
            .ToListAsync(ct);
    }
}
