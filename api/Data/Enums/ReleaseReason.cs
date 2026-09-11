namespace CareLanka.Api.Data.Enums;

public enum ReleaseReason
{
    Discharged,
    HoldExpired,
    Cancelled,
    Transferred,
    Rejected,

    /// <summary>
    /// The bed was wrong and somebody fixed it. Distinct from <see cref="Transferred"/>, which
    /// is a patient genuinely moving ward, because the two bill differently: a corrected
    /// assignment charges nothing at all, and a transfer charges the nights actually spent.
    /// </summary>
    /// <remarks>
    /// Without the distinction, correcting a mis-click two minutes after making it would put a
    /// second bed line on the bill — and every stay is rounded up to at least one day, so
    /// the patient would be charged two nights for one. The reason column is what
    /// <c>BillingService</c> reads to know not to.
    /// </remarks>
    Corrected
}
