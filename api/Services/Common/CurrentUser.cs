using System.Security.Claims;
using CareLanka.Api.Data.Entities.Common;

namespace CareLanka.Api.Services.Common;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? StaffMemberId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public StaffRole? Role =>
        Enum.TryParse<StaffRole>(Principal?.FindFirstValue(ClaimTypes.Role), true, out var role)
            ? role
            : null;
}
