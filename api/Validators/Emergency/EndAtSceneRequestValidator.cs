using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class EndAtSceneRequestValidator : AbstractValidator<EndAtSceneRequest>
{
    public EndAtSceneRequestValidator()
    {
        RuleFor(x => x.Outcome).NotNull().IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
