using Fundoo.User.Domain.Entities;

namespace Fundoo.User.Application.Abstractions;

public interface IUserRepository
{
    Task<AppUser?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<AppUser?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(AppUser user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);

    Task AddResetTokenAsync(
    PasswordResetToken token,
    CancellationToken ct = default
);

    Task<PasswordResetToken?> FindValidResetTokenAsync(
        string tokenHash,
        DateTime currentTime,
        CancellationToken ct = default
    );

    Task MarkUnusedResetTokensUsedAsync(
        Guid userId,
        DateTime currentTime,
        CancellationToken ct = default
    );

    Task AddSessionAsync(UserSession session, CancellationToken ct = default);

    Task<UserSession?> FindSessionByIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<UserSession?> FindSessionByRefreshTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task<UserSession?> FindSessionByPreviousRefreshTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task<bool> TryRotateRefreshTokenAsync(
        Guid sessionId,
        string expectedTokenHash,
        int expectedVersion,
        string replacementTokenHash,
        DateTime currentTime,
        CancellationToken ct = default);
    Task<IReadOnlyList<UserSession>> GetSessionsByUserIdAsync(Guid userId, CancellationToken ct = default);
}
