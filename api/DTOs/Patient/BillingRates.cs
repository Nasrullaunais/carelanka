using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class BillingRateBook
{
    [Required]
    public IReadOnlyList<WardRates> Wards { get; set; } = [];

    [Required]
    public IReadOnlyList<AdmissionFee> AdmissionFees { get; set; } = [];

    [Required]
    public string Currency { get; set; } = "LKR";
}

public class WardRates
{
    [Required]
    public WardType WardType { get; set; }

    [Required]
    public IReadOnlyList<ExpenseRate> Expenses { get; set; } = [];
}

public class ExpenseRate
{
    [Required]
    public string ExpenseKey { get; set; } = string.Empty;

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

public class UpdateBillingRatesRequest
{
    [MaxLength(200)]
    public IReadOnlyList<WardExpenseRateUpdate> Expenses { get; set; } = [];

    [MaxLength(20)]
    public IReadOnlyList<AdmissionFeeUpdate> AdmissionFees { get; set; } = [];
}

public class WardExpenseRateUpdate
{
    [Required]
    [EnumDataType(typeof(WardType))]
    public WardType? WardType { get; set; }

    [Required]
    [MinLength(1)]
    [MaxLength(40)]
    public string ExpenseKey { get; set; } = string.Empty;

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
