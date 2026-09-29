using CareLanka.Api.DTOs.Common;
using FluentValidation;

namespace CareLanka.Api.Validators.Common;

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword).NotEmpty().MaximumLength(128);

        RuleFor(request => request.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            .NotEqual(request => request.CurrentPassword)
            .WithMessage("Choose a password different from your current one.");
    }
}
