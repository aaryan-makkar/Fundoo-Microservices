namespace Fundoo.User.Application.Abstractions;

public record GeneratedResetToken(
    string RawToken,
    string TokenHash
);

public interface IResetTokenService
{
    GeneratedResetToken GenerateToken();

    string HashToken(string token);
}