using System.Diagnostics;
using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// What the run did, as it does it: every step with its timing, every tool call by name, every
/// error, and the verdict of the deterministic validator. This is the record assignment section
/// 9.1 asks to be persisted, and it is built as the run goes rather than reconstructed afterwards
/// from what the code was supposed to have done.
/// </summary>
public sealed class BedAgentJournal
{
    private readonly List<BedAgentStep> _steps = [];
    private readonly List<BedToolCall> _tools = [];
    private readonly List<string> _errors = [];

    public BedWorkflowValidation Validation { get; } = new() { Passed = true };

    /// <summary>
    /// A retry starts its steps again. The errors stay - they are why there was a second attempt.
    /// </summary>
    public void Restart()
    {
        _steps.Clear();
        _tools.Clear();
    }

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

    public async Task<TResult> ToolAsync<TResult>(
        string step, string tool, Func<Task<TResult>> call)
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

    public void Fail(Exception failure) => _errors.Add(failure.Message);

    public BedAgentRun Finish(BedSuggestionAnswer answer, Guid? admissionId, int attempts)
        => new(BedAgent.Plan, _steps, _tools, answer, admissionId, Validation, _errors, attempts);

    /// <summary>
    /// The safe failure. Every attempt is spent, so the run says so in a sentence a nurse can act
    /// on - assign a bed by hand - rather than showing an empty panel or an exception.
    /// </summary>
    public BedAgentRun SafeFailure(int attempts)
        => new(
            BedAgent.Plan,
            _steps,
            _tools,
            BedAgent.Blocked(BedAgentOutcome.Failed, BedBlockers.AgentFailed()),
            AdmissionId: null,
            Validation,
            _errors,
            attempts);

    private void Record(
        string step,
        string? tool,
        DateTimeOffset startedAt,
        Stopwatch clock,
        bool ok,
        string? error)
    {
        clock.Stop();
        var durationMs = (int)clock.ElapsedMilliseconds;

        _steps.Add(new BedAgentStep
        {
            Step = step,
            Tool = tool,
            StartedAt = startedAt,
            DurationMs = durationMs,
            Ok = ok,
            Error = error
        });

        if (tool is not null)
        {
            _tools.Add(new BedToolCall(tool, step, startedAt, durationMs, ok));
        }
    }
}
