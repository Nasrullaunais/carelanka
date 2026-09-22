using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class AssignAmbulanceCrewRequestValidator : AbstractValidator<AssignAmbulanceCrewRequest>
{
    public AssignAmbulanceCrewRequestValidator()
        => RuleFor(request => request.StaffMemberId).NotEmpty();
}
