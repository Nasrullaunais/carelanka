using CareLanka.Api.DTOs.Equipment;
using FluentValidation;

namespace CareLanka.Api.Validators.Equipment;

public sealed class RejectPrescriptionRequestValidator : AbstractValidator<RejectPrescriptionRequest>
{
    public RejectPrescriptionRequestValidator()
        => RuleFor(request => request.Reason).NotEmpty().MaximumLength(500);
}
