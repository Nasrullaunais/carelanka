using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.DTOs.Equipment;
using Microsoft.EntityFrameworkCore;
using CategoryEntity = CareLanka.Api.Data.Entities.Equipment.EquipmentCategory;

namespace CareLanka.Api.Services.Equipment;

public sealed class EquipmentCategoryService : IEquipmentCategoryService
{
    private readonly CareLankaDbContext _db;

    public EquipmentCategoryService(CareLankaDbContext db) => _db = db;

    public async Task<IReadOnlyList<EquipmentCategory>> ListAsync(
        CancellationToken cancellationToken = default)
        => await _db.EquipmentCategories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new EquipmentCategory
            {
                Id = c.Id,
                Name = c.Name,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync(cancellationToken);

    public async Task<EquipmentCategory> CreateAsync(
        CreateEquipmentCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        if (name.Length == 0)
        {
            throw new BadRequestException(MessageCode.ValidationFailed);
        }

        // Case-insensitive: "Surgical Gear" and "surgical gear" are the same category to a
        // person, and two rows that look identical on screen are worse than a 409.
        var taken = await _db.EquipmentCategories
            .AnyAsync(c => c.Name.ToLower() == name.ToLower(), cancellationToken);

        if (taken)
        {
            throw new ConflictException(MessageCode.CategoryNameTaken, name);
        }

        var category = new CategoryEntity { Id = Guid.NewGuid(), Name = name };

        _db.EquipmentCategories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        return new EquipmentCategory
        {
            Id = category.Id,
            Name = category.Name,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };
    }

    public Task<CategoryEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.EquipmentCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<CategoryEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await FindByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException("Equipment category", id);
}
