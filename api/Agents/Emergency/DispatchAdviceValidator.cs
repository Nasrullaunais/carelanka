namespace CareLanka.Api.Agents.Emergency;

public sealed record DispatchAdviceVerdict(bool Passed, string? FailedRule, string Detail);

public static class DispatchAdviceValidator
{
    public const string OnShortlist = "on_shortlist";
    public const string WithinTimeMargin = "within_time_margin";
    public const string ReasonNamesChoice = "reason_names_choice";

    // Enough to prefer a fuller crew or a fresher location, never enough to cost real time.
    public const int MaxExtraMinutes = 3;

    public const int MaxRationaleLength = 600;

    public static DispatchAdviceVerdict ValidateChoice(DispatchAdvice advice, DispatchChoiceContext context)
    {
        var chosen = context.Shortlist.FirstOrDefault(ambulance => ambulance.Id == advice.AmbulanceId);

        if (chosen is null)
        {
            return Fail(OnShortlist, "The AI picked an ambulance that was not on the eligible list.");
        }

        var fastest = context.Shortlist[0];

        if (fastest.RouteMinutes is { } fastestMinutes)
        {
            if (chosen.RouteMinutes is not { } chosenMinutes)
            {
                return Fail(WithinTimeMargin,
                    $"The AI picked {chosen.RegistrationNumber}, which has no road estimate, over ambulances that do.");
            }

            if (chosenMinutes - fastestMinutes > MaxExtraMinutes)
            {
                return Fail(WithinTimeMargin,
                    $"The AI picked {chosen.RegistrationNumber}, {chosenMinutes - fastestMinutes} minute(s) slower "
                    + $"than {fastest.RegistrationNumber}; the limit is {MaxExtraMinutes}.");
            }
        }
        else if (chosen.Id != fastest.Id)
        {
            return Fail(WithinTimeMargin,
                "No road estimates were available, so the AI could not justify skipping the first eligible ambulance.");
        }

        return ValidateReason(advice, chosen.RegistrationNumber);
    }

    public static DispatchAdviceVerdict ValidateDiversion(DispatchAdvice advice, DiversionContext context)
        => advice.AmbulanceId != context.Source.AmbulanceId
            ? Fail(OnShortlist, "The AI named a different ambulance from the one the diversion rules chose.")
            : ValidateReason(advice, context.Source.AmbulanceRegistration);

    private static DispatchAdviceVerdict ValidateReason(DispatchAdvice advice, string registration)
    {
        if (string.IsNullOrWhiteSpace(advice.Rationale) || advice.Rationale.Length > MaxRationaleLength)
        {
            return Fail(ReasonNamesChoice, "The AI's reason was empty or too long.");
        }

        if (!advice.Rationale.Contains(registration, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(ReasonNamesChoice, $"The AI's reason did not name the ambulance it picked, {registration}.");
        }

        return new DispatchAdviceVerdict(true, null, "The recommendation passed every dispatch rule.");
    }

    private static DispatchAdviceVerdict Fail(string rule, string detail) => new(false, rule, detail);
}
