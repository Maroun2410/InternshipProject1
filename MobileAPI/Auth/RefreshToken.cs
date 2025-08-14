using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobileAPI.Auth;

public class RefreshToken
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required] public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))] public ApplicationUser? User { get; set; }

    // Store a SHA256 hash of the token string (never the raw token)
    [Required, MaxLength(128)]
    public string TokenHash { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    // Optional telemetry
    [MaxLength(200)] public string? Device { get; set; }
    [MaxLength(100)] public string? IpAddress { get; set; }
    [MaxLength(300)] public string? UserAgent { get; set; }

    public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;
}
