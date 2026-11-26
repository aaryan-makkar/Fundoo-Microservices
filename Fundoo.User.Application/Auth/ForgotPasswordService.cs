using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entities;

namespace Fundoo.User.Application.Auth;

public class ForgotPasswordService(
    IUserRepository users,
    IResetTokenService tokens,
    IEmailNotifier notifier,
    IPasswordHasher hasher) : IForgotPasswordService
{
    public async Task RequestAsync(
        string email,
        CancellationToken ct = default)
    {
        var user = await users.FindByNormalizedEmailAsync(
            EmailNormalizer.Normalize(email), ct);

        if (user is null || !user.IsActive)
            return;

        var now = DateTime.UtcNow;

        // Invalidate previous unused tokens.
        await users.MarkUnusedResetTokensUsedAsync(user.Id, now, ct);

        // Generate a new random reset token.
        var generatedToken = tokens.GenerateToken();

        // Store only the token hash.
        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = generatedToken.TokenHash,
            ExpiresAtUtc = now.AddMinutes(15),
            UsedAtUtc = null
        };

        await users.AddResetTokenAsync(resetToken, ct);

        await users.SaveChangesAsync(ct);

        // Send the original token through Notification Service.
        await notifier.SendPasswordResetEmailAsync(
            user.Email,
            user.FirstName,
            generatedToken.RawToken,
            ct);
    }

    public async Task<AuthResult> ResetAsync(
        string token,
        string newPassword,
        CancellationToken ct = default)
    {
        const string invalid = "Invalid or expired token.";

        // Hash the token received from the client.
        string tokenHash = tokens.HashToken(token);

        var now = DateTime.UtcNow;

        // Find a valid, unused and unexpired token.
        var resetToken = await users.FindValidResetTokenAsync(
            tokenHash, now, ct);

        if (resetToken is null)
            return AuthResult.Failure(invalid);

        var user = await users.FindByIdAsync(
            resetToken.UserId, ct);

        if (user is null || !user.IsActive)
            return AuthResult.Failure(invalid);

        // Hash the new password using the existing BCrypt implementation.
        user.PasswordHash = hasher.Hash(newPassword);

        user.UpdatedAtUtc = now;

        // Invalidate all outstanding reset tokens for this user.
        await users.MarkUnusedResetTokensUsedAsync(user.Id, now, ct);

        // Persist password and token usage together.
        await users.SaveChangesAsync(ct);

        return AuthResult.Success();
    }
}