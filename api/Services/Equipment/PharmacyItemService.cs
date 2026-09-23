using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using BatchEntity = CareLanka.Api.Data.Entities.Equipment.PharmacyBatch;
using ItemEntity = CareLanka.Api.Data.Entities.Equipment.PharmacyItem;
using TransactionEntity = CareLanka.Api.Data.Entities.Equipment.PharmacyTransaction;

namespace CareLanka.Api.Services.Equipment;

public sealed class PharmacyItemService : IPharmacyItemService
{
    private readonly CareLankaDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IEquipmentConfirmationCode _confirmationCode;

    public PharmacyItemService(
        CareLankaDbContext db,
        ICurrentUser currentUser,
        IEquipmentConfirmationCode confirmationCode)
    {
        _db = db;
        _currentUser = currentUser;
        _confirmationCode = confirmationCode;
    }

    public async Task<PagedResult<PharmacyItem>> ListAsync(
        PharmacyItemQuery query, CancellationToken cancellationToken = default)
    {
        var items = _db.PharmacyItems.AsNoTracking()
            .Include(i => i.Category)
            .Include(i => i.Batches)
            .AsQueryable();

        if (query.CategoryId is { } category)
        {
            items = items.Where(i => i.CategoryId == category);
        }

        if (query.AvailableOnly)
        {
            items = items.Where(i => i.QuantityOnHand > 0);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
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
            Unit = unit,
            QuantityOnHand = request.QuantityOnHand,
            ReorderThreshold = request.ReorderThreshold,
            UnitPrice = request.UnitPrice
        };

        _db.PharmacyItems.Add(item);

        // Whatever is on the shelf on day one is batch 1, so the register never holds stock that
        // belongs to no delivery.
        if (request.QuantityOnHand > 0 || request.ExpiryDate is not null)
        {
            item.Batches.Add(new BatchEntity
            {
                Id = Guid.NewGuid(),
                PharmacyItemId = item.Id,
                BatchNumber = 1,
                Reference = Normalise(request.BatchNumber),
                ExpiryDate = request.ExpiryDate,
                QuantityOnHand = request.QuantityOnHand
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(item);
    }

    public async Task RemoveAsync(
        Guid id, string? confirmationCode, CancellationToken cancellationToken = default)
    {
        _confirmationCode.Ensure(confirmationCode);

        var item = await _db.PharmacyItems
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("Pharmacy item", id);

        // Removing hides the medicine from every list, so the shelf has to be empty first:
        // otherwise stock would disappear with it and the consumption history would not say where.
        if (item.QuantityOnHand > 0)
        {
            throw new ConflictException(
                MessageCode.PharmacyRemoveNeedsEmpty, item.Name, item.QuantityOnHand, item.Unit);
        }

        item.IsActive = false;
        item.DeletedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PharmacyItem> UpdateReorderThresholdAsync(
        Guid id, int reorderThreshold, CancellationToken cancellationToken = default)
    {
        var item = await _db.PharmacyItems
            .Include(i => i.Category)
            .Include(i => i.Batches)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("Pharmacy item", id);

        item.ReorderThreshold = reorderThreshold;

        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(item);
    }

    public async Task<IReadOnlyList<PharmacyBatch>> ListBatchesAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        _ = await ReadAsync(id, cancellationToken);

        var batches = await _db.PharmacyBatches.AsNoTracking()
            .Where(b => b.PharmacyItemId == id)
            .OrderBy(b => b.BatchNumber)
            .ToListAsync(cancellationToken);

        return batches.Select(ToBatchDto).ToList();
    }

    public async Task<PharmacyBatch> AddBatchAsync(
        Guid id, AddPharmacyBatchRequest request, CancellationToken cancellationToken = default)
    {
        var item = await ReadAsync(id, cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await LockAsync(id, cancellationToken);

        var lastNumber = await _db.PharmacyBatches
            .Where(b => b.PharmacyItemId == item.Id)
            .Select(b => (int?)b.BatchNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var batch = new BatchEntity
        {
            Id = Guid.NewGuid(),
            PharmacyItemId = item.Id,
            BatchNumber = lastNumber + 1,
            Reference = Normalise(request.Reference),
            ExpiryDate = request.ExpiryDate,
            QuantityOnHand = request.Quantity,
            Note = Normalise(request.Note)
        };

        _db.PharmacyBatches.Add(batch);

        _db.PharmacyTransactions.Add(new TransactionEntity
        {
            Id = Guid.NewGuid(),
            PharmacyItemId = item.Id,
            PharmacyBatchId = batch.Id,
            Type = PharmacyTransactionType.Received,
            Quantity = request.Quantity,
            PerformedByStaffId = _currentUser.Id,
            Note = Normalise(request.Note)
        });

        await AddAsync(item.Id, request.Quantity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToBatchDto(batch);
    }

    public async Task<PharmacyItem> RecordTransactionAsync(
        Guid id, CreatePharmacyTransactionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureMovementIsAllowed(request);

        var item = await ReadAsync(id, cancellationToken);

        // Stock arriving has an expiry date, which this shape cannot carry: it is a batch.
        if (request.Type == PharmacyTransactionType.Received)
        {
            throw new BadRequestException(MessageCode.PharmacyReceiveIsABatch, item.Name);
        }

        var takesStock = request.Type is PharmacyTransactionType.Dispensed
            or PharmacyTransactionType.ExpiredRemoved;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await LockAsync(id, cancellationToken);

        if (takesStock)
        {
            await TakeFromBatchesAsync(item, request, cancellationToken);
        }
        else
        {
            await AdjustNewestBatchAsync(item, request, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToDto(await ReadAsync(id, cancellationToken));
    }

    public async Task<PharmacyItem> RecordBatchTransactionAsync(
        Guid id,
        Guid batchId,
        CreatePharmacyTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureMovementIsAllowed(request);

        var item = await ReadAsync(id, cancellationToken);

        if (request.Type == PharmacyTransactionType.Received)
        {
            throw new BadRequestException(MessageCode.PharmacyReceiveIsABatch, item.Name);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await LockAsync(id, cancellationToken);

        var batch = await _db.PharmacyBatches
            .FirstOrDefaultAsync(b => b.Id == batchId && b.PharmacyItemId == id, cancellationToken)
            ?? throw new NotFoundException("Pharmacy batch", batchId);

        var takesStock = request.Type is PharmacyTransactionType.Dispensed
            or PharmacyTransactionType.ExpiredRemoved;

        if (takesStock && batch.QuantityOnHand < request.Quantity)
        {
            // The batch is named in the message, because the shelf may well hold enough in total.
            throw new ConflictException(
                MessageCode.InsufficientStock,
                $"{item.Name} batch {batch.BatchNumber}",
                batch.QuantityOnHand,
                item.Unit,
                request.Quantity);
        }

        var change = takesStock ? -request.Quantity : request.Quantity;

        batch.QuantityOnHand += change;

        _db.PharmacyTransactions.Add(new TransactionEntity
        {
            Id = Guid.NewGuid(),
            PharmacyItemId = item.Id,
            PharmacyBatchId = batch.Id,
            Type = request.Type,
            Quantity = request.Quantity,
            PerformedByStaffId = _currentUser.Id,
            Note = Normalise(request.Note)
        });

        await AddAsync(item.Id, change, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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
                PharmacyBatchId = t.PharmacyBatchId,
                BatchNumber = _db.PharmacyBatches
                    .Where(b => b.Id == t.PharmacyBatchId)
                    .Select(b => (int?)b.BatchNumber)
                    .FirstOrDefault(),
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

    private static void EnsureMovementIsAllowed(CreatePharmacyTransactionRequest request)
    {
        if (request.Type == PharmacyTransactionType.Adjusted
            && string.IsNullOrWhiteSpace(request.Note))
        {
            throw new BadRequestException(MessageCode.AdjustmentNeedsNote);
        }
    }

    // Earliest expiry first, spilling into the next batch when one is not enough: the box that
    // goes out of date soonest is the one that leaves the shelf first.
    private async Task TakeFromBatchesAsync(
        ItemEntity item, CreatePharmacyTransactionRequest request, CancellationToken cancellationToken)
    {
        var batches = await _db.PharmacyBatches
            .Where(b => b.PharmacyItemId == item.Id && b.QuantityOnHand > 0)
            .OrderBy(b => b.ExpiryDate == null)
            .ThenBy(b => b.ExpiryDate)
            .ThenBy(b => b.BatchNumber)
            .ToListAsync(cancellationToken);

        var onHand = batches.Sum(b => b.QuantityOnHand);

        if (onHand < request.Quantity)
        {
            throw new ConflictException(
                MessageCode.InsufficientStock,
                item.Name, onHand, item.Unit, request.Quantity);
        }

        var left = request.Quantity;

        foreach (var batch in batches)
        {
            if (left == 0)
            {
                break;
            }

            var taken = Math.Min(batch.QuantityOnHand, left);
            batch.QuantityOnHand -= taken;
            left -= taken;

            _db.PharmacyTransactions.Add(new TransactionEntity
            {
                Id = Guid.NewGuid(),
                PharmacyItemId = item.Id,
                PharmacyBatchId = batch.Id,
                Type = request.Type,
                Quantity = taken,
                PerformedByStaffId = _currentUser.Id,
                Note = Normalise(request.Note)
            });
        }

        await AddAsync(item.Id, -request.Quantity, cancellationToken);
    }

    // A correction to a count. It lands on the newest batch, which is the one somebody has just
    // been counting; an item with no batches at all gets one, so stock always belongs somewhere.
    private async Task AdjustNewestBatchAsync(
        ItemEntity item, CreatePharmacyTransactionRequest request, CancellationToken cancellationToken)
    {
        var batch = await _db.PharmacyBatches
            .Where(b => b.PharmacyItemId == item.Id)
            .OrderByDescending(b => b.BatchNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (batch is null)
        {
            batch = new BatchEntity
            {
                Id = Guid.NewGuid(),
                PharmacyItemId = item.Id,
                BatchNumber = 1,
                QuantityOnHand = 0,
                Note = Normalise(request.Note)
            };

            _db.PharmacyBatches.Add(batch);
        }

        batch.QuantityOnHand += request.Quantity;

        _db.PharmacyTransactions.Add(new TransactionEntity
        {
            Id = Guid.NewGuid(),
            PharmacyItemId = item.Id,
            PharmacyBatchId = batch.Id,
            Type = request.Type,
            Quantity = request.Quantity,
            PerformedByStaffId = _currentUser.Id,
            Note = Normalise(request.Note)
        });

        await AddAsync(item.Id, request.Quantity, cancellationToken);
    }

    // Every stock movement on an item takes this row lock first, so two people moving the same
    // medicine queue up instead of both reading the same count and both spending it.
    private Task LockAsync(Guid id, CancellationToken cancellationToken)
        => _db.Database.ExecuteSqlAsync(
            $"SELECT id FROM pharmacy_items WHERE id = {id} FOR UPDATE", cancellationToken);

    // The item's total is the batches added up. It moves by the same amount they do, inside the
    // same transaction, so the register and the batch list can never disagree.
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
               .Include(i => i.Batches)
               .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
           ?? throw new NotFoundException("Pharmacy item", id);

    private static IQueryable<ItemEntity> Sort(IQueryable<ItemEntity> items, string sortBy, string sortDir)
    {
        var descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy?.ToLowerInvariant() switch
        {
            "expiry_date" => descending
                ? items.OrderByDescending(i => i.Batches
                    .Where(b => b.QuantityOnHand > 0).Min(b => b.ExpiryDate))
                : items.OrderBy(i => i.Batches
                    .Where(b => b.QuantityOnHand > 0).Min(b => b.ExpiryDate)),
            "quantity_on_hand" => descending
                ? items.OrderByDescending(i => i.QuantityOnHand)
                : items.OrderBy(i => i.QuantityOnHand),
            "created_at" => descending
                ? items.OrderByDescending(i => i.CreatedAt)
                : items.OrderBy(i => i.CreatedAt),
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
            Unit = item.Unit,
            BatchCount = item.Batches.Count,
            EarliestExpiry = item.Batches
                .Where(b => b.QuantityOnHand > 0 && b.ExpiryDate != null)
                .Min(b => b.ExpiryDate),
            QuantityOnHand = item.QuantityOnHand,
            ReorderThreshold = item.ReorderThreshold,
            UnitPrice = item.UnitPrice,
            IsAvailable = item.QuantityOnHand > 0,
            BelowThreshold = item.QuantityOnHand <= item.ReorderThreshold,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };

    private static PharmacyBatch ToBatchDto(BatchEntity batch)
        => new()
        {
            Id = batch.Id,
            PharmacyItemId = batch.PharmacyItemId,
            BatchNumber = batch.BatchNumber,
            Reference = batch.Reference,
            ExpiryDate = batch.ExpiryDate,
            QuantityOnHand = batch.QuantityOnHand,
            Note = batch.Note,
            ReceivedAt = batch.CreatedAt,
            UpdatedAt = batch.UpdatedAt
        };

    private static string? Normalise(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
