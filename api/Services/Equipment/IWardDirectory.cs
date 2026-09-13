namespace CareLanka.Api.Services.Equipment;

public interface IWardDirectory
{
    Task<IReadOnlyDictionary<Guid, string>> GetWardNamesAsync(
        IReadOnlyCollection<Guid> wardIds, CancellationToken cancellationToken = default);
}
