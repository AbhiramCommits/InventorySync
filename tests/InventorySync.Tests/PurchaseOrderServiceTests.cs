using InventorySync.Core.Dtos;
using InventorySync.Core.Enums;
using InventorySync.Core.Exceptions;
using InventorySync.Infrastructure.Data;
using InventorySync.Infrastructure.Repositories;
using InventorySync.Infrastructure.Services;
using InventorySync.Tests.TestHelpers;

namespace InventorySync.Tests;

public class PurchaseOrderServiceTests
{
    [Fact]
    public async Task Create_ComputesTotalAmountAndStartsAsDraft()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        var created = await service.CreateAsync(new CreatePurchaseOrderRequest
        {
            PoNumber = "PO-TEST-001",
            VendorCode = "VND-001",
            OrderDateUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpectedDateUtc = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            Lines =
            {
                new CreatePurchaseOrderLineRequest { Sku = "SKU-A", QuantityOrdered = 10, UnitPrice = 1.50m },
                new CreatePurchaseOrderLineRequest { Sku = "SKU-B", QuantityOrdered = 4, UnitPrice = 2.25m },
            },
        });

        Assert.Equal(PurchaseOrderStatus.Draft, created.Status);
        Assert.Equal(24m, created.TotalAmount);
        Assert.Equal(2, created.Lines.Count);
        Assert.All(created.Lines, l => Assert.Equal(0, l.QuantityReceived));
    }

    [Fact]
    public async Task Create_DuplicatePoNumber_ThrowsConflict()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        await service.CreateAsync(ValidRequest());

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(ValidRequest()));
    }

    [Fact]
    public async Task Create_WithoutLines_Throws()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        var request = ValidRequest();
        request.Lines.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request));
    }

    [Fact]
    public async Task Submit_MovesDraftToSubmitted()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        var created = await service.CreateAsync(ValidRequest());
        var submitted = await service.SubmitAsync(created.Id);

        Assert.Equal(PurchaseOrderStatus.Submitted, submitted.Status);
    }

    [Fact]
    public async Task Submit_NonDraftOrder_Throws()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        var created = await service.CreateAsync(ValidRequest());
        await service.SubmitAsync(created.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitAsync(created.Id));
    }

    [Fact]
    public async Task ReceiveLine_TracksPartialAndFullReceipt()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        var created = await service.CreateAsync(ValidRequest());
        await service.SubmitAsync(created.Id);
        var firstLine = created.Lines[0];
        var secondLine = created.Lines[1];

        var partial = await service.ReceiveLineAsync(created.Id, firstLine.Id, new ReceiveLineRequest { Quantity = 3 });
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, partial.Status);

        await service.ReceiveLineAsync(created.Id, firstLine.Id, new ReceiveLineRequest { Quantity = 7 });
        await service.ReceiveLineAsync(created.Id, secondLine.Id, new ReceiveLineRequest { Quantity = 4 });
        var received = await service.GetByIdAsync(created.Id);

        Assert.Equal(PurchaseOrderStatus.Received, received.Status);
        Assert.Equal(10, received.Lines.First(l => l.Id == firstLine.Id).QuantityReceived);
    }

    [Fact]
    public async Task ReceiveLine_OverReceipt_Throws()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        var created = await service.CreateAsync(ValidRequest());
        await service.SubmitAsync(created.Id);
        var line = created.Lines[0];

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReceiveLineAsync(created.Id, line.Id, new ReceiveLineRequest { Quantity = 11 }));
    }

    [Fact]
    public async Task Cancel_MovesSubmittedToCancelled()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        var created = await service.CreateAsync(ValidRequest());
        await service.SubmitAsync(created.Id);

        var cancelled = await service.CancelAsync(created.Id);
        Assert.Equal(PurchaseOrderStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task GetById_Missing_ThrowsNotFoundException()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.GetByIdAsync(999));
    }

    private static CreatePurchaseOrderRequest ValidRequest()
    {
        return new CreatePurchaseOrderRequest
        {
            PoNumber = "PO-TEST-001",
            VendorCode = "VND-001",
            OrderDateUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpectedDateUtc = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            Lines =
            {
                new CreatePurchaseOrderLineRequest { Sku = "SKU-A", QuantityOrdered = 10, UnitPrice = 1.50m },
                new CreatePurchaseOrderLineRequest { Sku = "SKU-B", QuantityOrdered = 4, UnitPrice = 2.25m },
            },
        };
    }

    private static PurchaseOrderService CreateService(SyncDbContext db)
    {
        return new PurchaseOrderService(new PurchaseOrderRepository(db));
    }
}
