using CareLanka.Api.Data.Configurations.Patient;
using CareLanka.Api.DTOs.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

public sealed class ApproveCareRecommendationRequestValidator
    : AbstractValidator<ApproveCareRecommendationRequest>
{
    public ApproveCareRecommendationRequestValidator()
    {
        RuleFor(request => request.DoctorMessage)
            .MaximumLength(CareRecommendationConfiguration.MessageLength)
            .When(request => request.DoctorMessage is not null);
    }
}
