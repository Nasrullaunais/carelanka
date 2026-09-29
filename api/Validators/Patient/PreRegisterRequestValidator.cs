using CareLanka.Api.DTOs.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

public sealed class PreRegisterRequestValidator : AbstractValidator<PreRegisterRequest>
{
    public PreRegisterRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.Nic).NotEmpty().PatientNic();

        RuleFor(request => request.FullName).NotEmpty().MaximumLength(200);

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
