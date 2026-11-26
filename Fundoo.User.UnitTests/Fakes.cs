using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entities;

namespace Fundoo.User.UnitTests;

public class FakeUserRepository : IUserRepository
{
    public List<AppUser> Users { get; } = new();

    public List<PasswordResetToken> ResetTokens { get; } = new();

    public List<UserSession> Sessions { get; } = new();

    public Task<AppUser?> FindByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken ct = default) =>
        Task.FromResult(
            Users.FirstOrDefault(u => u.NormalizedEmail == normalizedEmail));

    public Task<AppUser?> FindByIdAsync(
        Guid id,
        CancellationToken ct = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task AddAsync(AppUser user, CancellationToken ct = default)
    {
        Users.Add(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task AddResetTokenAsync(
        PasswordResetToken token,
        CancellationToken ct = default)
    {
        ResetTokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<PasswordResetToken?> FindValidResetTokenAsync(
        string tokenHash,
        DateTime currentTime,
        CancellationToken ct = default)
    {
        return Task.FromResult(
            ResetTokens.FirstOrDefault(t =>
                t.TokenHash == tokenHash &&
                t.UsedAtUtc == null &&
                t.ExpiresAtUtc > currentTime));
    }

    public Task MarkUnusedResetTokensUsedAsync(
        Guid userId,
        DateTime currentTime,
        CancellationToken ct = default)
    {
        foreach (var token in ResetTokens.Where(
                     t => t.UserId == userId && t.UsedAtUtc == null))
        {
            token.UsedAtUtc = currentTime;
        }

        return Task.CompletedTask;
    }

    public Task AddSessionAsync(
        UserSession session,
        CancellationToken ct = default)
    {
        Sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<UserSession?> FindSessionByIdAsync(
        Guid sessionId,
        CancellationToken ct = default)
    {
        return Task.FromResult(
            Sessions.FirstOrDefault(s => s.Id == sessionId));
    }

    public Task<UserSession?> FindSessionByRefreshTokenHashAsync(
        string tokenHash,
        CancellationToken ct = default) =>
        Task.FromResult(Sessions.FirstOrDefault(s => s.RefreshTokenHash == tokenHash));

    public Task<UserSession?> FindSessionByPreviousRefreshTokenHashAsync(
        string tokenHash,
        CancellationToken ct = default) =>
        Task.FromResult(Sessions.FirstOrDefault(s => s.PreviousRefreshTokenHash == tokenHash));

    public Task<bool> TryRotateRefreshTokenAsync(
        Guid sessionId,
        string expectedTokenHash,
        int expectedVersion,
        string replacementTokenHash,
        DateTime currentTime,
        CancellationToken ct = default)
    {
        var session = Sessions.FirstOrDefault(s =>
            s.Id == sessionId &&
            s.RefreshTokenHash == expectedTokenHash &&
            s.Version == expectedVersion &&
            s.RevokedAtUtc == null &&
            s.ExpiresAtUtc > currentTime);
        if (session is null)
            return Task.FromResult(false);

        session.RefreshTokenHash = replacementTokenHash;
        session.PreviousRefreshTokenHash = expectedTokenHash;
        session.LastUsedAtUtc = currentTime;
        session.Version++;
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<UserSession>> GetSessionsByUserIdAsync(
        Guid userId,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<UserSession>>(Sessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.LastUsedAtUtc ?? s.CreatedAtUtc)
            .ToList());
}

public class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "hashed:" + password;

    public bool Verify(string password, string hash) =>
        hash == "hashed:" + password;
}

public class FakeTokenService : ITokenService
{
    public string CreateAccessToken(AppUser user, Guid sessionId) =>
        "token-for-" + user.Id + "-session-" + sessionId;
}
