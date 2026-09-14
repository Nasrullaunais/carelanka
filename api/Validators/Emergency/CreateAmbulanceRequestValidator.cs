using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class CreateAmbulanceRequestValidator : AbstractValidator<CreateAmbulanceRequest>
{
    public CreateAmbulanceRequestValidator()
    {
        RuleFor(request => request.RegistrationNumber).NotEmpty().MaximumLength(20);
        RuleFor(request => request.CurrentLatitude)
            .InclusiveBetween(-90, 90)
            .When(request => request.CurrentLatitude.HasValue);
        RuleFor(request => request.CurrentLongitude)
            .InclusiveBetween(-180, 180)
            .When(request => request.CurrentLongitude.HasValue);
        RuleFor(request => request)
            .Must(request => request.CurrentLatitude.HasValue == request.CurrentLongitude.HasValue)
            .WithMessage("Current latitude and longitude must be supplied together.");
    }
}
