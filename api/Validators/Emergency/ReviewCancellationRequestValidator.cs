using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class ReviewCancellationRequestValidator : AbstractValidator<ReviewCancellationRequest>
{
    public ReviewCancellationRequestValidator() => RuleFor(x => x.Notes).MaximumLength(500);
}
