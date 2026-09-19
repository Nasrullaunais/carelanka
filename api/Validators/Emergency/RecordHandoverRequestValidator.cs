using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class RecordHandoverRequestValidator : AbstractValidator<RecordHandoverRequest>
{
    public RecordHandoverRequestValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.PatientCondition).MaximumLength(500);
    }
}
