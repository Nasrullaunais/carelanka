using System.Security.Cryptography;
using CareLanka.Api.Data.Configurations.Patient;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// Makes the number a patient quotes at the counter: <c>B7K2X9Q</c>.
/// </summary>
/// <remarks>
/// The same alphabet and the same reasoning as <see cref="PatientCodes"/>. Random rather than a
/// running invoice number: a sequence would publish how much business the hospital does, and
/// two desks preparing a bill at once would fight over the next one.
/// </remarks>
public static class BillCodes
{
    /// <summary>No 0/O and no 1/I/L - the pairs people get wrong reading one off paper.</summary>
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    private const char Prefix = 'B';

    public static string Next()
    {
        var code = new char[BillConfiguration.BillNumberLength];
        code[0] = Prefix;

        for (var i = 1; i < code.Length; i++)
        {
            // RandomNumberGenerator, not Random: Random is seeded per instance and two requests
            // arriving in the same tick can produce the same sequence.
            code[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(code);
    }
}
