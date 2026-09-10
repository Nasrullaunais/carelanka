namespace CareLanka.Api.Data.Enums;

// AdmissionUrgency, not Urgency: equipment-spec.yaml already has an Urgency of its own for
// maintenance jobs. Same word, different fact, one app. The JSON field stays "urgency".
public enum AdmissionUrgency
{
    Routine,
    Urgent,
    Emergency
}
