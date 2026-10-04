using CareLanka.Api.DTOs.Emergency;

namespace CareLanka.Api.Services.Emergency;

public interface IAddressSearch
{
    Task<IReadOnlyList<AddressSuggestion>> SearchAsync(string query, CancellationToken cancellationToken = default);
}
