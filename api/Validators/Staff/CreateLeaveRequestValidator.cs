using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Staff;
using FluentValidation;

namespace CareLanka.Api.Validators.Staff;

public sealed class CreateLeaveRequestValidator : AbstractValidator<CreateLeaveRequest>
{
    public CreateLeaveRequestValidator()
    {
        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("A valid leave type is required.");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");

        When(x => x.Type is LeaveType.Annual or LeaveType.Sick or LeaveType.Emergency, () =>
        {
            RuleFor(x => x.StartDate)
                .NotEmpty().WithMessage("Start date is required.");

            RuleFor(x => x.EndDate)
                .NotEmpty().WithMessage("End date is required.");

            RuleFor(x => x.StartDate)
                .Must((request, startDate) => !startDate.HasValue || !request.EndDate.HasValue || startDate.Value <= request.EndDate.Value)
                .WithMessage("Start date must not be after end date.");
        });

        When(x => x.Type == LeaveType.ShiftSwap, () =>
        {
            RuleFor(x => x.SwapShiftId)
                .NotEmpty().WithMessage("Swap shift ID is required.");

            RuleFor(x => x.SwapWithStaffMemberId)
                .NotEmpty().WithMessage("Swap with staff member ID is required.");
        });
    }
}
