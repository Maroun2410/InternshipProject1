using System.Security.Claims;

namespace MobileAPI.Services;

public interface ICurrentUser
{
    Guid? OwnerId { get; }
    bool IsWorker { get; }
    bool IsOwner { get; }
}

public class CurrentUserFromHttpContext : ICurrentUser
{
    private readonly IHttpContextAccessor _http;
    public CurrentUserFromHttpContext(IHttpContextAccessor http) => _http = http;

    public Guid? OwnerId
    {
        get
        {
            var user = _http.HttpContext?.User;
            var value = user?.FindFirst("owner_id")?.Value
                        ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsWorker =>
        _http.HttpContext?.User?.IsInRole("Worker") ?? false;

    public bool IsOwner =>
        _http.HttpContext?.User?.IsInRole("Owner") ?? false;
}
