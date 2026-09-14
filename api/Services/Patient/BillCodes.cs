using System.Security.Cryptography;
using CareLanka.Api.Data.Configurations.Patient;

namespace CareLanka.Api.Services.Patient;

public static class BillCodes
{
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    private const char Prefix = 'B';

    public static string Next()
    {
        var code = new char[BillConfiguration.BillNumberLength];
        code[0] = Prefix;

        for (var i = 1; i < code.Length; i++)
        {
            code[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(code);
    }
}
