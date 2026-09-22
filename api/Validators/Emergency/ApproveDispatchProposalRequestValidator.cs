using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class ApproveDispatchProposalRequestValidator : AbstractValidator<ApproveDispatchProposalRequest>
{
    public ApproveDispatchProposalRequestValidator()
    {
        RuleFor(request => request.Notes).MaximumLength(500);
    }
}
