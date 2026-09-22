using CareLanka.Api.DTOs.Equipment;
using FluentValidation;

namespace CareLanka.Api.Validators.Equipment;

public sealed class UploadPrescriptionRequestValidator : AbstractValidator<UploadPrescriptionRequest>
{
    public UploadPrescriptionRequestValidator()
    {
        RuleFor(request => request.File)
            .NotNull().WithMessage("Attach a photo or PDF of the prescription.");

        RuleFor(request => request.Note).MaximumLength(500);
    }
}
