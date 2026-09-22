using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// The one part of the run a language model is allowed to touch. It is handed a shortlist of beds
/// that have already passed every hard rule and are already ranked, plus what a clinician typed
/// about this patient, and it answers two things: which of those beds it would take, and why in
/// one sentence.
/// <para>
/// The choice is bounded before it is made. Every bed on the shortlist is legal, sits at the same
/// care level as the deterministic top pick, and needs the same person's approval - so the worst a
/// bad answer can do is pick a second-best bed and explain it badly. It cannot reach a bed the
/// rules refused, cannot change a care level, and cannot write anything. Step 7 re-checks the
/// answer in C# afterwards regardless.
/// </para>
/// </summary>
public interface IBedAdvisor
{
    Task<BedAdvice> AdviseAsync(BedAdviceContext context, CancellationToken ct = default);
}

/// <summary>
/// Everything the model sees. The patient's name, NIC and patient code are deliberately not here:
/// the choice does not depend on them, and what is not sent cannot leak.
/// </summary>
/// <remarks>
/// <see cref="Notes"/> is free text a clinician typed, and free text is data, never instructions.
/// It is sent as a JSON value under its own key, and the instruction tells the model that the
/// notes describe a patient rather than address it - so a record containing "ignore previous
/// instructions" changes nothing about what comes back.
/// </remarks>
public sealed record BedAdviceContext(
    AdmissionCategory Category,
    int? Age,
    Gender Gender,
    AdmissionUrgency Urgency,
    bool IsInfectious,
    PatientNotes Notes,
    IReadOnlyList<BedAdviceCandidate> Shortlist);

public sealed record BedAdviceCandidate(
    string BedNumber,
    string WardName,
    WardType WardType,
    bool HasIsolation,
    int FreeBedsInWard,
    int UsableBedsInWard,
    bool SeenThisWardBefore,
    IReadOnlyList<string> Reasons);

/// <summary>
/// A bed number off the shortlist and the sentence to show under it. Both may be null: no answer
/// is an ordinary outcome, and it leaves the deterministic pick and the written sentence standing.
/// </summary>
public sealed record BedAdvice(string? BedNumber, string? Reason)
{
    public static readonly BedAdvice None = new(null, null);
}
