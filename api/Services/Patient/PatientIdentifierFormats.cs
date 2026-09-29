namespace CareLanka.Api.Services.Patient;

public static class PatientIdentifierFormats
{
    // A passport has to start with a letter; otherwise a mistyped NIC such as 2947382939772v
    // has a letter in it and passes as "a passport, presumably".
    public const string Nic = @"^(\d{9}[VvXx]|\d{12}|[A-Za-z][A-Za-z0-9]{5,14})$";

    public const string NicMessage =
        "Enter an NIC as nine digits and a V (199534501V) or as twelve digits (199745600321), "
        + "or a passport number starting with a letter. Leave it blank if they have no papers.";

    public const string Phone = @"^(0\d{9}|\+94\d{9})$";

    public const string PhoneMessage =
        "A phone number is ten digits starting with 0, like 0771234567.";

    // Mirrors PatientCodes.Next: a P and seven characters, with I, L, O, 0 and 1 left out.
    public const string PatientCode = "^P[23456789ABCDEFGHJKMNPQRSTUVWXYZ]{7}$";

    public const string PatientCodeMessage =
        "A patient code is a P followed by seven characters, like PK4M9XB2. "
        + "It is on the slip the hospital gave you.";

    public const int MaxAgeYears = 120;
}
