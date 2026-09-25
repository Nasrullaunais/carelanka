using CareLanka.Api.DTOs.Staff;
using FluentValidation;

namespace CareLanka.Api.Validators.Staff;

public sealed class ListShiftsQueryParametersValidator : AbstractValidator<ListShiftsQueryParameters>
{
    public ListShiftsQueryParametersValidator()
    {
        RuleFor(x => x.From)
            .NotEmpty().WithMessage("From date is required.");

        RuleFor(x => x.To)
            .NotEmpty().WithMessage("To date is required.")
            .Must((parameters, to) => to >= parameters.From)
            .WithMessage("To date must be greater than or equal to From date.");

        RuleFor(x => x.Role)
            .IsInEnum()
            .When(x => x.Role.HasValue)
            .WithMessage("A valid staff role is required.");

        RuleFor(x => x.CoverageStatus)
            .IsInEnum()
            .When(x => x.CoverageStatus.HasValue)
            .WithMessage("A valid coverage status is required.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.SortBy)
            .Must(value => string.IsNullOrWhiteSpace(value) || value.ToLower() is "date" or "ward" or "coverage_status")
            .WithMessage("SortBy must be date, ward, or coverage_status.");

        RuleFor(x => x.SortDir)
            .Must(value => string.IsNullOrWhiteSpace(value) || value.ToLower() is "asc" or "desc")
            .WithMessage("SortDir must be asc or desc.");
    }
}
