using CareLanka.Api.DTOs.Common;

namespace CareLanka.Api.Services.Common;

public interface IAuthService
{
    Task<AuthTokens> LoginStaffAsync(StaffLoginRequest request, CancellationToken ct = default);

    Task<AuthTokens> RegisterPatientAsync(PatientRegisterRequest request, CancellationToken ct = default);

    Task<AuthTokens> LoginPatientAsync(PatientLoginRequest request, CancellationToken ct = default);

    Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken ct = default);

    Task LogoutAsync(string refreshToken, CancellationToken ct = default);

    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);

    Task<CurrentPrincipal> GetCurrentPrincipalAsync(CancellationToken ct = default);

    Task<PatientPasswordReset> ResetPatientPasswordAsync(Guid patientAccountId, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, string>> GetPatientUsernamesAsync(
        IReadOnlyCollection<Guid> accountIds, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> FindPatientAccountIdsByUsernameAsync(string search, CancellationToken ct = default);
}
