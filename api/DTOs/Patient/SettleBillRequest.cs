using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>Taking the money.</summary>
public class SettleBillRequest
{
    /// <summary>
    /// How it was paid, in whatever words reception uses - "cash", "card ending 4417",
    /// "insurance, claim 88231". Free text on purpose: a payment-method enum is the first step
    /// of a payments system, and this component is not building one.
    /// </summary>
    [MaxLength(300)]
    public string? SettlementNote { get; set; }
}
