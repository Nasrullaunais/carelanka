using System.Diagnostics;
using CareLanka.Api.Data.Entities.Common;

namespace CareLanka.Api.Agents;

/// <summary>
/// Collects what assignment §9.1 requires an agent run to persist: the ordered plan, the steps
/// that completed, every allow-listed tool call with its inputs, outputs and timings, the
/// deterministic validation verdicts, and the errors.
/// </summary>
/// <remarks>
/// Nothing the model wrote reaches this class except a rationale a human is meant to read. §6
/// forbids persisting hidden reasoning, so there is deliberately no place to put it.
/// </remarks>
public sealed class AgentTrace
{
    private readonly List<WorkflowStepRecord> _steps = [];
    private readonly List<ToolCallRecord> _toolCalls = [];
    private readonly List<WorkflowValidationRecord> _validations = [];
    private readonly List<string> _errors = [];
    private readonly TimeProvider _time;

    public AgentTrace(TimeProvider time, IReadOnlyList<string> plan)
    {
        _time = time;
        Plan = plan;
    }

    public IReadOnlyList<string> Plan { get; }

    public IReadOnlyList<WorkflowStepRecord> Steps => _steps;

    public IReadOnlyList<ToolCallRecord> ToolCalls => _toolCalls;

    public IReadOnlyList<WorkflowValidationRecord> Validations => _validations;

    public IReadOnlyList<string> Errors => _errors;

    public bool AllValidationsPassed => _validations.All(validation => validation.Passed);

    public IReadOnlyList<string> FailedRules => _validations
        .Where(validation => !validation.Passed)
        .Select(validation => validation.Rule)
        .Distinct()
        .ToList();

    /// <summary>
    /// Runs one allow-listed tool and records it. <paramref name="describe"/> writes a short
    /// summary of the result - counts and ids - rather than the payload, so a trace stays
    /// readable and no clinical detail is copied into a second place.
    /// </summary>
    public async Task<T> ToolAsync<T>(
        string tool, string input, Func<Task<T>> call, Func<T, string> describe)
    {
        var startedAt = _time.GetUtcNow();
        var clock = Stopwatch.StartNew();

        try
        {
            var result = await call();

            clock.Stop();

            _toolCalls.Add(new ToolCallRecord(
                tool, input, describe(result), startedAt, (int)clock.ElapsedMilliseconds, true, null));

            _steps.Add(new WorkflowStepRecord(
                tool, tool, startedAt, (int)clock.ElapsedMilliseconds, true, null));

            return result;
        }
        catch (Exception exception)
        {
            clock.Stop();

            _toolCalls.Add(new ToolCallRecord(
                tool, input, string.Empty, startedAt, (int)clock.ElapsedMilliseconds,
                false, exception.Message));

            _steps.Add(new WorkflowStepRecord(
                tool, tool, startedAt, (int)clock.ElapsedMilliseconds, false, exception.Message));

            _errors.Add($"{tool}: {exception.Message}");

            throw;
        }
    }

    /// <summary>A step that called no tool - planning, filtering, ranking, deciding.</summary>
    public StepScope Step(string step) => new(this, step, _time.GetUtcNow());

    /// <summary>
    /// One deterministic verdict. Never the model marking its own work: every rule recorded here
    /// is ordinary C# run against the database.
    /// </summary>
    public void Validate(string rule, bool passed, string? message = null)
        => _validations.Add(new WorkflowValidationRecord(rule, passed, message, _time.GetUtcNow()));

    public void Error(string message) => _errors.Add(message);

    public readonly struct StepScope : IDisposable
    {
        private readonly AgentTrace _trace;
        private readonly string _step;
        private readonly DateTimeOffset _startedAt;
        private readonly long _startedTicks;

        internal StepScope(AgentTrace trace, string step, DateTimeOffset startedAt)
        {
            _trace = trace;
            _step = step;
            _startedAt = startedAt;
            _startedTicks = Stopwatch.GetTimestamp();
        }

        public void Dispose() => _trace._steps.Add(new WorkflowStepRecord(
            _step,
            null,
            _startedAt,
            (int)Stopwatch.GetElapsedTime(_startedTicks).TotalMilliseconds,
            true,
            null));
    }
}
