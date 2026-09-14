using System.Security.Cryptography;
using CareLanka.Api.Data.Configurations.Patient;

namespace CareLanka.Api.Services.Patient;

public static class PatientCodes
{
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    private const char Prefix = 'P';

    public static string Next()
    {
        var code = new char[PatientConfiguration.PatientCodeLength];
        code[0] = Prefix;

        for (var i = 1; i < code.Length; i++)
        {
            code[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(code);
    }
}
