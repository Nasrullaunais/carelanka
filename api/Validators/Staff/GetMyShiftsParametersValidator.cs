using CareLanka.Api.DTOs.Staff;
using FluentValidation;

namespace CareLanka.Api.Validators.Staff;

public sealed class GetMyShiftsParametersValidator : AbstractValidator<GetMyShiftsParameters>
{
    public GetMyShiftsParametersValidator()
    {
        RuleFor(x => x.To)
            .Must((parameters, to) => !parameters.From.HasValue || !to.HasValue || to.Value >= parameters.From.Value)
            .WithMessage("To date must be greater than or equal to From date.")
            .When(x => x.To.HasValue && x.From.HasValue);
    }
}
