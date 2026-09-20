using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// The Bed and Patient Details Agent. One run answers one counter question - who is this, and
/// where do they go - by planning, calling its four read-only tools, dropping every bed that fails
/// a hard rule, ranking what is left, deciding a best pick with selectable alternatives, checking
/// that decision again deterministically, and then stopping.
/// <para>
/// It writes nothing. No tool here changes the database, so a run nobody acts on leaves the
/// hospital exactly as it found it and takes no bed out of circulation. The bed is claimed only
/// when a human presses a button, on the manual endpoint that predates the agent.
/// </para>
/// </summary>
public sealed class BedAgent : IBedAgent
{
    /// <summary>
    /// Two retries, then a safe failure. A failure is a recorded outcome carrying a blocker a
    /// nurse can act on, never an exception thrown at the screen.
    /// </summary>
    public const int MaxAttempts = 3;

    public static readonly IReadOnlyList<string> Plan =
    [
        ResolvePatient,
        ReadRequirements,
        ListCandidates,
        ApplyHardRules,
        RankOnSoftRules,
        DecideBest,
        WeighNotes,
        Validate,
        Pause
    ];

    private const string ResolvePatient = "resolve_patient";
    private const string ReadRequirements = "read_admission_requirements";
    private const string ListCandidates = "list_candidate_beds";
    private const string ApplyHardRules = "apply_hard_rules";
    private const string RankOnSoftRules = "rank_on_soft_rules";
    private const string DecideBest = "decide_best_and_alternatives";
    private const string WeighNotes = "weigh_patient_notes";
    private const string Validate = "validate_deterministically";
    private const string Pause = "pause_for_approval";

    /// <summary>
    /// How many wards the model is allowed to choose between, one bed each. Five is enough for a
    /// real choice and short enough that a nurse can check the answer against the bed board.
    /// </summary>
    private const int ShortlistSize = 5;

    private readonly IBedAgentTools _tools;
    private readonly IBedRationaleWriter _rationale;
    private readonly IBedAdvisor _advisor;
    private readonly ILogger<BedAgent> _logger;

    public BedAgent(
        IBedAgentTools tools,
        IBedRationaleWriter rationale,
        IBedAdvisor advisor,
        ILogger<BedAgent> logger)
    {
        _tools = tools;
        _rationale = rationale;
        _advisor = advisor;
        _logger = logger;
    }

