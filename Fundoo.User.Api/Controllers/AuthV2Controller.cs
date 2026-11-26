using Asp.Versioning;
using Fundoo.User.Api.Contracts;
using Fundoo.User.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Fundoo.User.Api.Controllers;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthV2Controller(
    IAuthService auth,
    IForgotPasswordService forgotPassword) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterRequest request,
        CancellationToken ct)
    {
        var result = await auth.RegisterAsync(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Password,
            ct);

        if (!result.Succeeded)
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: result.Error);

        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken ct)
    {
        var result = await auth.LoginAsync(
            request.Email,
            request.Password,
            Request.Headers.UserAgent.ToString(),
            ct);

        if (!result.Succeeded)
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: result.Error);

        return Ok(new
        {
            accessToken = result.AccessToken,
            refreshToken = result.RefreshToken,
            sessionId = result.SessionId,
            apiVersion = "2.0",
            message = "Login successful"
        });
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
            refreshToken = result.RefreshToken,
            apiVersion = "2.0"
        });
    }

    [Authorize]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
            return Unauthorized();

        var sessions = await auth.GetSessionsAsync(userId, ct);
        return Ok(new
        {
            apiVersion = "2.0",
            sessions = sessions.Select(s => new
            {
                sessionId = s.Id,
                deviceInfo = s.DeviceInfo,
                createdAtUtc = s.CreatedAtUtc,
                expiresAtUtc = s.ExpiresAtUtc,
                lastUsedAtUtc = s.LastUsedAtUtc,
                isRevoked = s.RevokedAtUtc != null
            })
        });
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

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken ct)
    {
        await forgotPassword.RequestAsync(request.Email, ct);

        return Ok(new
        {
            message = "If an account exists for this email, a reset token has been sent.",
            apiVersion = "2.0"
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken ct)
    {
        var result = await forgotPassword.ResetAsync(
            request.Token,
            request.NewPassword,
            ct);

        if (!result.Succeeded)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: result.Error);

        return Ok(new
        {
            message = "Password has been reset. You can now log in.",
            apiVersion = "2.0"
        });
    }
}
