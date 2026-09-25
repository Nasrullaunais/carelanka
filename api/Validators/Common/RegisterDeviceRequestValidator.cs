using CareLanka.Api.DTOs.Common;
using FluentValidation;

namespace CareLanka.Api.Validators.Common;

public sealed class RegisterDeviceRequestValidator : AbstractValidator<RegisterDeviceRequest>
{
    public RegisterDeviceRequestValidator()
    {
        RuleFor(request => request.Token).NotEmpty().MaximumLength(512);
        RuleFor(request => request.Platform).IsInEnum();
    }
}
