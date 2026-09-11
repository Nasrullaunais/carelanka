namespace CareLanka.Api.Data.Enums;

/// <summary>
/// The two things that have to be true before a patient goes home.
/// </summary>
/// <remarks>
/// It was five until 2026-09-11. <c>medication_issued</c>, <c>follow_up_recorded</c> and
/// <c>transport_arranged</c> came off, because each of them was a box that recorded something
/// the bill already records: what the patient was given, and what was arranged for them, are
/// lines on the bill with a price against them. Two records of one fact is how they end up
/// disagreeing, and the checklist copy was the one nobody could price.
///
/// What is left is the pair that genuinely gate a discharge and that nothing else states:
/// a doctor said this person is well enough to leave, and the money is settled.
///
/// <c>ClinicalClearance</c> is Doctor-only and <c>BillingSettled</c> is nobody's — settling the
/// bill writes it. Both are enforced in the service, not here.
/// </remarks>
public enum DischargeChecklistItemType
{
    ClinicalClearance,
    BillingSettled
}
