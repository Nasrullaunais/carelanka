using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class AddressSearchRequestValidator : AbstractValidator<AddressSearchRequest>
{
    public AddressSearchRequestValidator()
        => RuleFor(request => request.Query).NotEmpty().MinimumLength(3).MaximumLength(200);
}
