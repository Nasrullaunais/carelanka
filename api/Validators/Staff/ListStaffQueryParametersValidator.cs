using CareLanka.Api.DTOs.Staff;
using FluentValidation;

namespace CareLanka.Api.Validators.Staff;

public sealed class ListStaffQueryParametersValidator : AbstractValidator<ListStaffQueryParameters>
{
    public ListStaffQueryParametersValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.SortBy)
            .Must(value => string.IsNullOrWhiteSpace(value) || value.ToLower() is "full_name" or "role" or "department" or "created_at")
            .WithMessage("SortBy must be full_name, role, department, or created_at.");

        RuleFor(x => x.SortDir)
            .Must(value => string.IsNullOrWhiteSpace(value) || value.ToLower() is "asc" or "desc")
            .WithMessage("SortDir must be asc or desc.");
    }
}
