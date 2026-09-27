using CareLanka.Api.DTOs.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

public sealed class PatientAppAccountListRequestValidator : AbstractValidator<PatientAppAccountListRequest>
{
    public PatientAppAccountListRequestValidator()
    {
        RuleFor(request => request.Search).MaximumLength(100);
        RuleFor(request => request.Page).GreaterThanOrEqualTo(1);
        RuleFor(request => request.PageSize).InclusiveBetween(1, 100);
    }
}
