using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// The whole price grid, as GET /api/billing/rates returns it.
/// </summary>
/// <remarks>
/// Every member below is [Required] for the reason DTOs/Patient/Ward.cs gives: without it the
/// generated clients type all of them as optional, and a grid whose every cell is
/// <c>number | undefined</c> cannot be rendered without a fallback per cell.
/// </remarks>
public class BillingRateBook
{
    /// <summary>One entry per kind of ward, in the order the settings screen shows them.</summary>
    [Required]
    public IReadOnlyList<WardRates> Wards { get; set; } = [];

    /// <summary>The one-off charge for opening a visit, by care level.</summary>
    [Required]
    public IReadOnlyList<AdmissionFee> AdmissionFees { get; set; } = [];

    /// <summary>Fixed. This component does not convert currency and does not pretend to.</summary>
    [Required]
    public string Currency { get; set; } = "LKR";
}

/// <summary>Every expense priced for one kind of ward.</summary>
public class WardRates
{
    [Required]
    public WardType WardType { get; set; }

    [Required]
    public IReadOnlyList<ExpenseRate> Expenses { get; set; } = [];
}

/// <summary>One cell of the grid.</summary>
public class ExpenseRate
{
    /// <summary>Stable key — `bed_day`, `food`, `therapy`. What the client matches on.</summary>
    [Required]
    public string ExpenseKey { get; set; } = string.Empty;

    /// <summary>Rupees. Zero means there is no suggested price, not that it is free.</summary>
    [Required]
    public decimal Amount { get; set; }
}

public class AdmissionFee
{
    [Required]
    public AdmissionCategory Category { get; set; }

    [Required]
    public decimal Amount { get; set; }
}

/// <summary>Body of PUT /api/billing/rates. Only the cells that changed need to be sent.</summary>
public class UpdateBillingRatesRequest
{
    [MaxLength(200)]
    public IReadOnlyList<WardExpenseRateUpdate> Expenses { get; set; } = [];

    [MaxLength(20)]
    public IReadOnlyList<AdmissionFeeUpdate> AdmissionFees { get; set; } = [];
}

public class WardExpenseRateUpdate
{
    /// <summary>Nullable for the reason CreateWardRequest explains: a missing key would default to `icu`.</summary>
    [Required]
    [EnumDataType(typeof(WardType))]
    public WardType? WardType { get; set; }

    [Required]
    [MinLength(1)]
    [MaxLength(40)]
    public string ExpenseKey { get; set; } = string.Empty;

    /// <summary>A negative price is not a discount, it is a typo that pays the patient.</summary>
    [Required]
    [Range(0, 10_000_000)]
    public decimal? Amount { get; set; }
}

public class AdmissionFeeUpdate
{
    [Required]
    [EnumDataType(typeof(AdmissionCategory))]
    public AdmissionCategory? Category { get; set; }

    [Required]
    [Range(0, 10_000_000)]
    public decimal? Amount { get; set; }
}
