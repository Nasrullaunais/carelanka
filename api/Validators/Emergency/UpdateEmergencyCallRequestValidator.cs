using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class UpdateEmergencyCallRequestValidator : AbstractValidator<UpdateEmergencyCallRequest>
{
    public UpdateEmergencyCallRequestValidator()
    {
        RuleFor(request => request.Details).MaximumLength(1000);
        RuleFor(request => request.CallerName).MaximumLength(200);
        RuleFor(request => request.CallerPhone).MaximumLength(20);
        RuleFor(request => request.Latitude)
            .InclusiveBetween(-90, 90)
            .When(request => request.Latitude.HasValue);
        RuleFor(request => request.Longitude)
            .InclusiveBetween(-180, 180)
            .When(request => request.Longitude.HasValue);
        RuleFor(request => request)
            .Must(request => request.Latitude.HasValue == request.Longitude.HasValue)
            .WithMessage("Latitude and longitude must be supplied together.");
    }
}
