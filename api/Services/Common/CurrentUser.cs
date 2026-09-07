using CareLanka.Api.Common.Auth;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Common;

/// <inheritdoc cref="ICurrentUser"/>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public bool IsAuthenticated =>
        _accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid Id => Guid.Parse(Claim(CareLankaClaims.Subject));

    public PrincipalType PrincipalType =>
        EnumWire.FromWire<PrincipalType>(Claim(CareLankaClaims.PrincipalType));

    public PrincipalRole Role =>
        EnumWire.FromWire<PrincipalRole>(Claim(CareLankaClaims.Role));

    private string Claim(string type)
    {
        var value = _accessor.HttpContext?.User.FindFirst(type)?.Value;

        // A validated token that is missing one of its four claims is not a client error to
        // explain — it is a token this API should not have issued. 401 and move on.
        return string.IsNullOrEmpty(value)
            ? throw new UnauthorizedException(MessageCode.NotAuthenticated)
            : value;
    }
}
