using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

public class AdmissionFeeRate : SoftDeletableEntity
{
    public AdmissionCategory Category { get; set; }

    public decimal Amount { get; set; }
}
