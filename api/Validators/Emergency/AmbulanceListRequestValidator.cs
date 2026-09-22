using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class AmbulanceListRequestValidator : AbstractValidator<AmbulanceListRequest>
{
    public AmbulanceListRequestValidator()
    {
        RuleFor(request => request.NearToLatitude)
            .InclusiveBetween(-90, 90)
            .When(request => request.NearToLatitude.HasValue);
        RuleFor(request => request.NearToLongitude)
            .InclusiveBetween(-180, 180)
            .When(request => request.NearToLongitude.HasValue);
        RuleFor(request => request)
            .Must(request => request.NearToLatitude.HasValue == request.NearToLongitude.HasValue)
            .WithMessage("Near-to latitude and longitude must be supplied together.");
        RuleFor(request => request)
            .Must(request => request.SortBy != AmbulanceSortField.Distance
                || request.NearToLatitude.HasValue)
            .WithMessage("Distance sorting requires a near-to location.");
        RuleFor(request => request.Page).GreaterThanOrEqualTo(1);
        RuleFor(request => request.PageSize).InclusiveBetween(1, 100);
        RuleFor(request => request.SortDir).Must(value => value is "asc" or "desc");
    }
}
