namespace CareLanka.Api.Services.Patient;

public static class MaskedIdentity
{
    private const char Bullet = '•';

    private const int PhoneDigitsShown = 3;

    public static string Name(string fullName)
        => string.Join(
            ' ',
            fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(MaskWord));

    public static string? Phone(string? phone)
    {
        var digits = phone?.Trim();

        if (string.IsNullOrEmpty(digits))
        {
            return null;
        }

        return digits.Length <= PhoneDigitsShown
            ? new string(Bullet, digits.Length)
            : new string(Bullet, digits.Length - PhoneDigitsShown) + digits[^PhoneDigitsShown..];
    }

    private static string MaskWord(string word)
        => word.Length <= 2
            ? word[0] + new string(Bullet, word.Length - 1)
            : word[0] + new string(Bullet, word.Length - 2) + word[^1];
}
