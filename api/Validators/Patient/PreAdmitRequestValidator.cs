using CareLanka.Api.DTOs.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

public sealed class PreAdmitRequestValidator : AbstractValidator<PreAdmitRequest>
{
    public PreAdmitRequestValidator()
    {
        RuleFor(request => request.DispatchId).NotEmpty().MaximumLength(64);
        RuleFor(request => request.ExpectedArrival).NotEmpty();
        RuleFor(request => request.Urgency).IsInEnum();
        RuleFor(request => request.PatientId).NotEmpty().When(request => request.PatientIsCaller);
        RuleFor(request => request.ProvisionalGender)
            .IsInEnum()
            .When(request => request.ProvisionalGender.HasValue);
        RuleFor(request => request.DestinationWardTypeHint)
            .IsInEnum()
            .When(request => request.DestinationWardTypeHint.HasValue);
    }
}
