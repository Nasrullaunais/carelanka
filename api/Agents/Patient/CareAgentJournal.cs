using System.Diagnostics;
using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// What the run did, as it does it - same shape and purpose as <see cref="BedAgentJournal"/>.
/// Every step with its timing, every tool call by name, every error, and the verdict of the
/// deterministic validator, built as the run goes rather than reconstructed afterwards.
/// </summary>
public sealed class CareAgentJournal
{
    private readonly List<BedAgentStep> _steps = [];
    private readonly List<string> _errors = [];

    public CareWorkflowValidation Validation { get; } = new() { Passed = true };

    public bool RedFlag { get; set; }

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

    public CareAgentRun Finish(CareAgentOutcome outcome, CareDraftCandidate? draft, int attempts)
        => new(CareAgent.Plan, _steps, outcome, draft, RedFlag, Validation, _errors, attempts);

    /// <summary>
    /// Every attempt is spent. The recommendation row survives untouched - a doctor or nurse can
    /// still read the patient's own report and act without the draft.
    /// </summary>
    public CareAgentRun SafeFailure(int attempts)
        => new(CareAgent.Plan, _steps, CareAgentOutcome.Failed, null, RedFlag, Validation, _errors, attempts);

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

public sealed record CareAgentRun(
    IReadOnlyList<string> Plan,
    IReadOnlyList<BedAgentStep> Steps,
    CareAgentOutcome Outcome,
    CareDraftCandidate? Draft,
    bool RedFlag,
    CareWorkflowValidation Validation,
    IReadOnlyList<string> Errors,
    int Attempts);
