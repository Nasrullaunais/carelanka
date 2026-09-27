using CareLanka.Api.DTOs.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

public sealed class CreateWalkInAppointmentRequestValidator
    : AbstractValidator<CreateWalkInAppointmentRequest>
{
    public CreateWalkInAppointmentRequestValidator()
    {
        RuleFor(request => request.PatientId).NotEmpty().WithMessage("Choose a patient.");

        RuleFor(request => request.Reason).MaximumLength(300);
    }
}
