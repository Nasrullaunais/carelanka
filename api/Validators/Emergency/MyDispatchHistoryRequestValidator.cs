using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class MyDispatchHistoryRequestValidator : AbstractValidator<MyDispatchHistoryRequest>
{
    public MyDispatchHistoryRequestValidator()
    {
        RuleFor(request => request.Page).GreaterThanOrEqualTo(1);
        RuleFor(request => request.PageSize).InclusiveBetween(1, 100);
        RuleFor(request => request.To).GreaterThanOrEqualTo(request => request.From)
            .When(request => request.From is not null && request.To is not null);
    }
}
