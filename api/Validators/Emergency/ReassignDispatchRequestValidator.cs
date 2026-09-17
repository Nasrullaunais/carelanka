using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class ReassignDispatchRequestValidator : AbstractValidator<ReassignDispatchRequest>
{
    public ReassignDispatchRequestValidator()
    {
        RuleFor(x => x.ReplacementAmbulanceId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
