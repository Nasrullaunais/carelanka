using CareLanka.Api.DTOs.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

public sealed class CreatePatientRequestValidator : AbstractValidator<CreatePatientRequest>
{
    public CreatePatientRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.FullName).NotEmpty().MaximumLength(200);

        RuleFor(request => request.Nic)
            .PatientNic()
            .When(request => !string.IsNullOrWhiteSpace(request.Nic));

        RuleFor(request => request.Gender).NotNull().IsInEnum();

        RuleFor(request => request.DateOfBirth)
            .PatientDateOfBirth()
            .MatchesNic(request => request.Nic)
            .When(request => request.DateOfBirth.HasValue);

        RuleFor(request => request.Phone)
            .PatientPhone()
            .When(request => !string.IsNullOrWhiteSpace(request.Phone));

        RuleFor(request => request.Address).MaximumLength(300);

        RuleFor(request => request.EmergencyContactName).MaximumLength(200);

        RuleFor(request => request.EmergencyContactPhone)
            .PatientPhone()
            .When(request => !string.IsNullOrWhiteSpace(request.EmergencyContactPhone));
    }
}

/// <summary>
/// A PUT sends the whole record again, so it is checked exactly as a create is. FluentValidation
/// looks a validator up by the exact request type, which is why this one exists at all.
/// </summary>
public sealed class UpdatePatientRequestValidator : AbstractValidator<UpdatePatientRequest>
{
    public UpdatePatientRequestValidator() => Include(new CreatePatientRequestValidator());
}
