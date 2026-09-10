using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.DTOs.Equipment;
using Microsoft.EntityFrameworkCore;
using CategoryEntity = CareLanka.Api.Data.Entities.Equipment.PharmacyCategory;

namespace CareLanka.Api.Services.Equipment;

public sealed class PharmacyCategoryService : IPharmacyCategoryService
{
    private readonly CareLankaDbContext _db;

    public PharmacyCategoryService(CareLankaDbContext db) => _db = db;

    public async Task<IReadOnlyList<PharmacyCategory>> ListAsync(
        CancellationToken cancellationToken = default)
        => await _db.PharmacyCategories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => ToDto(c))
            .ToListAsync(cancellationToken);

    public async Task<PharmacyCategory> CreateAsync(
        CreatePharmacyCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        if (name.Length == 0)
        {
            throw new BadRequestException(MessageCode.ValidationFailed);
        }

        // Case-insensitive, like equipment categories: "Chronic Medicines" and "chronic
        // medicines" are one category to a person, and two rows that look identical on a
        // dispensing screen are worse than a 409.
        var taken = await _db.PharmacyCategories
            .AnyAsync(c => c.Name.ToLower() == name.ToLower(), cancellationToken);

        if (taken)
        {
            throw new ConflictException(MessageCode.PharmacyCategoryNameTaken, name);
        }

        var category = new CategoryEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            RequiresPrescription = request.RequiresPrescription
        };

        _db.PharmacyCategories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(category);
    }

    public Task<CategoryEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.PharmacyCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<CategoryEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await FindByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException("Pharmacy category", id);

    private static PharmacyCategory ToDto(CategoryEntity category)
        => new()
        {
            Id = category.Id,
            Name = category.Name,
            RequiresPrescription = category.RequiresPrescription,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };
}
