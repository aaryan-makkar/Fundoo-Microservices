using System.Security.Cryptography;
using System.Text;
using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entities;

namespace Fundoo.User.Application.Auth;

public class AuthService(
    IUserRepository users,
    IPasswordHasher hasher,
    ITokenService tokens,
    AuthTokenOptions jwtOptions) : IAuthService
{
    public Task<IReadOnlyList<UserSession>> GetSessionsAsync(
        Guid userId,
        CancellationToken ct = default) => users.GetSessionsByUserIdAsync(userId, ct);

    public async Task<bool> RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken ct = default)
    {
        var session = await users.FindSessionByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            return false;

        // Treat a repeated logout as success so clients can safely retry it.
        if (session.RevokedAtUtc is null)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
            await users.SaveChangesAsync(ct);
        }

        return true;
    }

    public async Task<AuthResult> RefreshAsync(
        string refreshToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return AuthResult.Failure("Session is invalid or expired. Please sign in again.");

        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        var session = await users.FindSessionByRefreshTokenHashAsync(tokenHash, ct);
        var now = DateTime.UtcNow;

        if (session is null)
        {
            var previouslyUsedSession = await users.FindSessionByPreviousRefreshTokenHashAsync(tokenHash, ct);
            if (previouslyUsedSession is { RevokedAtUtc: null } &&
                previouslyUsedSession.ExpiresAtUtc > now)
            {
                return AuthResult.Failure(
                    "This session was already refreshed. Use the latest session tokens.",
                    isConflict: true);
            }

            return AuthResult.Failure("Session is invalid or expired. Please sign in again.");
        }

        if (session.RevokedAtUtc is not null || session.ExpiresAtUtc <= now)
            return AuthResult.Failure("Session is invalid or expired. Please sign in again.");

        var user = await users.FindByIdAsync(session.UserId, ct);
        if (user is null || !user.IsActive)
            return AuthResult.Failure("Session is invalid or expired. Please sign in again.");

        var replacementRefreshToken = CreateRefreshToken();
        var replacementTokenHash = HashRefreshToken(replacementRefreshToken);
        var rotated = await users.TryRotateRefreshTokenAsync(
            session.Id,
            tokenHash,
            session.Version,
            replacementTokenHash,
            now,
            ct);

        if (!rotated)
        {
            var previouslyUsedSession = await users.FindSessionByPreviousRefreshTokenHashAsync(tokenHash, ct);
            if (previouslyUsedSession is { RevokedAtUtc: null } &&
                previouslyUsedSession.ExpiresAtUtc > now)
            {
                return AuthResult.Failure(
                    "This session was already refreshed. Use the latest session tokens.",
                    isConflict: true);
            }

            return AuthResult.Failure("Session is invalid or expired. Please sign in again.");
        }

        return AuthResult.Success(
            tokens.CreateAccessToken(user, session.Id),
            replacementRefreshToken,
            session.Id);
    }

    public async Task<AuthResult> RegisterAsync(
        string firstName,
        string lastName,
        string email,
        string password,
        CancellationToken ct = default)
    {
        var normalized = EmailNormalizer.Normalize(email);

        if (await users.FindByNormalizedEmailAsync(normalized, ct) is not null)
            return AuthResult.Failure("An account with this email already exists.");

        var now = DateTime.UtcNow;

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = email.Trim(),
            NormalizedEmail = normalized,
            PasswordHash = hasher.Hash(password),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await users.AddAsync(user, ct);
        await users.SaveChangesAsync(ct);

        return AuthResult.Success();
    }

    public async Task<AuthResult> LoginAsync(
        string email,
        string password,
        string? deviceInfo = null,
        CancellationToken ct = default)
    {
        var user = await users.FindByNormalizedEmailAsync(
            EmailNormalizer.Normalize(email), ct);

        if (user is null || !user.IsActive ||
            !hasher.Verify(password, user.PasswordHash))
        {
            return AuthResult.Failure("Invalid email or password.");
        }

        var now = DateTime.UtcNow;

        // Create a unique session for this login.
        var sessionId = Guid.NewGuid();

        // Generate a cryptographically secure refresh token.
        var refreshToken = CreateRefreshToken();

        // Store only the SHA-256 hash, never the raw refresh token.
        var refreshTokenHash = HashRefreshToken(refreshToken);

        var session = new UserSession
        {
            Id = sessionId,
            UserId = user.Id,
            RefreshTokenHash = refreshTokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(jwtOptions.RefreshTokenDays),
            LastUsedAtUtc = now,
            DeviceInfo = string.IsNullOrWhiteSpace(deviceInfo)
                ? null
                : deviceInfo.Trim()[..Math.Min(deviceInfo.Trim().Length, 512)],
            Version = 0
        };

        await users.AddSessionAsync(session, ct);
        await users.SaveChangesAsync(ct);

        // Include the session ID in the access token.
        var accessToken = tokens.CreateAccessToken(user, sessionId);

        return AuthResult.Success(accessToken, refreshToken, sessionId);
    }

    private static string CreateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
