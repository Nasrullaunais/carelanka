namespace CareLanka.Api.Data.Enums;

// The five keys ChecklistUpdateRequest publishes. ClinicalClearance is Doctor-only, enforced
// in the service, not here.
public enum DischargeChecklistItemType
{
    ClinicalClearance,
    MedicationIssued,
    BillingSettled,
    FollowUpRecorded,
    TransportArranged
}
