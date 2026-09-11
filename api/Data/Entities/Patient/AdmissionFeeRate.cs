using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

/// <summary>
/// The one-off charge for opening a visit, by care level.
/// </summary>
/// <remarks>
/// Its own table rather than a row in <see cref="BillingRate"/> because it is priced by
/// something else entirely. A bed costs what the ward costs; the admission fee is set by the
/// care level a clinician recorded, and it is charged before anybody knows which ward the
/// patient will end up in. One table keyed by both would have a null in every row.
/// </remarks>
public class AdmissionFeeRate : SoftDeletableEntity
{
    public AdmissionCategory Category { get; set; }

    public decimal Amount { get; set; }
}
