using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Staff;
using FluentValidation;

namespace CareLanka.Api.Validators.Staff;

public sealed class CreateStaffMemberRequestValidator : AbstractValidator<CreateStaffMemberRequest>
{
    public CreateStaffMemberRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
            .Must(email => email.EndsWith("@carelanka.lk", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Work email must be a carelanka.lk address.");

        RuleFor(x => x.TemporaryPassword)
            .NotEmpty().WithMessage("Temporary password is required.")
            .MinimumLength(12).WithMessage("Temporary password must be at least 12 characters.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20).WithMessage("Phone number must not exceed 20 characters.")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("A valid staff role is required.");

        RuleFor(x => x.Department)
            .MaximumLength(100).WithMessage("Department must not exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.Department));

        RuleFor(x => x.Specialization)
            .MaximumLength(150).WithMessage("Specialization must not exceed 150 characters.")
            .When(x => !string.IsNullOrEmpty(x.Specialization));

        RuleFor(x => x.RegistrationNumber)
            .MaximumLength(50).WithMessage("Registration number must not exceed 50 characters.")
            .When(x => !string.IsNullOrEmpty(x.RegistrationNumber));

        // A doctor's professional details are required; nobody else is asked for them today.
        RuleFor(x => x.Title)
            .NotNull().WithMessage("Title is required for a doctor.")
            .When(x => x.Role == StaffRole.Doctor);

        RuleFor(x => x.Specialization)
            .NotEmpty().WithMessage("Specialization is required for a doctor.")
            .When(x => x.Role == StaffRole.Doctor);

        RuleFor(x => x.RegistrationNumber)
            .NotEmpty().WithMessage("Registration number is required for a doctor.")
            .When(x => x.Role == StaffRole.Doctor);

        RuleFor(x => x.JoiningDate)
            .NotNull().WithMessage("Joining date is required for a doctor.")
            .When(x => x.Role == StaffRole.Doctor);
    }
}
