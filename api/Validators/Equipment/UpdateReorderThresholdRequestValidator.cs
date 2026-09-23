using CareLanka.Api.DTOs.Equipment;
using FluentValidation;

namespace CareLanka.Api.Validators.Equipment;

public sealed class UpdateReorderThresholdRequestValidator : AbstractValidator<UpdateReorderThresholdRequest>
{
    public UpdateReorderThresholdRequestValidator()
    {
        RuleFor(request => request.ReorderThreshold)
            .GreaterThanOrEqualTo(0).WithMessage("Enter a reorder threshold of 0 or more.");
    }
}
