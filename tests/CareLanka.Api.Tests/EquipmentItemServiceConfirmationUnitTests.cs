using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Equipment;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Common;
using CareLanka.Api.Services.Equipment;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CareLanka.Api.Tests;

// A true UNIT test, distinct from the rest of this suite: no HTTP, no real Postgres.
// EquipmentItemService is constructed directly with an EF Core InMemory database and
// hand-written fakes for ICurrentUser/IEquipmentConfirmationCode, isolating just the
// EnsureConfirmationCode business rule (equipment administrator skips the code,
// hospital administrator does not) from the controller, auth middleware, and network.
public sealed class EquipmentItemServiceConfirmationUnitTests
{
    [Fact]
    public async Task An_equipment_administrator_confirming_never_calls_Ensure_on_the_confirmation_code()
    {
        await using var db = NewInMemoryDb();
        var item = await SeedAwaitingItemAsync(db);
        var confirmationCode = new RecordingConfirmationCode();
        var service = new EquipmentItemService(
            db,
            new FakeWardDirectory(),
            new FakeCurrentUser(PrincipalRole.EquipmentAdministrator),
            confirmationCode);

        await service.ConfirmAsync(item.Id, confirmationCode: null);

        Assert.False(confirmationCode.WasCalled);
    }

    [Fact]
    public async Task A_hospital_administrator_confirming_always_calls_Ensure_on_the_confirmation_code()
    {
        await using var db = NewInMemoryDb();
        var item = await SeedAwaitingItemAsync(db);
        var confirmationCode = new RecordingConfirmationCode();
        var service = new EquipmentItemService(
            db,
            new FakeWardDirectory(),
            new FakeCurrentUser(PrincipalRole.HospitalAdministrator),
            confirmationCode);

        await service.ConfirmAsync(item.Id, confirmationCode: "whatever-was-typed");

        Assert.True(confirmationCode.WasCalled);
    }

    private static CareLankaDbContext NewInMemoryDb()
    {
        // A fresh, isolated database per test - no Testcontainers, no network, no
        // shared state between tests.
        var options = new DbContextOptionsBuilder<CareLankaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new CareLankaDbContext(options);
    }

    private static async Task<EquipmentItem> SeedAwaitingItemAsync(CareLankaDbContext db)
    {
        var category = new EquipmentCategory
        {
            Id = Guid.NewGuid(),
            Name = "Unit Test Category",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var item = new EquipmentItem
        {
            Id = Guid.NewGuid(),
            Name = "Unit Test Ventilator",
            CategoryId = category.Id,
            Model = "V-100",
            Manufacturer = "Acme Medical",
            PurchaseDate = new DateOnly(2026, 1, 5),
            Status = EquipmentStatus.Available,
            AssetTag = $"EQ-{Guid.NewGuid():N}"[..14],
            AwaitingConfirmation = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        db.EquipmentCategories.Add(category);
        db.EquipmentItems.Add(item);
        await db.SaveChangesAsync();

        return item;
    }

    private sealed class RecordingConfirmationCode : IEquipmentConfirmationCode
    {
        public bool WasCalled { get; private set; }

        public void Ensure(string? supplied) => WasCalled = true;
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public FakeCurrentUser(PrincipalRole role) => Role = role;

        public bool IsAuthenticated => true;

        public Guid Id { get; } = Guid.NewGuid();

        public PrincipalType PrincipalType => PrincipalType.Staff;

        public PrincipalRole Role { get; }
    }

    private sealed class FakeWardDirectory : IWardDirectory
    {
        public Task<IReadOnlyDictionary<Guid, string>> GetWardNamesAsync(
            IReadOnlyCollection<Guid> wardIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
    }
}
