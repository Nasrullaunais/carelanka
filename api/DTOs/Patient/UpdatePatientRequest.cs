namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Body of PUT /api/patients/{id}. The spec defines this as CreatePatientRequest with nothing
/// added, so it inherits rather than repeating eight properties that would then drift apart.
/// </summary>
/// <remarks>
/// A PUT, so every field is replaced by what is sent — omitting phone clears it. The one thing
/// it cannot touch is TempReference: once an unidentified arrival has been given one, the
/// wristband and the verbal handover from that period still have to resolve to this person.
/// </remarks>
public class UpdatePatientRequest : CreatePatientRequest;
