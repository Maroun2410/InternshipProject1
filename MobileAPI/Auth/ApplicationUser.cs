using Microsoft.AspNetCore.Identity;

namespace MobileAPI.Auth;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? FullName { get; set; }
    public bool IsActive { get; set; } = true;

    // If role = Worker, this links the worker to the Employer (Owner) they work for.
    public Guid? EmployerOwnerId { get; set; }
}
