namespace CareLanka.Api.Services.Emergency;

public interface IReverseGeocoder
{
    Task<string?> FindAddressAsync(decimal latitude, decimal longitude, CancellationToken cancellationToken = default);
}
