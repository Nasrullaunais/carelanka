namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Result of the deterministic C# validator, run over the best pick and every alternative before
/// any of them reach a human. It runs a second time, under a row lock, at assign-bed.
/// </summary>
public sealed class BedWorkflowValidation
{
    public bool Passed { get; set; }

    public IReadOnlyList<string> FailedRules { get; set; } = Array.Empty<string>();
}
