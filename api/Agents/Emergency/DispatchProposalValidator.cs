using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Emergency;

namespace CareLanka.Api.Agents.Emergency;

public static class DispatchProposalValidator
{
    public const string AmbulanceStillEligible = "ambulance_still_eligible";
    public const string SourceDispatchStillPrePickup = "source_dispatch_still_pre_pickup";
    public const string SourceCallHasReplacement = "source_call_has_replacement";

    public static DispatchValidationResult AmbulanceEligible(bool isEligible, DateTimeOffset now)
        => new()
        {
            Check = AmbulanceStillEligible,
            Passed = isEligible,
            Detail = isEligible
                ? "The proposed ambulance is still eligible."
                : "The proposed ambulance is no longer eligible - it may have been sent elsewhere.",
            CheckedAt = now
        };

    public static DispatchValidationResult SourcePrePickup(DispatchStatus sourceStatus, DateTimeOffset now)
    {
        var stillPrePickup = sourceStatus.IsPrePickup();

        return new DispatchValidationResult
        {
            Check = SourceDispatchStillPrePickup,
            Passed = stillPrePickup,
            Detail = stillPrePickup
                ? "The source ambulance has not yet reached its patient."
                : "The source crew reached their patient while this proposal was waiting.",
            CheckedAt = now
        };
    }

    public static DispatchValidationResult ReplacementAvailable(bool hasReplacement, DateTimeOffset now)
        => new()
        {
            Check = SourceCallHasReplacement,
            Passed = hasReplacement,
            Detail = hasReplacement
                ? "A replacement ambulance is available for the displaced call."
                : "No replacement ambulance is free for the displaced call.",
            CheckedAt = now
        };
}
