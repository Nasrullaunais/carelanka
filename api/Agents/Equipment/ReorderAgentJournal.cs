using System.Diagnostics;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Agents.Equipment;

/// <summary>
/// What the run did, as it does it - same shape and purpose as <c>CareAgentJournal</c>. Every
/// step with its timing, every tool call by name, every error, and the verdict of RT1, built as
/// the run goes rather than reconstructed afterwards.
/// </summary>
public sealed class ReorderAgentJournal
{
    private readonly List<BedAgentStep> _steps = [];
    private readonly List<string> _errors = [];

    public bool ValidationPassed { get; set; } = true;

    public string? FailedRule { get; set; }

    public TResult Step<TResult>(string step, Func<TResult> work)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var clock = Stopwatch.StartNew();

        try
        {
            var result = work();
            Record(step, tool: null, startedAt, clock, ok: true, error: null);

            return result;
        }
        catch (Exception failure)
        {
            Record(step, tool: null, startedAt, clock, ok: false, failure.Message);
            throw;
        }
    }

    public async Task<TResult> ToolAsync<TResult>(string step, string tool, Func<Task<TResult>> call)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var clock = Stopwatch.StartNew();

        try
        {
            var result = await call();
            Record(step, tool, startedAt, clock, ok: true, error: null);

            return result;
        }
        catch (Exception failure)
        {
            Record(step, tool, startedAt, clock, ok: false, failure.Message);
            throw;
        }
    }

    public async Task<TResult> StepAsync<TResult>(string step, Func<Task<TResult>> work)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var clock = Stopwatch.StartNew();

        try
        {
            var result = await work();
            Record(step, tool: null, startedAt, clock, ok: true, error: null);

            return result;
        }
        catch (Exception failure)
        {
            Record(step, tool: null, startedAt, clock, ok: false, failure.Message);
            throw;
        }
    }

    public void Fail(Exception failure) => _errors.Add(failure.Message);

    public ReorderAgentRun Finish(ReorderAgentOutcome outcome, ReorderDraftCandidate? draft, int attempts)
        => new(ReorderAgent.Plan, _steps, outcome, draft, ValidationPassed, FailedRule, _errors, attempts);

    /// <summary>
    /// Every attempt is spent. The suggestion row survives with no draft - a human can still edit
    /// the threshold by hand, exactly as they could before this agent existed.
    /// </summary>
    public ReorderAgentRun SafeFailure(int attempts)
        => new(ReorderAgent.Plan, _steps, ReorderAgentOutcome.Failed, null, false, null, _errors, attempts);

    private void Record(
        string step, string? tool, DateTimeOffset startedAt, Stopwatch clock, bool ok, string? error)
    {
        clock.Stop();

        _steps.Add(new BedAgentStep
        {
            Step = step,
            Tool = tool,
            StartedAt = startedAt,
            DurationMs = (int)clock.ElapsedMilliseconds,
            Ok = ok,
            Error = error
        });
    }
}

public sealed record ReorderAgentRun(
    IReadOnlyList<string> Plan,
    IReadOnlyList<BedAgentStep> Steps,
    ReorderAgentOutcome Outcome,
    ReorderDraftCandidate? Draft,
    bool ValidationPassed,
    string? FailedRule,
    IReadOnlyList<string> Errors,
    int Attempts);
