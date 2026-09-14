namespace CareLanka.Api.DTOs.Patient;

public class ChecklistUpdateRequest
{
    public bool? ClinicalClearance { get; set; }

    public bool? BillingSettled { get; set; }
}
