using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class CancellationRequestListRequestValidator : AbstractValidator<CancellationRequestListRequest>
{
    public CancellationRequestListRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
