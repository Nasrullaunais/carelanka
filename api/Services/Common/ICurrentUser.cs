using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Common;

/// <summary>
/// The signed-in principal, read from the token and nowhere else.
/// <para>
/// Every <c>/me/*</c> endpoint in every component resolves its subject through this,
/// never through a route parameter. A route that takes an id and checks it against the
/// JWT is one forgotten check away from letting any patient read another patient's
/// admission; a route that takes no id cannot have that bug at all.
/// </para>
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The <c>sub</c> claim. Throws if the request is not authenticated.</summary>
    Guid Id { get; }

    /// <summary>The <c>typ</c> claim — which table <see cref="Id"/> is in.</summary>
    PrincipalType PrincipalType { get; }

    /// <summary>The <c>role</c> claim.</summary>
    PrincipalRole Role { get; }
}
