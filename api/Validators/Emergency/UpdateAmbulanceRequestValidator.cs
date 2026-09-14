using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class UpdateAmbulanceRequestValidator : AbstractValidator<UpdateAmbulanceRequest>
{
    public UpdateAmbulanceRequestValidator()
    {
        RuleFor(request => request.Status)
            .Must(status => status is AmbulanceStatus.Available or AmbulanceStatus.OutOfService)
            .When(request => request.Status.HasValue);
        RuleFor(request => request.RegistrationNumber)
            .NotEmpty()
            .MaximumLength(20)
            .When(request => request.RegistrationNumber is not null);
        RuleFor(request => request.OutOfServiceReason)
            .MaximumLength(500)
            .When(request => request.OutOfServiceReason is not null);
    }
}
