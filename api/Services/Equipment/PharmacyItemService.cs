using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using ItemEntity = CareLanka.Api.Data.Entities.Equipment.PharmacyItem;
using TransactionEntity = CareLanka.Api.Data.Entities.Equipment.PharmacyTransaction;

namespace CareLanka.Api.Services.Equipment;

public sealed class PharmacyItemService : IPharmacyItemService
{
    private readonly CareLankaDbContext _db;
    private readonly ICurrentUser _currentUser;

    public PharmacyItemService(CareLankaDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<PharmacyItem>> ListAsync(
        PharmacyItemQuery query, CancellationToken cancellationToken = default)
    {
        var items = _db.PharmacyItems.AsNoTracking().Include(i => i.Category).AsQueryable();

        if (query.CategoryId is { } category)
        {
            items = items.Where(i => i.CategoryId == category);
        }

        // The availability half of the plan's search requirement. Computed from the
        // quantity rather than read from a stored flag, so it can never be stale.
        if (query.AvailableOnly)
        {
            items = items.Where(i => i.QuantityOnHand > 0);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // ILIKE via EF.Functions, so a nurse typing "para" finds Paracetamol without
            // knowing how the label was capitalised.
            var term = $"%{query.Search.Trim()}%";

            items = items.Where(i =>
                EF.Functions.ILike(i.Name, term)
                || (i.Manufacturer != null && EF.Functions.ILike(i.Manufacturer, term)));
        }

        var totalItems = await items.CountAsync(cancellationToken);

        var rows = await Sort(items, query.SortBy, query.SortDir)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<PharmacyItem>.From(
            rows.Select(ToDto).ToList(), query.Page, query.PageSize, totalItems);
    }

    public async Task<PharmacyItem> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => ToDto(await ReadAsync(id, cancellationToken));

    public async Task<PharmacyItem> CreateAsync(
        CreatePharmacyItemRequest request, CancellationToken cancellationToken = default)
    {
        // Throws 404 rather than a foreign-key violation, so a mistyped category id reads as
        // "no such category" instead of a 500.
        var category = await _db.PharmacyCategories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken)
            ?? throw new NotFoundException("Pharmacy category", request.CategoryId);

        var name = request.Name.Trim();
        var unit = request.Unit.Trim();

        if (name.Length == 0 || unit.Length == 0)
        {
            throw new BadRequestException(MessageCode.ValidationFailed);
        }

        var taken = await _db.PharmacyItems
            .AnyAsync(i => i.Name.ToLower() == name.ToLower(), cancellationToken);

        if (taken)
        {
            throw new ConflictException(MessageCode.PharmacyItemNameTaken, name);
        }

        var item = new ItemEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            CategoryId = category.Id,
            Category = category,
            Manufacturer = Normalise(request.Manufacturer),
            BatchNumber = Normalise(request.BatchNumber),
            ExpiryDate = request.ExpiryDate,
            Unit = unit,
            // Opening stock. Every later change is a transaction, so this is the only place
            // the column is written outside RecordTransactionAsync.
            QuantityOnHand = request.QuantityOnHand,
            ReorderThreshold = request.ReorderThreshold,
            UnitPrice = request.UnitPrice
        };

