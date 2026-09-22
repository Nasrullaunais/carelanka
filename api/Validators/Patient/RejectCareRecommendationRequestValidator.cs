using CareLanka.Api.Data.Configurations.Patient;
using CareLanka.Api.DTOs.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

public sealed class RejectCareRecommendationRequestValidator
    : AbstractValidator<RejectCareRecommendationRequest>
{
    public const int MinLength = 3;

    public RejectCareRecommendationRequestValidator()
    {
        RuleFor(request => request.Reason)
            .NotEmpty()
            .MinimumLength(MinLength)
            .MaximumLength(CareRecommendationConfiguration.RejectionReasonLength);
    }
}
