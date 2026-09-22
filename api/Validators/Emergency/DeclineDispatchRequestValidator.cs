using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class DeclineDispatchRequestValidator : AbstractValidator<DeclineDispatchRequest>
{
    public DeclineDispatchRequestValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}
