using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;

namespace CareLanka.Api.Services.Equipment;

// Who is in the hospital now, so a screen can offer a patient to pick instead of an id to type.
// Two screens use it: the laboratory files a result, and the equipment register assigns an item.
public interface IWardPatientService
{
    /// <param name="wardName">Null for every current visit, including those holding no bed.</param>
    Task<PagedResult<WardPatient>> ListAsync(
        string? wardName, int page, int pageSize, CancellationToken cancellationToken = default);
}
