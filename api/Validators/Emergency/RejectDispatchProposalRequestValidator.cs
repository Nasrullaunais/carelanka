using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class RejectDispatchProposalRequestValidator : AbstractValidator<RejectDispatchProposalRequest>
{
    public RejectDispatchProposalRequestValidator()
    {
        RuleFor(request => request.Reason).IsInEnum();
        RuleFor(request => request.Notes).MaximumLength(500);
    }
}
