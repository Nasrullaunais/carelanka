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

    [Fact]
    public async Task A_failed_save_rolls_back_every_entity_in_it_not_just_the_one_that_failed()
    {
        // TRANSACTION testing: a single SaveChangesAsync() wraps every pending change in
        // one atomic transaction. Here a brand-new, perfectly valid category is queued
        // alongside an item that will fail the asset-tag uniqueness constraint. If the
        // save were not atomic, the valid category could be left behind even though the
        // whole call threw. We re-read through a SEPARATE DbContext afterwards, so
        // nothing left in the first context's local change tracker can hide the answer.
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var sharedTag = $"DB-TEST-{Guid.NewGuid():N}"[..14];
        var existingCategory = NewCategory();
        db.EquipmentCategories.Add(existingCategory);
        db.EquipmentItems.Add(NewItem(existingCategory.Id, sharedTag));
        await db.SaveChangesAsync();

        var newCategoryInTheSameSave = NewCategory();
        db.EquipmentCategories.Add(newCategoryInTheSameSave);
        db.EquipmentItems.Add(NewItem(existingCategory.Id, sharedTag)); // duplicate tag - will fail

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        using var freshScope = _application.Services.CreateScope();
        var freshDb = freshScope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var survived = await freshDb.EquipmentCategories.AsNoTracking()
            .AnyAsync(c => c.Id == newCategoryInTheSameSave.Id);

        Assert.False(survived);
    }

    [Fact]
    public async Task The_staff_members_role_constraint_in_the_real_database_lists_equipment_administrator()
    {
        // MIGRATION testing: every other test here only proves migrations run without
        // throwing (ApiApplication.InitializeAsync calls MigrateAsync for every test).
        // This checks the actual, specific effect our migration
        // (20260930202957_Common_AddEquipmentAdministratorRole) was meant to have: the
        // ck_staff_members_role check constraint in the REAL Postgres catalog, not the
        // C# model, must list the new role. Read with pg_get_constraintdef, bypassing
        // EF entirely, so this can't pass just because the C# enum looks right.
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var definition = await db.Database
            .SqlQueryRaw<string>(
                "SELECT pg_get_constraintdef(oid) AS \"Value\" FROM pg_constraint WHERE conname = 'ck_staff_members_role'")
            .SingleAsync();

        Assert.Contains("equipment_administrator", definition);
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
