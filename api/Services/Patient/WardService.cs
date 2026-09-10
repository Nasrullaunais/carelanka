using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Configurations.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using CreateWardRequest = CareLanka.Api.DTOs.Patient.CreateWardRequest;
using WardEntity = CareLanka.Api.Data.Entities.Patient.Ward;
using WardResponse = CareLanka.Api.DTOs.Patient.Ward;

namespace CareLanka.Api.Services.Patient;

public sealed class WardService : IWardService
{
    private readonly CareLankaDbContext _db;
    private readonly IBedRegistryService _beds;

    public WardService(CareLankaDbContext db, IBedRegistryService beds)
    {
        _db = db;
        _beds = beds;
    }

    public async Task<IReadOnlyList<WardResponse>> ListAsync(
        WardType? wardType, bool isActive, CancellationToken ct = default)
    {
        // IgnoreQueryFilters, then filter by hand: the global filter only ever shows active
        // rows, so without this isActive=false silently returns nothing at all.
        var query = _db.Wards.IgnoreQueryFilters().Where(w => w.IsActive == isActive);

        if (wardType is not null)
        {
            query = query.Where(w => w.WardType == wardType);
        }

        var wards = await query.OrderBy(w => w.Name).ToListAsync(ct);

        var bedCounts = await _beds.CountBedsByWardAsync(
            wards.Select(w => w.Id).ToList(), ct);

        return wards.Select(w => ToResponse(w, bedCounts)).ToList();
    }

    public async Task<WardResponse> CreateAsync(
        CreateWardRequest request, CancellationToken ct = default)
    {
        var ward = new WardEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            WardType = request.WardType,
            GenderPolicy = request.GenderPolicy,
            IsActive = request.IsActive
        };

        _db.Wards.Add(ward);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (IsDuplicateName(exception))
        {
            // The index is the guarantee, not a prior read: two administrators creating
            // "ICU-1" at the same moment both pass any check we could do beforehand.
            throw new ConflictException(MessageCode.WardNameTaken, ward.Name);
        }

        var bedCounts = await _beds.CountBedsByWardAsync(new[] { ward.Id }, ct);

        return ToResponse(ward, bedCounts);
    }

    public Task<WardEntity?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Wards.FirstOrDefaultAsync(w => w.Id == id, ct);

    public async Task<WardEntity> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await FindByIdAsync(id, ct) ?? throw new NotFoundException("Ward", id);

    private static WardResponse ToResponse(WardEntity ward, IReadOnlyDictionary<Guid, int> bedCounts)
        => new()
        {
            Id = ward.Id,
            Name = ward.Name,
            WardType = ward.WardType,
            GenderPolicy = ward.GenderPolicy,
            IsActive = ward.IsActive,
            TotalBeds = bedCounts.TryGetValue(ward.Id, out var count) ? count : 0,
            CreatedAt = ward.CreatedAt,
            UpdatedAt = ward.UpdatedAt
        };

    private static bool IsDuplicateName(DbUpdateException exception)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: WardConfiguration.NameUniqueIndex
        };
}