    public async Task<BedAgentRun> RunAsync(
        BedAgentRequest request,
        Func<IReadOnlyList<BedAgentStep>, CancellationToken, Task>? onProgress = null,
        CancellationToken ct = default)
    {
        var journal = new BedAgentJournal();

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await AttemptAsync(request, journal, attempt, onProgress, ct);
            }
            catch (ApiException)
            {
                // A 404 or a 409 is the caller being told something true about this visit. Asking
                // again would get the same answer, so it leaves rather than burning the retries.
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception failure)
            {
                journal.Fail(failure);

                if (attempt < MaxAttempts)
                {
                    _logger.LogWarning(
                        failure, "Bed agent attempt {Attempt} failed; retrying.", attempt);
                    continue;
                }

                _logger.LogError(
                    failure, "Bed agent gave up after {Attempts} attempts.", MaxAttempts);

                return journal.SafeFailure(MaxAttempts);
            }
        }

        return journal.SafeFailure(MaxAttempts);
    }

    private async Task<BedAgentRun> AttemptAsync(
        BedAgentRequest request,
        BedAgentJournal journal,
        int attempt,
        Func<IReadOnlyList<BedAgentStep>, CancellationToken, Task>? onProgress,
        CancellationToken ct)
    {
        journal.Restart();

        var resolved = await ResolveAsync(request, journal, ct);

        if (resolved.Answer is { } settled)
        {
            return journal.Finish(settled, resolved.AdmissionId, attempt);
        }

        var requirements = resolved.Requirements!;

        if (!BedPlacementRules.RequiresBed(requirements.Category))
        {
            return journal.Finish(
                Blocked(
                    BedAgentOutcome.VisitNeedsNoBed,
                    BedBlockers.NoBedRequired(requirements.Category)),
                resolved.AdmissionId,
                attempt);
        }

        var candidates = await journal.ToolAsync(
            ListCandidates,
            BedAgentTools.ListAvailableBeds,
            () => _tools.ListAvailableBedsAsync(null, ct));

        var load = await journal.ToolAsync(
            ListCandidates,
            BedAgentTools.GetWardOccupancy,
            () => _tools.GetWardOccupancyAsync(ct));

        var previousWards = await _tools.ListPreviousWardsAsync(requirements.PatientId, ct);

        var filtered = journal.Step(
            ApplyHardRules, () => BedSuggestionPlanner.Filter(requirements, candidates));

        var ranked = journal.Step(
            RankOnSoftRules,
            () => BedSuggestionPlanner.Rank(requirements, filtered.Survivors, load, previousWards));

        var decided = journal.Step(
            DecideBest, () => BedSuggestionPlanner.Decide(requirements, filtered, ranked));

        var advised = await AdviseAsync(requirements, decided, journal, onProgress, ct);

        var checkedAnswer = journal.Step(
            Validate, () => BedSuggestionValidator.Recheck(requirements, advised, journal.Validation));

        await DescribeAsync(requirements, checkedAnswer, load, previousWards, ct);

        journal.Step(Pause, () => 0);

        return journal.Finish(checkedAnswer, resolved.AdmissionId, attempt);
    }

    /// <summary>
    /// Step 6b: let the model choose between beds the rules already agreed to, on the one thing
    /// the rules cannot read - what a clinician wrote about this patient.
    /// </summary>
    /// <remarks>
    /// The shortlist only ever holds beds at the same care level as the ranked pick, so whichever
    /// one comes back needs the same person's approval and carries the same downgrade flag. A
    /// model that answers badly costs a place in the order, never a rule.
    /// </remarks>
    private async Task<BedSuggestionAnswer> AdviseAsync(
        AdmissionRequirements requirements,
        BedSuggestionAnswer decided,
        BedAgentJournal journal,
        Func<IReadOnlyList<BedAgentStep>, CancellationToken, Task>? onProgress,
        CancellationToken ct)
    {
        if (decided.Best is not { } best)
        {
            journal.Step(WeighNotes, () => 0);

            return decided;
        }

        var notes = await journal.ToolAsync(
            WeighNotes,
            BedAgentTools.GetPatientNotes,
            () => _tools.GetPatientNotesAsync(requirements.PatientId, ct));

        // One bed per ward. Five beds in the same ward is not a choice - they differ by a number
        // on a door - and it would spend the model's one question on nothing. The interesting
        // question is which ward suits this patient, so the shortlist is the best bed in each.
        var shortlist = new[] { best }
            .Concat(decided.Alternatives.Where(bed => bed.IsDowngrade == best.IsDowngrade))
            .GroupBy(bed => bed.Candidate.Ward.Id)
            .Select(ward => ward.First())
            .Take(ShortlistSize)
            .ToList();

        if (notes.IsEmpty || shortlist.Count < 2)
        {
            // Nothing to read, or nothing to choose between. A model call here would cost a second
            // of a nurse's time to confirm the order it was already given.
            return decided;
        }

        // Everything up to here runs in milliseconds. The model call below is the one step worth
        // a nurse's screen knowing about, so this is the one checkpoint the run persists mid-flight.
        if (onProgress is not null)
        {
            await onProgress(journal.StepsSoFar, ct);
        }

        var context = new BedAdviceContext(
            requirements.Category,
            BedPlacementRules.AgeOn(
                requirements.DateOfBirth, DateOnly.FromDateTime(DateTime.UtcNow)),
            requirements.Gender,
            requirements.Urgency,
            requirements.IsInfectious,
            notes,
            shortlist.Select(Candidate).ToList());

        var advice = await journal.StepAsync(
            WeighNotes, () => _advisor.AdviseAsync(context, ct));

        var chosen = shortlist.FirstOrDefault(bed =>
            bed.Candidate.Bed.BedNumber == advice.BedNumber) ?? best;

        chosen.Rationale = advice.Reason;

        if (ReferenceEquals(chosen, best))
        {
            return decided;
        }

        _logger.LogInformation(
            "The bed advisor moved {BedNumber} ahead of the ranked pick {RankedBedNumber}.",
            chosen.Candidate.Bed.BedNumber,
            best.Candidate.Bed.BedNumber);

        return decided with
        {
            Best = chosen,
            Alternatives = new[] { best }
                .Concat(decided.Alternatives.Where(bed => !ReferenceEquals(bed, chosen)))
                .ToList()
        };
    }

    private static BedAdviceCandidate Candidate(RankedBed bed)
        => new(
            bed.Candidate.Bed.BedNumber,
            bed.Candidate.Ward.Name,
            bed.Candidate.Ward.WardType,
            bed.Candidate.Bed.HasIsolation,
            bed.Load is null ? 0 : Math.Max(0, bed.Load.UsableBeds - bed.Load.ClaimedBeds),
            bed.Load?.UsableBeds ?? 0,
            bed.SeenThisWardBefore,
            bed.Reasons);

    private async Task<ResolvedRun> ResolveAsync(
        BedAgentRequest request, BedAgentJournal journal, CancellationToken ct)
    {
        // The identifier wins when both are present. The service resolves one to guard the run,
        // and passes both, so without this a desk lookup by NIC would be journalled as though the
        // nurse had picked the patient off the board - which is not what happened.
        if (request.PatientIdentifier is null && request.AdmissionId is { } admissionId)
        {
            var requirements = await journal.ToolAsync(
                ResolvePatient,
                BedAgentTools.GetAdmissionRequirements,
                () => _tools.GetAdmissionRequirementsAsync(admissionId, ct))
                ?? throw new NotFoundException("Admission", admissionId);

            journal.Step(ReadRequirements, () => 0);

            return new ResolvedRun(requirements, admissionId, Answer: null);
        }

        var lookup = await journal.ToolAsync(
            ResolvePatient,
            BedAgentTools.FindPatient,
            () => _tools.FindPatientAsync(request.PatientIdentifier!, ct));

        if (lookup is null)
        {
            return new ResolvedRun(
                Requirements: null,
                AdmissionId: null,
                Blocked(BedAgentOutcome.PatientNotFound, BedBlockers.NoSuchPatient()));
        }

        if (lookup.OpenAdmission is null)
        {
            return new ResolvedRun(
                Requirements: null,
                AdmissionId: null,
                Blocked(
                    BedAgentOutcome.PatientNotFound,
                    BedBlockers.NoOpenAdmission(lookup.Patient.FullName)));
        }

        var resolved = journal.Step(
            ReadRequirements,
            () => BedAgentTools.Requirements(lookup.OpenAdmission, lookup.Patient));

        return new ResolvedRun(resolved, lookup.OpenAdmission.Id, Answer: null);
    }

    private async Task DescribeAsync(
        AdmissionRequirements requirements,
        BedSuggestionAnswer answer,
        IReadOnlyDictionary<Guid, WardLoad> load,
        IReadOnlyCollection<Guid> previousWards,
        CancellationToken ct)
    {
        var position = 0;

        foreach (var bed in new[] { answer.Best }.Concat(answer.Alternatives).OfType<RankedBed>())
        {
            if (!string.IsNullOrWhiteSpace(bed.Rationale))
            {
                position++;

                continue;
            }

            var wardLoad = load.TryGetValue(bed.Candidate.Ward.Id, out var found) ? found : null;

            bed.Rationale = await _rationale.WriteAsync(
                new BedRationaleContext(
                    requirements.Category,
                    bed.Candidate.Ward.WardType,
                    bed.Candidate.Ward.Name,
                    bed.Candidate.Bed.BedNumber,
                    bed.IsDowngrade,
                    previousWards.Contains(bed.Candidate.Ward.Id),
                    wardLoad is null ? 0 : Math.Max(0, wardLoad.UsableBeds - wardLoad.ClaimedBeds),
                    wardLoad?.UsableBeds ?? 0,
                    bed.Candidate.Ward.GenderPolicy,
                    requirements.IsInfectious,
                    bed.Candidate.Bed.HasIsolation,
                    position++),
                ct);
        }
    }

    internal static BedSuggestionAnswer Blocked(
        BedAgentOutcome outcome, BedSuggestionBlocker blocker)
        => new(outcome, Best: null, Array.Empty<RankedBed>(), blocker, RequiresApprovalBy: null);

    private sealed record ResolvedRun(
        AdmissionRequirements? Requirements, Guid? AdmissionId, BedSuggestionAnswer? Answer);
}

public sealed record BedAgentRequest(Guid? AdmissionId, string? PatientIdentifier);

public sealed record BedAgentRun(
    IReadOnlyList<string> Plan,
    IReadOnlyList<BedAgentStep> Steps,
    IReadOnlyList<BedToolCall> ToolCalls,
    BedSuggestionAnswer Answer,
    Guid? AdmissionId,
    BedWorkflowValidation Validation,
    IReadOnlyList<string> Errors,
    int Attempts);

public sealed record BedToolCall(
    string Tool, string Step, DateTimeOffset StartedAt, int DurationMs, bool Ok);
