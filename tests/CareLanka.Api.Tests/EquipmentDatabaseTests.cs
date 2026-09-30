using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Equipment;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

// Database-level tests: these go straight through CareLankaDbContext against a real
// Postgres instance (via Testcontainers), instead of through HTTP like
// EquipmentBeginnerTests.cs. This is what proves DATABASE testing, as distinct from
// API testing - we're checking the constraint the database itself enforces, not
// what the controller/service layer happens to reject first.
[Collection(ApiCollection.Name)]
public sealed class EquipmentDatabaseTests
{
    private readonly ApiApplication _application;

    public EquipmentDatabaseTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task The_database_rejects_two_active_items_sharing_an_asset_tag()
    {
        // EquipmentItemConfiguration.cs defines a unique filtered index,
        // ux_equipment_items_asset_tag, over active items only. We insert one item,
        // then try to insert a second one with the SAME asset tag, going straight to
        // the database - bypassing any application-level uniqueness check - to prove
        // the constraint itself, not just the service's pre-check, actually holds.
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var sharedTag = $"DB-TEST-{Guid.NewGuid():N}"[..14];
        var category = NewCategory();
        db.EquipmentCategories.Add(category);
        db.EquipmentItems.Add(NewItem(category.Id, sharedTag));
        await db.SaveChangesAsync();

        db.EquipmentItems.Add(NewItem(category.Id, sharedTag));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task The_database_refuses_to_delete_a_category_that_still_has_items()
    {
        // EquipmentItemConfiguration.cs sets OnDelete(DeleteBehavior.Restrict) on the
        // Category -> Items relationship.
        //
        // DEFECT FOUND IN OUR OWN TEST DESIGN (not the app): this test originally called
        // db.EquipmentCategories.Remove(category) and expected DbUpdateException. It
        // failed with InvalidOperationException instead - EF Core's in-memory change
        // tracker severs the tracked relationship and rejects the removal itself, before
        // any SQL is even sent. That only proves EF's client-side tracking behaviour,
        // not that the DATABASE's own foreign-key constraint holds. Fixed by issuing a
        // raw SQL DELETE, bypassing the change tracker entirely, so the real Postgres
        // foreign-key constraint is what rejects it.
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var category = NewCategory();
        db.EquipmentCategories.Add(category);
        db.EquipmentItems.Add(NewItem(category.Id, $"DB-TEST-{Guid.NewGuid():N}"[..14]));
        await db.SaveChangesAsync();

        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM equipment_categories WHERE id = {category.Id}"));
    }

    private static EquipmentCategory NewCategory() => new()
    {
        Id = Guid.NewGuid(),
        Name = $"DB Test Category {Guid.NewGuid():N}"[..30],
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static EquipmentItem NewItem(Guid categoryId, string assetTag) => new()
    {
        Id = Guid.NewGuid(),
        Name = "DB Test Ventilator",
        CategoryId = categoryId,
        Model = "V-100",
        Manufacturer = "Acme Medical",
        PurchaseDate = new DateOnly(2026, 1, 5),
        Status = EquipmentStatus.Available,
        AssetTag = assetTag,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}
