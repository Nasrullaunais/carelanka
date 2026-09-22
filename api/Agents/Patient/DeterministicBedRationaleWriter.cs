using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// Composes the on-screen sentence from the facts the ranking already established, with no model
/// call and so no network, no key and no quota. It is the stand-in described on
/// <see cref="IBedRationaleWriter"/> and it is also the safe fallback once a model exists: a run
/// whose model call fails still shows a nurse why this bed came first.
/// </summary>
public sealed class DeterministicBedRationaleWriter : IBedRationaleWriter
{
    public Task<string?> WriteAsync(BedRationaleContext context, CancellationToken ct = default)
    {
        var sentences = new List<string> { Placement(context) };

        if (context.IsInfectious && context.HasIsolation)
        {
            sentences.Add("It can isolate, which this patient needs.");
        }

        if (context.SeenThisWardBefore)
        {
            sentences.Add("The patient has been on this ward before.");
        }

        sentences.Add(Load(context));

        return Task.FromResult<string?>(string.Join(" ", sentences));
    }

    private static string Placement(BedRationaleContext context)
        => context.IsDowngrade
            ? $"{context.WardName} is one step below {BedRuleNames.Spoken(context.Category)}, "
                + "so a Duty Manager has to approve it."
            : $"{context.WardName} matches a {BedRuleNames.Spoken(context.Category)} patient.";

    private static string Load(BedRationaleContext context)
    {
        if (context.UsableBedsInWard == 0)
        {
            return "It is the ward's only usable bed.";
        }

        return context.FreeBedsInWard switch
        {
            <= 1 => $"It is the last free bed on {context.WardName}.",
            _ => $"{context.FreeBedsInWard} of {context.UsableBedsInWard} beds on "
                + $"{context.WardName} are free, so it is the emptier ward."
        };
    }
}
