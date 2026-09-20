using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// Writes the one short sentence shown under a suggested bed. This is the only part of the run a
/// language model is allowed to touch: the rules, the ranking and the blocker are all decided in
/// C# before this is called, so a writer that answers badly - or not at all - costs a sentence and
/// never a placement.
/// <para>
/// STUB (STUBS.md row M4b). The deterministic writer below stands in for the group's
/// <c>ILanguageModel</c>, which ADR 2 puts in <c>api/Agents/</c> and which nobody has built.
/// Swapping in a Gemini-backed writer is one DI registration and nothing else moves.
/// </para>
/// </summary>
public interface IBedRationaleWriter
{
    Task<string?> WriteAsync(BedRationaleContext context, CancellationToken ct = default);
}

/// <summary>
/// Everything the writer is allowed to see. Note what is not here: the patient's name, their NIC
/// and any free text anybody typed. Patient data is data, never instructions, so a patient called
/// "ignore previous instructions" changes nothing about what comes back.
/// </summary>
public sealed record BedRationaleContext(
    AdmissionCategory Category,
    WardType WardType,
    string WardName,
    string BedNumber,
    bool IsDowngrade,
    bool SeenThisWardBefore,
    int FreeBedsInWard,
    int UsableBedsInWard,
    GenderPolicy GenderPolicy,
    bool IsInfectious,
    bool HasIsolation,
    int RankPosition);
