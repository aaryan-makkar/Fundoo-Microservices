namespace Fundoo.User.Domain.Entities;

public class UserSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string RefreshTokenHash { get; set; } = "";

    public string? PreviousRefreshTokenHash { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public DateTime? LastUsedAtUtc { get; set; }

    public string? DeviceInfo { get; set; }

    public int Version { get; set; }
}
