using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class ReportAmbulanceLocationRequestValidator : AbstractValidator<ReportAmbulanceLocationRequest>
{
    public ReportAmbulanceLocationRequestValidator()
    {
        RuleFor(x => x.Latitude).NotNull().InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).NotNull().InclusiveBetween(-180, 180);
    }
}
