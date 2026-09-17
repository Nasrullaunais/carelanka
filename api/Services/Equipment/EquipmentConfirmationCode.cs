using System.Security.Cryptography;
using System.Text;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Equipment;

public interface IEquipmentConfirmationCode
{
    void Ensure(string? supplied);
}

public sealed class EquipmentConfirmationCode : IEquipmentConfirmationCode
{
    private readonly IOptions<EquipmentOptions> _options;

    public EquipmentConfirmationCode(IOptions<EquipmentOptions> options) => _options = options;

    public void Ensure(string? supplied)
    {
        var expected = Encoding.UTF8.GetBytes(_options.Value.ConfirmationCode);
        var actual = Encoding.UTF8.GetBytes(supplied ?? string.Empty);

        if (expected.Length == 0 || !CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            throw new ForbiddenException(MessageCode.ConfirmationCodeIncorrect);
        }
    }
}
