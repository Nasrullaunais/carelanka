using CareLanka.Api.DTOs.Equipment;
using FluentValidation;

namespace CareLanka.Api.Validators.Equipment;

public sealed class CreatePrescriptionRequestValidator : AbstractValidator<CreatePrescriptionRequest>
{
    public CreatePrescriptionRequestValidator()
    {
        RuleFor(request => request.PatientId).NotEmpty();

        RuleFor(request => request.Body)
            .MaximumLength(4000)
            .When(request => !string.IsNullOrEmpty(request.Body));

        RuleFor(request => request)
            .Must(request => !string.IsNullOrWhiteSpace(request.Body) ^ request.File is not null)
            .WithMessage("Write the prescription or attach a file - not both, not neither.")
            .WithName("Body");
    }
}
