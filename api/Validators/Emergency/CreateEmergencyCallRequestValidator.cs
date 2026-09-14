using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class CreateEmergencyCallRequestValidator : AbstractValidator<CreateEmergencyCallRequest>
{
    public CreateEmergencyCallRequestValidator()
    {
        RuleFor(request => request.PatientIsCaller).NotNull();
        RuleFor(request => request.PatientId)
            .Must(id => id is null || id != Guid.Empty);
        RuleFor(request => request.PatientId)
            .Null()
            .When(request => request.PatientIsCaller == false)
            .WithMessage("Patient id cannot be supplied when the caller is reporting for somebody else.");
        RuleFor(request => request.CallerName).MaximumLength(200);
        RuleFor(request => request.CallerPhone).MaximumLength(20);
        RuleFor(request => request.Latitude).NotNull().InclusiveBetween(-90, 90);
        RuleFor(request => request.Longitude).NotNull().InclusiveBetween(-180, 180);
        RuleFor(request => request.LocationAccuracyMetres).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(request => request.LocationCapturedAt).NotNull();
        RuleFor(request => request.IdempotencyKey).NotNull().NotEqual(Guid.Empty);
        RuleFor(request => request.Details).MaximumLength(1000);
    }
}
