using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class RetireAmbulanceRequestValidator : AbstractValidator<RetireAmbulanceRequest>
{
    public RetireAmbulanceRequestValidator()
        => RuleFor(request => request.Reason).NotEmpty().MaximumLength(500);
}
