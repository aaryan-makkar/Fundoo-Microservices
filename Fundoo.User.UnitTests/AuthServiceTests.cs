using Fundoo.User.Application.Auth;

using System.Security.Cryptography;
using System.Text;

namespace Fundoo.User.UnitTests;

public class AuthServiceTests
{
    private readonly FakeUserRepository _repo = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _service = new AuthService(
            _repo,
            new FakePasswordHasher(),
            new FakeTokenService(),
            new AuthTokenOptions
            {
                RefreshTokenDays = 7
            });
    }

    [Fact]
    public async Task Register_NewUser_Succeeds_AndStoresHashAndNormalizedEmail()
    {
        var result = await _service.RegisterAsync("Asha", "Rao", " asha@example.com ", "Str0ng!Passw0rd");

        Assert.True(result.Succeeded);
        var user = Assert.Single(_repo.Users);
        Assert.Equal("ASHA@EXAMPLE.COM", user.NormalizedEmail);
        Assert.Equal("asha@example.com", user.Email);
        Assert.NotEqual("Str0ng!Passw0rd", user.PasswordHash);
    }

    [Fact]
    public async Task Register_SameEmailWithDifferentCapitalization_Fails()
    {
        await _service.RegisterAsync("Asha", "Rao", "asha@example.com", "Str0ng!Passw0rd");

        var result = await _service.RegisterAsync("Other", "Person", "ASHA@Example.com", "An0ther!Passw0rd");

        Assert.False(result.Succeeded);
        Assert.Single(_repo.Users);
    }

    [Fact]
    public async Task Login_CorrectCredentials_CreatesSessionAndReturnsBothTokens()
    {
        await _service.RegisterAsync(
            "Asha", "Rao", "asha@example.com", "Str0ng!Passw0rd");

        var result = await _service.LoginAsync(
            "asha@example.com", "Str0ng!Passw0rd");

        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrEmpty(result.AccessToken));
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));

        var session = Assert.Single(_repo.Sessions);

        Assert.Equal(_repo.Users[0].Id, session.UserId);
        Assert.NotEqual(result.RefreshToken, session.RefreshTokenHash);
        Assert.True(session.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_UnknownEmail_And_WrongPassword_ReturnSameGenericError()
    {
        await _service.RegisterAsync("Asha", "Rao", "asha@example.com", "Str0ng!Passw0rd");

        var unknownEmail = await _service.LoginAsync("nobody@example.com", "Str0ng!Passw0rd");
        var wrongPassword = await _service.LoginAsync("asha@example.com", "wrong-password");

        Assert.False(unknownEmail.Succeeded);
        Assert.False(wrongPassword.Succeeded);
        Assert.Equal(unknownEmail.Error, wrongPassword.Error);
        Assert.Null(unknownEmail.AccessToken);
        Assert.Null(wrongPassword.AccessToken);
    }

    [Fact]
    public async Task Login_InactiveUser_Fails()
    {
        await _service.RegisterAsync("Asha", "Rao", "asha@example.com", "Str0ng!Passw0rd");
        _repo.Users[0].IsActive = false;

        var result = await _service.LoginAsync("asha@example.com", "Str0ng!Passw0rd");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndRejectsPreviouslyUsedToken()
    {
        await _service.RegisterAsync("Asha", "Rao", "asha@example.com", "Str0ng!Passw0rd");
        var login = await _service.LoginAsync("asha@example.com", "Str0ng!Passw0rd");
        var session = Assert.Single(_repo.Sessions);
        var originalToken = login.RefreshToken!;
        var originalHash = session.RefreshTokenHash;

        var refreshed = await _service.RefreshAsync(originalToken);

        Assert.True(refreshed.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshed.RefreshToken));
        Assert.NotEqual(originalHash, session.RefreshTokenHash);
        Assert.Equal(originalHash, session.PreviousRefreshTokenHash);
        Assert.Equal(1, session.Version);
        Assert.Equal(Hash(refreshed.RefreshToken!), session.RefreshTokenHash);

        var replayed = await _service.RefreshAsync(originalToken);

        Assert.False(replayed.Succeeded);
        Assert.True(replayed.IsConflict);
        Assert.Equal(1, session.Version);
    }

    [Fact]
    public async Task LoginOnDifferentDevices_CreatesIndependentSessions()
    {
        await _service.RegisterAsync("Asha", "Rao", "asha@example.com", "Str0ng!Passw0rd");

        var laptopLogin = await _service.LoginAsync(
            "asha@example.com", "Str0ng!Passw0rd", "Laptop Browser");
        var mobileLogin = await _service.LoginAsync(
            "asha@example.com", "Str0ng!Passw0rd", "Mobile Browser");

        Assert.Equal(2, _repo.Sessions.Count);
        Assert.NotEqual(laptopLogin.SessionId, mobileLogin.SessionId);
        Assert.NotEqual(laptopLogin.RefreshToken, mobileLogin.RefreshToken);
        Assert.Equal("Laptop Browser", _repo.Sessions[0].DeviceInfo);
        Assert.Equal("Mobile Browser", _repo.Sessions[1].DeviceInfo);
        Assert.All(_repo.Sessions, session => Assert.Equal(_repo.Users[0].Id, session.UserId));
    }

    [Fact]
    public async Task ConcurrentRefreshRequests_OnlyRotateOnce_AndReturnConflictForSecond()
    {
        await _service.RegisterAsync("Asha", "Rao", "asha@example.com", "Str0ng!Passw0rd");
        var login = await _service.LoginAsync("asha@example.com", "Str0ng!Passw0rd");

        var results = await Task.WhenAll(
            _service.RefreshAsync(login.RefreshToken!),
            _service.RefreshAsync(login.RefreshToken!));

        Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => result.IsConflict);
        Assert.Single(_repo.Sessions);
        Assert.Equal(1, _repo.Sessions[0].Version);
    }

    [Fact]
    public async Task Refresh_RevokedOrExpiredSession_Fails()
    {
        await _service.RegisterAsync("Asha", "Rao", "asha@example.com", "Str0ng!Passw0rd");
        var login = await _service.LoginAsync("asha@example.com", "Str0ng!Passw0rd");
        var session = Assert.Single(_repo.Sessions);
        session.RevokedAtUtc = DateTime.UtcNow;

        var revoked = await _service.RefreshAsync(login.RefreshToken!);
        Assert.False(revoked.Succeeded);
        Assert.False(revoked.IsConflict);

        session.RevokedAtUtc = null;
        session.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);

        var expired = await _service.RefreshAsync(login.RefreshToken!);
        Assert.False(expired.Succeeded);
        Assert.False(expired.IsConflict);
    }

    [Fact]
    public async Task RevokeSession_OnlyRevokesSessionOwnedByRequestingUser()
    {
        await _service.RegisterAsync("Asha", "Rao", "asha@example.com", "Str0ng!Passw0rd");
        var login = await _service.LoginAsync("asha@example.com", "Str0ng!Passw0rd");
        var session = Assert.Single(_repo.Sessions);

        var wrongOwner = await _service.RevokeSessionAsync(Guid.NewGuid(), session.Id);
        Assert.False(wrongOwner);
        Assert.Null(session.RevokedAtUtc);

        var revoked = await _service.RevokeSessionAsync(_repo.Users[0].Id, session.Id);
        Assert.True(revoked);
        Assert.NotNull(session.RevokedAtUtc);

        var refresh = await _service.RefreshAsync(login.RefreshToken!);
        Assert.False(refresh.Succeeded);
    }

    private static string Hash(string token) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
