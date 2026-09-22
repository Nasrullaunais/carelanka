using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class CancelDispatchRequestValidator : AbstractValidator<CancelDispatchRequest>
{
    public CancelDispatchRequestValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}
