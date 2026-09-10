namespace CareLanka.Api.Services.Equipment;

// Our read-only port over Patient Management's ward list (GET /wards, patient-spec.yaml).
// Beds carry a ward_id we store and a ward_name we do not: the name is theirs.
public interface IWardDirectory
{
    /// <summary>Display names for these wards. A ward we cannot name is absent from the result.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetWardNamesAsync(
        IReadOnlyCollection<Guid> wardIds, CancellationToken cancellationToken = default);
}
