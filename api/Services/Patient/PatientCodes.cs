using System.Security.Cryptography;
using CareLanka.Api.Data.Configurations.Patient;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// Makes the short handle a patient is known by outside this component: <c>P7K2X9QM</c>.
/// </summary>
/// <remarks>
/// Random rather than a running number. A sequence would publish how many patients the
/// hospital has ever registered, and two desks registering at once would fight over the next
/// one. Random codes never collide often enough to care, and the unique index is the real
/// guarantee either way.
/// </remarks>
public static class PatientCodes
{
    /// <summary>
    /// No 0/O and no 1/I/L. Someone reads this off a wristband and someone else types it in,
    /// and those are the pairs they get wrong — 31 usable characters is plenty.
    /// </summary>
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    /// <summary>The fixed first character, so a code is recognisable as one on sight.</summary>
    private const char Prefix = 'P';

    /// <summary>
    /// A new candidate code. Random, so it is not unique by construction — the caller saves it
    /// behind the unique index and tries again if the database says no.
    /// </summary>
    public static string Next()
    {
        var code = new char[PatientConfiguration.PatientCodeLength];
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
