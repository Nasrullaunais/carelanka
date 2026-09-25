using CareLanka.Api.DTOs.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

public sealed class ClassifyAdmissionRequestValidator : AbstractValidator<ClassifyAdmissionRequest>
{
    public ClassifyAdmissionRequestValidator()
        => RuleFor(request => request.AdmissionCategory).IsInEnum();
}
