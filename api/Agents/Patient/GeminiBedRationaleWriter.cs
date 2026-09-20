using System.Text.Json;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// Asks the language model for the one sentence shown under a suggested bed, and for nothing else.
/// By the time this runs the bed has already been chosen and checked twice against the hard rules,
/// so the worst a bad answer can do is read badly.
/// </summary>
/// <remarks>
/// The model is handed a fixed instruction and a JSON object of facts - never free text somebody
/// typed, never a patient's name or NIC. Patient data is data, not instructions, so there is no
/// sentence a patient record could contain that changes what comes back.
/// <para>
/// When no model is configured, or the call fails, this falls through to
/// <see cref="DeterministicBedRationaleWriter"/>. That is not a stub standing in for unbuilt work:
/// a dead key or a flat quota on the day should cost the sentence's polish, not the suggestion.
/// </para>
/// </remarks>
public sealed class GeminiBedRationaleWriter : IBedRationaleWriter
{
    private const string Instruction = """
        You write one short sentence for a hospital bed suggestion screen, read by a ward nurse.
        You are given facts that have already been decided. You do not decide anything.
        Reply with JSON only: {"rationale": "<one sentence, at most 25 words>"}.
        State why this bed suits this patient, using only the facts given.
        Never invent a fact. Never mention a patient, a name or a medical condition.
        Never suggest a different bed, and never question the care level.
        """;

    private readonly ILanguageModel _model;
    private readonly DeterministicBedRationaleWriter _fallback;
    private readonly ILogger<GeminiBedRationaleWriter> _log;

    public GeminiBedRationaleWriter(
        ILanguageModel model,
        DeterministicBedRationaleWriter fallback,
        ILogger<GeminiBedRationaleWriter> log)
    {
        _model = model;
        _fallback = fallback;
        _log = log;
    }

    public async Task<string?> WriteAsync(BedRationaleContext context, CancellationToken ct = default)
    {
        if (!_model.IsConfigured)
        {
            return await _fallback.WriteAsync(context, ct);
        }

        var result = await _model.CompleteJsonAsync(Instruction, Facts(context), ct);

        if (!result.Ok)
        {
            _log.LogWarning("Bed rationale fell back to the written sentence: {Error}", result.Error);

            return await _fallback.WriteAsync(context, ct);
        }

        return Parse(result.Json) ?? await _fallback.WriteAsync(context, ct);
    }

    private static string Facts(BedRationaleContext context)
        => JsonSerializer.Serialize(new
        {
            care_level = BedRuleNames.Spoken(context.Category),
            ward_type = BedRuleNames.Spoken(context.WardType),
            ward_name = context.WardName,
            bed_number = context.BedNumber,
            is_one_level_below_the_care_level = context.IsDowngrade,
            patient_has_been_on_this_ward_before = context.SeenThisWardBefore,
            free_beds_in_this_ward = context.FreeBedsInWard,
            usable_beds_in_this_ward = context.UsableBedsInWard,
            ward_gender_policy = EnumWire.ToWire(context.GenderPolicy),
            patient_is_infectious = context.IsInfectious,
            bed_can_isolate = context.HasIsolation,
            rank = context.RankPosition == 0 ? "best" : "alternative"
        });

    /// <summary>
    /// A malformed answer is not repaired or guessed at - it is treated as no answer, which is
    /// what the deterministic writer is there for.
    /// </summary>
    private string? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("rationale", out var rationale)
                || rationale.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var sentence = rationale.GetString()?.Trim();

            return string.IsNullOrWhiteSpace(sentence) ? null : Clip(sentence);
        }
        catch (JsonException exception)
        {
            _log.LogWarning(exception, "The language model returned a rationale that is not JSON.");

            return null;
        }
    }

    private static string Clip(string sentence)
        => sentence.Length <= 300 ? sentence : sentence[..300];
}
