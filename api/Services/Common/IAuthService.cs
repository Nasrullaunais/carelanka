using CareLanka.Api.DTOs.Common;

namespace CareLanka.Api.Services.Common;

public interface IAuthService
{
    Task<AuthTokens> LoginStaffAsync(StaffLoginRequest request, CancellationToken ct = default);

    Task<AuthTokens> RegisterPatientAsync(PatientRegisterRequest request, CancellationToken ct = default);

    Task<AuthTokens> LoginPatientAsync(PatientLoginRequest request, CancellationToken ct = default);

    Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken ct = default);

    Task LogoutAsync(string refreshToken, CancellationToken ct = default);

    Task<CurrentPrincipal> GetCurrentPrincipalAsync(CancellationToken ct = default);
}
