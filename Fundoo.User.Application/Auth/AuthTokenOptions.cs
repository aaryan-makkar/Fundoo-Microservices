namespace Fundoo.User.Application.Auth;

public class AuthTokenOptions
{
    public int RefreshTokenDays { get; set; } = 7;
}