        _db.PharmacyItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(item);
    }

    public async Task<PharmacyItem> RecordTransactionAsync(
        Guid id, CreatePharmacyTransactionRequest request, CancellationToken cancellationToken = default)
    {
        // A stocktake correction nobody explained cannot be audited later, and this is the
        // one movement with no paperwork behind it. Plan section 5.1.
        if (request.Type == PharmacyTransactionType.Adjusted
            && string.IsNullOrWhiteSpace(request.Note))
        {
            throw new BadRequestException(MessageCode.AdjustmentNeedsNote);
        }

        // Read once, for the 404 and for the wording of the 409 below. This is never the
        // stock check: the check is the WHERE clause on the update.
        var item = await ReadAsync(id, cancellationToken);

        var takesStock = request.Type is PharmacyTransactionType.Dispensed
            or PharmacyTransactionType.ExpiredRemoved;

        // The quantity change and the row recording it must both land or neither. Without
        // this, a failure between them leaves stock that moved with nothing saying why.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var affected = takesStock
            ? await TakeAsync(id, request.Quantity, cancellationToken)
            : await AddAsync(id, request.Quantity, cancellationToken);

        if (affected == 0)
        {
            // The item exists - the read above proved it - so zero rows means the guard
            // bit. This is also what the loser of a race between two dispensers reads.
            throw new ConflictException(
                MessageCode.InsufficientStock,
                item.Name, item.QuantityOnHand, item.Unit, request.Quantity);
        }

        _db.PharmacyTransactions.Add(new TransactionEntity
        {
            Id = Guid.NewGuid(),
            PharmacyItemId = id,
            Type = request.Type,
            Quantity = request.Quantity,
            // Taken from the token, never from the body. A caller cannot record a movement
            // against somebody else's name.
            PerformedByStaffId = _currentUser.Id,
            Note = Normalise(request.Note)
        });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Re-read rather than adjust the copy in hand. The conditional update went straight
        // to the database, so the entity read earlier is already out of date.
        return ToDto(await ReadAsync(id, cancellationToken));
    }

    public async Task<PagedResult<PharmacyTransaction>> ListTransactionsAsync(
        Guid id, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        _ = await ReadAsync(id, cancellationToken);

        var history = _db.PharmacyTransactions.AsNoTracking().Where(t => t.PharmacyItemId == id);

        var totalItems = await history.CountAsync(cancellationToken);

        var rows = await history
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new PharmacyTransaction
            {
                Id = t.Id,
                PharmacyItemId = t.PharmacyItemId,
                Type = t.Type,
                Quantity = t.Quantity,
                PerformedByStaffId = t.PerformedByStaffId,
                Note = t.Note,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return PagedResult<PharmacyTransaction>.From(rows, page, pageSize, totalItems);
    }

    public Task<ItemEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.PharmacyItems.Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<ItemEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await FindByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Pharmacy item", id);

    /// <summary>
    /// The whole safety rule, in one statement. Postgres evaluates the WHERE and the write
    /// under one row lock, so of two people dispensing the last box at the same moment one
    /// updates a row and the other updates none. A SELECT followed by an UPDATE would let
    /// both read five, both write four, and six boxes leave a shelf holding five.
    /// </summary>
    private Task<int> TakeAsync(Guid id, int quantity, CancellationToken cancellationToken)
        => _db.PharmacyItems
            .Where(i => i.Id == id && i.QuantityOnHand >= quantity)
            .ExecuteUpdateAsync(set => set
                .SetProperty(i => i.QuantityOnHand, i => i.QuantityOnHand - quantity)
                // ExecuteUpdate goes straight to SQL and never passes the change tracker, so
                // TimestampInterceptor does not see it. Set by hand or the row would claim
                // it had not changed since the day it was created.
                .SetProperty(i => i.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken);

    private Task<int> AddAsync(Guid id, int quantity, CancellationToken cancellationToken)
        => _db.PharmacyItems
            .Where(i => i.Id == id)
            .ExecuteUpdateAsync(set => set
                .SetProperty(i => i.QuantityOnHand, i => i.QuantityOnHand + quantity)
                .SetProperty(i => i.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken);

    private async Task<ItemEntity> ReadAsync(Guid id, CancellationToken cancellationToken)
        => await _db.PharmacyItems
               .AsNoTracking()
               .Include(i => i.Category)
               .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
           ?? throw new NotFoundException("Pharmacy item", id);

    private static IQueryable<ItemEntity> Sort(IQueryable<ItemEntity> items, string sortBy, string sortDir)
    {
        var descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy?.ToLowerInvariant() switch
        {
            "expiry_date" => descending
                ? items.OrderByDescending(i => i.ExpiryDate)
                : items.OrderBy(i => i.ExpiryDate),
            "quantity_on_hand" => descending
                ? items.OrderByDescending(i => i.QuantityOnHand)
                : items.OrderBy(i => i.QuantityOnHand),
            "created_at" => descending
                ? items.OrderByDescending(i => i.CreatedAt)
                : items.OrderBy(i => i.CreatedAt),
            // Anything unrecognised sorts by name. A mistyped sort key is not worth a 400.
            _ => descending ? items.OrderByDescending(i => i.Name) : items.OrderBy(i => i.Name)
        };
    }

    private static PharmacyItem ToDto(ItemEntity item)
        => new()
        {
            Id = item.Id,
            Name = item.Name,
            CategoryId = item.CategoryId,
            CategoryName = item.Category.Name,
            Manufacturer = item.Manufacturer,
            BatchNumber = item.BatchNumber,
            ExpiryDate = item.ExpiryDate,
            Unit = item.Unit,
            QuantityOnHand = item.QuantityOnHand,
            ReorderThreshold = item.ReorderThreshold,
            UnitPrice = item.UnitPrice,
            // Both computed here, never stored. One source of truth is the quantity.
            IsAvailable = item.QuantityOnHand > 0,
            BelowThreshold = item.QuantityOnHand <= item.ReorderThreshold,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };

    private static string? Normalise(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
