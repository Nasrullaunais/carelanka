using System.Net;
using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

public sealed class AddressSearchUnavailableException : ApiException
{
    public AddressSearchUnavailableException()
        : base(HttpStatusCode.ServiceUnavailable, MessageCode.AddressSearchUnavailable)
    {
    }
}
