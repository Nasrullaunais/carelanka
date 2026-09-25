using CareLanka.Api.DTOs.Staff;
using FluentValidation;

namespace CareLanka.Api.Validators.Staff;

public sealed class ListLeaveRequestsQueryParametersValidator : AbstractValidator<ListLeaveRequestsQueryParameters>
{
    public ListLeaveRequestsQueryParametersValidator()
    {
        RuleFor(x => x.StaffMemberId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Staff member ID must not be empty.");

        RuleFor(x => x.Status)
            .IsInEnum()
            .When(x => x.Status.HasValue)
            .WithMessage("A valid leave status is required.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .When(x => x.Type.HasValue)
            .WithMessage("A valid leave type is required.");

        RuleFor(x => x.To)
            .Must((parameters, to) => !parameters.From.HasValue || !to.HasValue || to.Value >= parameters.From.Value)
            .WithMessage("To date must be greater than or equal to From date.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}
