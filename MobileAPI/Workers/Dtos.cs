using System.ComponentModel.DataAnnotations;

namespace MobileAPI.Workers;

public record InviteWorkerRequest(
    [Required, EmailAddress] string Email,
    string? FullName,
    int? ExpiresDays // default 7 if null
);

public record InviteWorkerResponse(
    Guid InviteId,
    string Email,
    DateTime ExpiresAt,
    string? InviteUrl, // present in DEV
    string? Token      // present in DEV
);

public record AcceptInviteRequest(
    [Required, EmailAddress] string Email,
    [Required] string Token,
    [Required, MinLength(6)] string Password,
    string? FullName
);

public record AssignFarmsRequest(
    [Required] Guid WorkerUserId,
    [Required] List<Guid> FarmIds
);
