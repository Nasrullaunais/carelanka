namespace CareLanka.Api.Data.Enums;

// The four ways stock moves. Quantity on a transaction is always positive; this is what
// gives it a sign, so a row can never be read two ways.
public enum PharmacyTransactionType
{
    Received,
    Dispensed,
    Adjusted,
    ExpiredRemoved
}
