using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class EmergencyCallListRequestValidator : AbstractValidator<EmergencyCallListRequest>
{
    public EmergencyCallListRequestValidator()
    {
        RuleFor(request => request.Page).GreaterThanOrEqualTo(1);
        RuleFor(request => request.PageSize).InclusiveBetween(1, 100);
        RuleFor(request => request.SortDir).Must(value => value is "asc" or "desc");
        RuleFor(request => request)
            .Must(request => request.From is null || request.To is null || request.From <= request.To)
            .WithMessage("From must not be after to.");
    }
}
