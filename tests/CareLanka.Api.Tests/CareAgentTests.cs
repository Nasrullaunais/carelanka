using CareLanka.Api.Agents.Patient;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CareLanka.Api.Tests;

/// <summary>
/// A draft that breaks a rule is sent back to the model once with what was wrong, instead of the
/// patient getting the backup note for one bad sentence. The validator still has the last word.
/// </summary>
public sealed class CareAgentTests
{
    private const string Report = "My head hurts, can I have something for it?";

    [Fact]
    public async Task A_draft_that_breaks_a_rule_is_revised_and_the_revision_is_used()
    {
        var advisor = new ScriptedAdvisor(
            Model("You can take ibuprofen for that headache."),
            Model("I am sorry your head hurts. Your nurse will check your record before giving you anything."));

        var run = await Run(advisor);

        Assert.Equal(2, advisor.Contexts.Count);
        Assert.Equal(CareDraftSource.Model, run.Draft!.Source);
        Assert.StartsWith("I am sorry your head hurts.", run.Draft.Message);
        Assert.True(run.Validation.Passed);
    }

    [Fact]
    public async Task The_revision_request_carries_the_rejected_draft_and_what_was_wrong_with_it()
    {
        var advisor = new ScriptedAdvisor(
            Model("You can take ibuprofen for that headache."),
            Model("Your nurse will check your record before giving you anything."));

        await Run(advisor);

        var revision = advisor.Contexts[1].Revision;

        Assert.NotNull(revision);
        Assert.Equal("You can take ibuprofen for that headache.", revision.RejectedMessage);
        Assert.Contains(revision.Problems, problem => problem.Contains("ibuprofen"));
        Assert.Null(advisor.Contexts[0].Revision);
    }

    [Fact]
    public async Task A_draft_that_passes_is_not_sent_back()
    {
        var advisor = new ScriptedAdvisor(
            Model("I am sorry your head hurts. Your nurse will check your record first."));

        var run = await Run(advisor);

        Assert.Single(advisor.Contexts);
        Assert.Equal(CareDraftSource.Model, run.Draft!.Source);
    }

    [Fact]
    public async Task A_revision_that_still_breaks_a_rule_falls_back_to_the_backup_note()
    {
        var advisor = new ScriptedAdvisor(
            Model("You can take ibuprofen for that headache."),
            Model("Try ibuprofen, it usually helps."));

        var run = await Run(advisor);

        Assert.Equal(2, advisor.Contexts.Count);
        Assert.Equal(CareDraftSource.ModelRejected, run.Draft!.Source);
        Assert.DoesNotContain("ibuprofen", run.Draft.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(run.Validation.Passed);
    }

    /// <summary>
    /// The reviewer is told the model's draft was unsafe, not that the model was busy - the first
    /// thing they see decides whether "Try the agent again" is worth pressing.
    /// </summary>
    [Fact]
    public async Task A_revision_the_model_could_not_answer_is_reported_as_a_rejected_draft()
    {
        var advisor = new ScriptedAdvisor(
            Model("You can take ibuprofen for that headache."),
            new CareDraftCandidate(CareUrgency.Medium, "Backup.", CareDraftSource.ModelUnavailable, "busy"));

        var run = await Run(advisor);

        Assert.Equal(CareDraftSource.ModelRejected, run.Draft!.Source);
        Assert.Contains("CR", run.Draft.SourceNote);
    }

    [Fact]
    public async Task A_backup_note_from_an_unreachable_model_is_not_sent_back()
    {
        var advisor = new ScriptedAdvisor(
            new CareDraftCandidate(CareUrgency.Medium, "Backup.", CareDraftSource.ModelUnavailable, "busy"));

        var run = await Run(advisor);

        Assert.Single(advisor.Contexts);
        Assert.Equal(CareDraftSource.ModelUnavailable, run.Draft!.Source);
    }

    private static CareDraftCandidate Model(string message) => new(CareUrgency.Medium, message);

    private static Task<CareAgentRun> Run(ICareAdvisor advisor)
        => new CareAgent(new FixedTools(), advisor, NullLogger<CareAgent>.Instance)
            .RunAsync(new CareAgentRequest(Guid.NewGuid(), Guid.NewGuid(), Report));

    private sealed class ScriptedAdvisor : ICareAdvisor
    {
        private readonly Queue<CareDraftCandidate> _answers;

        public ScriptedAdvisor(params CareDraftCandidate[] answers) => _answers = new(answers);

        public List<CareAdviceContext> Contexts { get; } = [];

        public Task<CareDraftCandidate> AdviseAsync(CareAdviceContext context, CancellationToken ct = default)
        {
            Contexts.Add(context);

            return Task.FromResult(_answers.Dequeue());
        }
    }

    private sealed class FixedTools : ICareAgentTools
    {
        public Task<CareMedicalProfileFacts?> GetMedicalProfileAsync(Guid patientId, CancellationToken ct = default)
            => Task.FromResult<CareMedicalProfileFacts?>(
                new CareMedicalProfileFacts("Migraine", "Penicillin", "Headaches since admission"));

        public Task<CarePatientHistoryFacts> GetPatientHistoryAsync(Guid patientId, CancellationToken ct = default)
            => Task.FromResult(new CarePatientHistoryFacts(40, Gender.Female, [], []));

        public Task<CareCurrentAdmissionFacts?> GetCurrentAdmissionAsync(Guid admissionId, CancellationToken ct = default)
            => Task.FromResult<CareCurrentAdmissionFacts?>(null);
    }
}
