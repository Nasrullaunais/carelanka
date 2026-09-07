using System.Net;
using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

/// <summary>
/// 401. Who you say you are did not check out.
/// <para>
/// Login throws this with <see cref="MessageCode.InvalidCredentials"/> for all three
/// failures — wrong password, unknown account, deactivated account — and the response is
/// byte-identical in each case. Different responses would turn the endpoint into a way to
/// discover which emails exist at the hospital.
/// </para>
/// </summary>
public class UnauthorizedException : ApiException
{
    public UnauthorizedException(MessageCode code = MessageCode.InvalidCredentials, params object?[] args)
        : base(HttpStatusCode.Unauthorized, code, args)
    {
    }
}
