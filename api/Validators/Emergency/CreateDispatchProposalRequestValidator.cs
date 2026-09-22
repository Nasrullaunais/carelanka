using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class CreateDispatchProposalRequestValidator : AbstractValidator<CreateDispatchProposalRequest>
{
    public CreateDispatchProposalRequestValidator()
    {
        RuleFor(request => request.EmergencyCallId).NotEmpty();
    }
}
