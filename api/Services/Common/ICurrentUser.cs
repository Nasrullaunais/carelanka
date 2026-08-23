using CareLanka.Api.Data.Entities.Common;

namespace CareLanka.Api.Services.Common;

/// <summary>Who is making this request, read from the JWT. Null on anonymous calls.</summary>
public interface ICurrentUser
{
    Guid? StaffMemberId { get; }
    StaffRole? Role { get; }
    bool IsAuthenticated { get; }
}
