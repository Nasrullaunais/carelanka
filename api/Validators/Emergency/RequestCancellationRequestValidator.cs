using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class RequestCancellationRequestValidator : AbstractValidator<RequestCancellationRequest>
{
    public RequestCancellationRequestValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}
