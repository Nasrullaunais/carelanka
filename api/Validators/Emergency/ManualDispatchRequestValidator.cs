using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class ManualDispatchRequestValidator : AbstractValidator<ManualDispatchRequest>
{
    public ManualDispatchRequestValidator() => RuleFor(x => x.AmbulanceId).NotEmpty();
}
