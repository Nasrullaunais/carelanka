namespace CareLanka.Api.DTOs.Patient;

public sealed class CareWorkflowValidation
{
    public bool Passed { get; set; } = true;

    public IReadOnlyList<string> FailedRules { get; set; } = Array.Empty<string>();
}
