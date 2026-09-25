using CareLanka.Api.DTOs.Staff;
using FluentValidation;

namespace CareLanka.Api.Validators.Staff;

public sealed class EndAllocationRequestValidator : AbstractValidator<EndAllocationRequest>
{
    public EndAllocationRequestValidator()
    {
        RuleFor(x => x.Reason)
            .IsInEnum().WithMessage("A valid allocation end reason is required.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes must not exceed 500 characters.");
    }
}
