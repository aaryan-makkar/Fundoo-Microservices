using Fundoo.User.Application.Auth;
using Fundoo.User.Domain.Entities;

namespace Fundoo.User.Application.Abstractions;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string firstName, string lastName, string email, string password, CancellationToken ct = default);
    Task<AuthResult> LoginAsync(string email, string password, string? deviceInfo = null, CancellationToken ct = default);
    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task<IReadOnlyList<UserSession>> GetSessionsAsync(Guid userId, CancellationToken ct = default);
    Task<bool> RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
}
