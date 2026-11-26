using Fundoo.User.Api.Contracts;
using Fundoo.User.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Fundoo.User.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController(IAuthService auth, IForgotPasswordService forgotPassword) : ControllerBase
{

    [Authorize]
    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        var userId = User.FindFirstValue("sub");

        var email = User.FindFirstValue("email");

        return Ok(new
        {
            userId,
            email
        });
    }

    [Authorize]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
            return Unauthorized();

        var sessions = await auth.GetSessionsAsync(userId, ct);
        return Ok(sessions.Select(s => new
        {
            sessionId = s.Id,
            deviceInfo = s.DeviceInfo,
            createdAtUtc = s.CreatedAtUtc,
            expiresAtUtc = s.ExpiresAtUtc,
            lastUsedAtUtc = s.LastUsedAtUtc,
            isRevoked = s.RevokedAtUtc != null
        }));
    }

    [Authorize]
    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
            return Unauthorized();

        if (!await auth.RevokeSessionAsync(userId, sessionId, ct))
            return NotFound();

        return NoContent();
    }


    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request.FirstName, request.LastName, request.Email, request.Password, ct);
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: result.Error);

        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(
            request.Email,
            request.Password,
            Request.Headers.UserAgent.ToString(),
            ct);
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: result.Error);

        return Ok(new TokenResponse(
    result.AccessToken!,
    result.RefreshToken!,
    result.SessionId!.Value
));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var result = await auth.RefreshAsync(request.RefreshToken, ct);
        if (!result.Succeeded)
            return Problem(
                statusCode: result.IsConflict
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status401Unauthorized,
                title: result.Error);

        return Ok(new
        {
            accessToken = result.AccessToken,
            refreshToken = result.RefreshToken
        });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await forgotPassword.RequestAsync(request.Email, ct);

        // Email exist kare ya na kare, jawab hamesha same
        return Ok(new { message = "If an account exists for this email, a reset token has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await forgotPassword.ResetAsync(request.Token, request.NewPassword, ct);
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);

        return Ok(new { message = "Password has been reset. You can now log in." });
    }
}
