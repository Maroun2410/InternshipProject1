using System.ComponentModel.DataAnnotations;

namespace MobileAPI.Domain;

public class WorkerInvite : BaseEntity
{
    [Required] public Guid OwnerId { get; set; }
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = default!;
    [MaxLength(200)] public string? FullName { get; set; }

    // Store SHA256 of the invite token (never store raw token)
    [Required, MaxLength(128)] public string TokenHash { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public bool IsRevoked { get; set; }

    // If accepted, link to the created worker user
    public Guid? WorkerUserId { get; set; }

    public bool IsActive => !IsRevoked && AcceptedAt == null && DateTime.UtcNow < ExpiresAt;
}
