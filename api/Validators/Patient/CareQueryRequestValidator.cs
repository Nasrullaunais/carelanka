using CareLanka.Api.DTOs.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

public sealed class CareQueryRequestValidator : AbstractValidator<CareQueryRequest>
{
    public const int MinLength = 5;
    public const int MaxLength = 2000;

    public CareQueryRequestValidator()
    {
        RuleFor(request => request.ReportedText)
            .NotEmpty()
            .MinimumLength(MinLength)
            .MaximumLength(MaxLength);
    }
}
