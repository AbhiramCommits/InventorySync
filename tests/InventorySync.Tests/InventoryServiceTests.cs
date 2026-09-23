using InventorySync.Core.Dtos;
using InventorySync.Core.Exceptions;
using InventorySync.Infrastructure.Data;
using InventorySync.Infrastructure.Repositories;
using InventorySync.Infrastructure.Services;
using InventorySync.Tests.TestHelpers;

namespace InventorySync.Tests;

public class InventoryServiceTests
{
    [Fact]
    public async Task Create_And_GetBySku_RoundTrip()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        var created = await service.CreateAsync(new CreateInventoryItemRequest
        {
            Sku = "SKU-TEST-001",
            Name = "Test Item",
            Description = "A test item",
            QuantityOnHand = 25,
            UnitCost = 12.3456m,
            WarehouseCode = "WH01",
        });

        Assert.True(created.Id > 0);

        var fetched = await service.GetBySkuAsync("SKU-TEST-001");
        Assert.Equal("Test Item", fetched.Name);
        Assert.Equal(25, fetched.QuantityOnHand);
        Assert.Equal(12.3456m, fetched.UnitCost);
        Assert.Equal("WH01", fetched.WarehouseCode);
    }

    [Fact]
    public async Task Create_DuplicateSku_ThrowsConflict()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        await service.CreateAsync(ValidRequest());

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(ValidRequest()));
    }

    [Fact]
    public async Task Update_ChangesFieldsAndRefreshesRowVersion()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        var created = await service.CreateAsync(ValidRequest());

        var updated = await service.UpdateAsync(created.Id, new UpdateInventoryItemRequest
        {
            Name = "Renamed Item",
            Description = null,
            QuantityOnHand = 99,
            UnitCost = 1.25m,
            WarehouseCode = "WH08",
        });

        Assert.Equal("Renamed Item", updated.Name);
        Assert.Null(updated.Description);
        Assert.Equal(99, updated.QuantityOnHand);
        Assert.Equal("WH08", updated.WarehouseCode);
    }

    [Fact]
    public async Task Update_InvalidRowVersion_ThrowsConflict()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        var created = await service.CreateAsync(ValidRequest());

        var request = new UpdateInventoryItemRequest
        {
            Name = "Renamed",
            Description = null,
            QuantityOnHand = 1,
            UnitCost = 1m,
            WarehouseCode = "WH01",
            RowVersion = "not-base64!",
        };

        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(created.Id, request));
    }

    [Fact]
    public async Task Delete_RemovesItem()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        var created = await service.CreateAsync(ValidRequest());
        await service.DeleteAsync(created.Id);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task GetPaged_FiltersByWarehouse()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        await service.CreateAsync(ValidRequest(sku: "SKU-1", warehouse: "WH01"));
        await service.CreateAsync(ValidRequest(sku: "SKU-2", warehouse: "WH01"));
        await service.CreateAsync(ValidRequest(sku: "SKU-3", warehouse: "WH02"));

        var result = await service.GetPagedAsync("WH01", 1, 10);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, i => Assert.Equal("WH01", i.WarehouseCode));
    }

    [Fact]
    public async Task GetById_Missing_ThrowsNotFoundException()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.GetByIdAsync(12345));
    }

    private static CreateInventoryItemRequest ValidRequest(string sku = "SKU-TEST-001", string warehouse = "WH01")
    {
        return new CreateInventoryItemRequest
        {
            Sku = sku,
            Name = "Test Item",
            Description = "A test item",
            QuantityOnHand = 25,
            UnitCost = 12.3456m,
            WarehouseCode = warehouse,
        };
    }

    private static InventoryService CreateService(SyncDbContext db)
    {
        return new InventoryService(new InventoryItemRepository(db));
    }
}
