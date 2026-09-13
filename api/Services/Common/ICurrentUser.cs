using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Common;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid Id { get; }

    PrincipalType PrincipalType { get; }

    PrincipalRole Role { get; }
}
