using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Emergency;

namespace CareLanka.Api.Tests;

public sealed class FakeAddressSearch : IAddressSearch
{
    public const string OfflineQuery = "offline";

    public Task<IReadOnlyList<AddressSuggestion>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        query == OfflineQuery
            ? throw new AddressSearchUnavailableException()
            : Task.FromResult<IReadOnlyList<AddressSuggestion>>(
                [new AddressSuggestion { Label = $"{query}, Colombo", Latitude = 6.9271m, Longitude = 79.8612m, ApproximateAccuracyMetres = 40 }]);
}
