using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// Step 7: the same rulebook, run again over exactly what a human is about to see. Nothing here is
/// the model's - it is the third of three passes, the first being the filter that built this list
/// and the last being the row-locked check inside assign-bed when somebody presses the button.
/// <para>
/// A bed that fails here is removed from the answer rather than shown with a warning. The point of
/// three passes is that the two cheap ones can be wrong without a patient reaching the wrong bed.
/// </para>
/// </summary>
public static class BedSuggestionValidator
{
    public static BedSuggestionAnswer Recheck(
        AdmissionRequirements requirements,
        BedSuggestionAnswer answer,
        BedWorkflowValidation validation)
    {
        var failed = new List<string>();

        var survivors = new[] { answer.Best }
            .Concat(answer.Alternatives)
            .OfType<RankedBed>()
            .Where(bed => Holds(requirements, bed, failed))
            .ToList();

        validation.Passed = failed.Count == 0;
        validation.FailedRules = failed;

        if (validation.Passed)
        {
            return answer;
        }

        if (survivors.Count == 0)
        {
            return BedAgent.Blocked(
                BedAgentOutcome.NoBedAvailable,
                BedBlockers.NothingFree(
                    requirements,
                    new BedFilterResult(
                        Array.Empty<PlacedBed>(), Array.Empty<DroppedBed>(), FreeBedsSeen: 0)));
        }

        var best = survivors[0];

        return answer with
        {
            Outcome = best.IsDowngrade
                ? BedAgentOutcome.ProposedWithDowngrade
                : BedAgentOutcome.Proposed,
            Best = best,
            Alternatives = survivors.Skip(1).ToList(),
            RequiresApprovalBy = best.RequiresDutyManager
                ? BedApproverRole.DutyManager
                : BedApproverRole.WardNurse
        };
    }

    private static bool Holds(
        AdmissionRequirements requirements, RankedBed bed, List<string> failed)
    {
        try
        {
            BedPlacementRules.EnsurePlaceable(
                requirements.Category,
                requirements.Gender,
                requirements.DateOfBirth,
                requirements.IsInfectious,
                bed.Candidate.Ward,
                bed.Candidate.Bed);

            return true;
        }
        catch (ConflictException refusal)
        {
            failed.Add($"{bed.Candidate.Bed.BedNumber}:{BedRuleNames.ForRefusal(refusal.Code)}");

            return false;
        }
    }
}
