using System.Security.Cryptography;
using System.Text;
using Fundoo.User.Application.Abstractions;

namespace Fundoo.User.Infrastructure.Security;

public class JwtResetTokenService : IResetTokenService
{
    public GeneratedResetToken GenerateToken()
    {
        // Generate a cryptographically secure random token.
        var randomBytes = RandomNumberGenerator.GetBytes(32);

        var rawToken = Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        // Generate its SHA-256 hash for database storage.
        var tokenHash = HashToken(rawToken);

        return new GeneratedResetToken(rawToken, tokenHash);
    }

    public string HashToken(string token)
    {
        var hashBytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(token));

        return Convert.ToHexString(hashBytes);
    }
}