using CareLanka.Api.DTOs.Common;

namespace CareLanka.Api.Services.Common;

public interface IDeviceTokenService
{
    Task<DeviceRegistration> RegisterAsync(RegisterDeviceRequest request, CancellationToken ct = default);

    Task UnregisterAsync(Guid id, CancellationToken ct = default);
}
