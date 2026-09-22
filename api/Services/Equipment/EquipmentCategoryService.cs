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
    private readonly IEquipmentConfirmationCode _confirmationCode;

    public EquipmentCategoryService(CareLankaDbContext db, IEquipmentConfirmationCode confirmationCode)
    {
        _db = db;
        _confirmationCode = confirmationCode;
    }

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

    public async Task<IReadOnlyList<EquipmentCategoryUsage>> ListForRemovalAsync(
        string? confirmationCode, CancellationToken cancellationToken = default)
    {
        _confirmationCode.Ensure(confirmationCode);

        // Items read through the category apply their own soft-delete filter, so a removed item
        // no longer holds its category in place.
        return await _db.EquipmentCategories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new EquipmentCategoryUsage
            {
                Id = c.Id,
                Name = c.Name,
                ItemCount = c.Items.Count()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task RemoveAsync(
        Guid id, string? confirmationCode, CancellationToken cancellationToken = default)
    {
        _confirmationCode.Ensure(confirmationCode);

        var category = await GetByIdAsync(id, cancellationToken);

        // Every item keeps a category, so one still in use cannot go. Retired items and items
        // awaiting confirmation count too: they are still on the register.
        var inUse = await _db.EquipmentItems.CountAsync(i => i.CategoryId == id, cancellationToken);

        if (inUse > 0)
        {
            throw new ConflictException(MessageCode.EquipmentCategoryInUse, category.Name, inUse);
        }

        // A soft delete: the name is free to be used again, because the unique index on it is
        // scoped to live rows, and items removed earlier still point at a row that exists.
        category.IsActive = false;
        category.DeletedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<CategoryEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.EquipmentCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<CategoryEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await FindByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException("Equipment category", id);
}
