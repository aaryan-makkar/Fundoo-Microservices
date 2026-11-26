namespace Fundoo.User.Application.Auth;

public record AuthResult(
    bool Succeeded,
    string? Error,
    string? AccessToken,
    string? RefreshToken,
    Guid? SessionId,
    bool IsConflict)
{
    public static AuthResult Success(
        string? accessToken = null,
        string? refreshToken = null,
        Guid? sessionId = null)
        => new(true, null, accessToken, refreshToken, sessionId, false);

    public static AuthResult Failure(string error, bool isConflict = false)
        => new(false, error, null, null, null, isConflict);
}
