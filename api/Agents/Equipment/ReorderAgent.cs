using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Equipment;
using Microsoft.Extensions.Options;

namespace CareLanka.Api.Agents.Equipment;

/// <summary>
/// The reorder-threshold advisor. One run reads one medicine's recent dispensing, asks the model
/// to suggest a new reorder threshold, and re-checks the result deterministically (RT1) before
/// anyone sees it. It never writes the threshold itself - applying a suggestion is a separate,
/// deliberate action a human takes through the plain item-edit endpoint.
/// </summary>
public sealed class ReorderAgent : IReorderAgent
{
    /// <summary>Two retries, then a safe failure - same budget as the bed and care agents.</summary>
    public const int MaxAttempts = 3;

    /// <summary>Enough days to see a trend without the prompt growing without bound.</summary>
    public const int HistoryWindowDays = 60;

    public static readonly IReadOnlyList<string> Plan =
    [
        GatherItem,
        GatherHistory,
        Draft,
        Validate,
        Pause
    ];

    private const string GatherItem = "gather_item";
    private const string GatherHistory = "gather_dispensing_history";
    private const string Draft = "draft_suggestion";
    private const string Validate = "validate_deterministically";
    private const string Pause = "pause_for_review";

    private readonly IReorderAgentTools _tools;
    private readonly IReorderAdvisor _advisor;
    private readonly int _leadTimeDays;
    private readonly ILogger<ReorderAgent> _logger;

    public ReorderAgent(
        IReorderAgentTools tools,
        IReorderAdvisor advisor,
        IOptions<EquipmentOptions> options,
        ILogger<ReorderAgent> logger)
    {
        _tools = tools;
        _advisor = advisor;
        _leadTimeDays = options.Value.ReorderLeadTimeDays;
        _logger = logger;
    }

    public async Task<ReorderAgentRun> RunAsync(ReorderAgentRequest request, CancellationToken ct = default)
    {
        var journal = new ReorderAgentJournal();

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await AttemptAsync(request, journal, attempt, ct);
            }
            catch (ApiException)
            {
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
                        failure, "Reorder agent attempt {Attempt} failed; retrying.", attempt);
                    continue;
                }

                _logger.LogError(
                    failure, "Reorder agent gave up after {Attempts} attempts.", MaxAttempts);

                return journal.SafeFailure(MaxAttempts);
            }
        }

        return journal.SafeFailure(MaxAttempts);
    }

    private async Task<ReorderAgentRun> AttemptAsync(
        ReorderAgentRequest request, ReorderAgentJournal journal, int attempt, CancellationToken ct)
    {
        var item = await journal.ToolAsync(
            GatherItem, "get_item",
            () => _tools.GetItemAsync(request.PharmacyItemId, ct));

        if (item is null)
        {
            throw new NotFoundException("Pharmacy item", request.PharmacyItemId);
        }

        var history = await journal.ToolAsync(
            GatherHistory, "get_dispensing_history",
            () => _tools.GetDispensingHistoryAsync(request.PharmacyItemId, HistoryWindowDays, ct));

        var context = new ReorderAdviceContext(item, history);

        var candidate = await journal.StepAsync(Draft, () => _advisor.SuggestAsync(context, ct));

        var validated = journal.Step(
            Validate, () => ReorderSuggestionValidator.Validate(candidate, item, history));

        var final = candidate;

        if (!validated.Passed)
        {
            // A suggestion breaking RT1 never reaches a reviewer. The deterministic formula is
            // built from the same numbers the model saw, so it always passes.
            _logger.LogWarning(
                "The reorder advisor's draft failed {FailedRule}; falling back to the deterministic "
                + "suggestion. The rejected suggestion was: {SuggestedThreshold}",
                validated.FailedRule, candidate.SuggestedThreshold);

            final = (await new DeterministicReorderAdvisor(_leadTimeDays).SuggestAsync(context, ct))
                with
            { Source = ReorderSuggestionSource.ModelRejected };

            validated = ReorderSuggestionValidator.Validate(final, item, history);
        }

        journal.ValidationPassed = validated.Passed;
        journal.FailedRule = validated.FailedRule;

        journal.Step(Pause, () => 0);

        return journal.Finish(ReorderAgentOutcome.Suggested, final, attempt);
    }
}

public interface IReorderAgent
{
    Task<ReorderAgentRun> RunAsync(ReorderAgentRequest request, CancellationToken ct = default);
}

public sealed record ReorderAgentRequest(Guid PharmacyItemId);
