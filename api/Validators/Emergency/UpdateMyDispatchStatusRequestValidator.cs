using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class UpdateMyDispatchStatusRequestValidator : AbstractValidator<UpdateMyDispatchStatusRequest>
{
    public UpdateMyDispatchStatusRequestValidator()
    {
        RuleFor(x => x.Status).Must(x => x is DispatchStatus.EnRouteToScene or DispatchStatus.AtScene or DispatchStatus.TransportingToHospital);
        RuleFor(x => x).Must(x => x.Latitude.HasValue == x.Longitude.HasValue)
            .WithMessage("Latitude and longitude must be supplied together.");
    }
}
