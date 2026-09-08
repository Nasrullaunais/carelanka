using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Common;

// Every /me endpoint resolves its subject through this, never through a route parameter —
// a route that takes no id cannot have the "forgot to check it against the JWT" bug at all.
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid Id { get; }

    PrincipalType PrincipalType { get; }

    PrincipalRole Role { get; }
}
