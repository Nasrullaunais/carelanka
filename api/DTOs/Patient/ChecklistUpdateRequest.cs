namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Any subset of the checklist. A key left out is not touched; a key set to <c>false</c> is
/// unticked.
/// </summary>
/// <remarks>
/// Every key is role-gated in <c>DischargeService</c> rather than on the route, because which
/// boxes you may tick depends on which boxes are in the body.
///
/// <c>billing_settled</c> is here so the contract is honest about the vocabulary, and sending
/// it is always refused - it is written by settling the bill and by nothing else.
/// </remarks>
public class ChecklistUpdateRequest
{
    /// <summary>Doctor only. The wall: without it nothing flags and nothing discharges.</summary>
    public bool? ClinicalClearance { get; set; }

    /// <summary>Ward nurse. Mandatory.</summary>
    public bool? MedicationIssued { get; set; }

    /// <summary>Refused here. Settle the bill instead - <c>POST /api/admissions/{id}/bill/settle</c>.</summary>
    public bool? BillingSettled { get; set; }

    /// <summary>Ward nurse. Optional item.</summary>
    public bool? FollowUpRecorded { get; set; }

    /// <summary>Ward nurse. Optional item.</summary>
    public bool? TransportArranged { get; set; }
}
