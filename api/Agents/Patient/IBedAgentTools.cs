using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// The bed agent's entire allow-list. Five tools, all five read. There is no tool that admits a
/// patient, reserves a bed, confirms a discharge, changes a care category, frees an occupied bed
/// or touches another component's data.
/// </summary>
/// <remarks>
/// §8.4 of the design doc lists four. The fifth is <see cref="ListPreviousWardsAsync"/>, which
/// soft rule S2 - prefer a ward the patient has been in before - cannot be applied without, and
/// which none of the other four returns. It reads and nothing else, so the security property the
/// list exists for is unchanged.
/// </remarks>
/// <remarks>
/// Least privilege here is the type, not the prompt: whatever the model decides, the agent has no
/// method to call that would change anything. The only write in the whole flow is a human pressing
/// "Use this bed", which runs <c>POST /admissions/{id}/assign-bed</c> - the manual endpoint that
/// predates the agent.
/// </remarks>
public interface IBedAgentTools
{
    /// <summary>
    /// NIC or patient code to the patient and their open visit if they have one. No match is an
    /// answer, not an error, so this returns null rather than throwing.
    /// </summary>
    Task<ResolvedPatient?> FindPatientAsync(
        string identifier, CancellationToken cancellationToken = default);

    Task<AdmissionRequirements?> GetAdmissionRequirementsAsync(
        Guid admissionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Free, usable beds in active wards, with the ward properties the hard rules read.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT narrowed by gender or isolation, although §8.4 lists both as arguments.
    /// A bed filtered away here is a bed the agent never sees, and the blocker sentence §8.6
    /// promises - "three general beds are free, but all are in female-only wards" - is only
    /// possible if the rule that rejected each bed is a filter result the agent can count.
    /// So narrowing by rule happens once, in the FILTER step, through
    /// <c>BedPlacementRules.EnsurePlaceable</c>.
    /// </remarks>
    Task<IReadOnlyList<CandidateBed>> ListAvailableBedsAsync(
        WardType? wardType = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WardLoad>> GetWardOccupancyAsync(CancellationToken cancellationToken = default);

    /// <summary>Wards this patient has stayed in before, for soft rule S2.</summary>
    Task<IReadOnlyCollection<Guid>> ListPreviousWardsAsync(
        Guid patientId, CancellationToken cancellationToken = default);
}
