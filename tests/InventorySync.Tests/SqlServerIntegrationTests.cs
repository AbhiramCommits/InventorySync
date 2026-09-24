using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using InventorySync.Core.Dtos;
using InventorySync.Core.Dtos.Erp;
using InventorySync.Core.Entities;
using InventorySync.Core.Enums;
using InventorySync.Core.Interfaces;
using InventorySync.Infrastructure.Data;
using InventorySync.Tests.TestHelpers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Xunit.Abstractions;

namespace InventorySync.Tests;

public class SqlServerIntegrationTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SqlServerIntegrationTests(SqlServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private (SqlServerApiFactory Factory, HttpClient Client, FakeErpClient Erp) CreateFactory(
        IEnumerable<ErpInventoryItemRecord>? items = null,
        IEnumerable<ErpPurchaseOrderRecord>? orders = null)
    {
        var erp = new FakeErpClient(items, orders);
        var factory = new SqlServerApiFactory(_fixture.ConnectionString, erp);
        var client = factory.CreateClient();
        return (factory, client, erp);
    }

    [Fact]
    public async Task Crud_RoundTrip_Works()
    {
        var (factory, client, _) = CreateFactory();
        using (factory)
        using (client)
        {
            var sku = $"SKU-CRUD-{Guid.NewGuid():N}";

            var createResponse = await client.PostAsJsonAsync("/api/inventory", new
            {
                sku,
                name = "Crud Item",
                description = (string?)null,
                quantityOnHand = 12,
                unitCost = 9.99m,
                warehouseCode = "WH01",
            });
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var created = await createResponse.Content.ReadFromJsonAsync<InventoryItemDto>();
            created.Should().NotBeNull();
            created!.RowVersion.Should().NotBeNull();

            var getResponse = await client.GetAsync($"/api/inventory/{created.Id}");
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var fetched = await getResponse.Content.ReadFromJsonAsync<InventoryItemDto>();
            fetched!.Sku.Should().Be(sku);

            var updateResponse = await client.PutAsJsonAsync($"/api/inventory/{created.Id}", new
            {
                name = "Crud Item Updated",
                description = (string?)null,
                quantityOnHand = 42,
                unitCost = 9.99m,
                warehouseCode = "WH02",
                rowVersion = created.RowVersion,
            });
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var updated = await updateResponse.Content.ReadFromJsonAsync<InventoryItemDto>();
            updated!.QuantityOnHand.Should().Be(42);
            updated.WarehouseCode.Should().Be("WH02");

            var deleteResponse = await client.DeleteAsync($"/api/inventory/{created.Id}");
            deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var missingResponse = await client.GetAsync($"/api/inventory/{created.Id}");
            missingResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task Paging_Boundaries_AreCorrect()
    {
        var (factory, client, _) = CreateFactory();
        using (factory)
        using (client)
        {
            var prefix = $"SKU-PAGE-{Guid.NewGuid():N}";
            var skus = Enumerable.Range(1, 25).Select(i => $"{prefix}-{i:D2}").ToList();

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();
                db.InventoryItems.AddRange(skus.Select(s => new InventoryItem
                {
                    Sku = s,
                    Name = $"Paged {s}",
                    QuantityOnHand = 10,
                    UnitCost = 1m,
                    WarehouseCode = "WH03",
                }));
                await db.SaveChangesAsync();
            }

            var page1 = await client.GetFromJsonAsync<PagedResult<InventoryItemDto>>($"/api/inventory?search={prefix}&page=1&pageSize=10");
            var page2 = await client.GetFromJsonAsync<PagedResult<InventoryItemDto>>($"/api/inventory?search={prefix}&page=2&pageSize=10");
            var page3 = await client.GetFromJsonAsync<PagedResult<InventoryItemDto>>($"/api/inventory?search={prefix}&page=3&pageSize=10");

            page1!.Items.Should().HaveCount(10);
            page2!.Items.Should().HaveCount(10);
            page3!.Items.Should().HaveCount(5);
            page1.TotalCount.Should().Be(25);
            page1.TotalPages.Should().Be(3);

            var pastEnd = await client.GetFromJsonAsync<PagedResult<InventoryItemDto>>($"/api/inventory?search={prefix}&page=99&pageSize=10");
            pastEnd!.Items.Should().BeEmpty();
            pastEnd.TotalCount.Should().Be(25);

            var clamped = await client.GetFromJsonAsync<PagedResult<InventoryItemDto>>($"/api/inventory?search={prefix}&page=1&pageSize=9999");
            clamped!.Items.Should().HaveCount(25);
        }
    }

    [Fact]
    public async Task Validation_Failures_ReturnProblemDetails400()
    {
        var (factory, client, _) = CreateFactory();
        using (factory)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/inventory", new
            {
                sku = "",
                name = "",
                quantityOnHand = -5,
                unitCost = 1m,
                warehouseCode = "",
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetailsPayload>();
            problem.Should().NotBeNull();
            problem!.Title.Should().NotBeNullOrEmpty();
            problem.Status.Should().Be(400);
            problem.Errors.Should().ContainKeys("Sku", "Name", "QuantityOnHand", "WarehouseCode");
        }
    }

    [Fact]
    public async Task Missing_Entities_Return404ProblemDetails()
    {
        var (factory, client, _) = CreateFactory();
        using (factory)
        using (client)
        {
            var inventory = await client.GetAsync("/api/inventory/99999999");
            inventory.StatusCode.Should().Be(HttpStatusCode.NotFound);
            inventory.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

            var order = await client.GetAsync("/api/purchaseorders/99999999");
            order.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var run = await client.GetAsync("/api/sync/runs/99999999");
            run.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task Concurrency_Conflict_Returns409()
    {
        var (factory, client, _) = CreateFactory();
        using (factory)
        using (client)
        {
            var sku = $"SKU-CONC-{Guid.NewGuid():N}";

            var createResponse = await client.PostAsJsonAsync("/api/inventory", new
            {
                sku,
                name = "Concurrency Item",
                description = (string?)null,
                quantityOnHand = 1,
                unitCost = 1m,
                warehouseCode = "WH01",
            });
            var created = await createResponse.Content.ReadFromJsonAsync<InventoryItemDto>();

            var firstUpdate = await client.PutAsJsonAsync($"/api/inventory/{created!.Id}", new
            {
                name = "Concurrency Item",
                description = (string?)null,
                quantityOnHand = 2,
                unitCost = 1m,
                warehouseCode = "WH01",
                rowVersion = created.RowVersion,
            });
            firstUpdate.StatusCode.Should().Be(HttpStatusCode.OK);

            var staleUpdate = await client.PutAsJsonAsync($"/api/inventory/{created.Id}", new
            {
                name = "Concurrency Item",
                description = (string?)null,
                quantityOnHand = 3,
                unitCost = 1m,
                warehouseCode = "WH01",
                rowVersion = created.RowVersion,
            });

            staleUpdate.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var problem = await staleUpdate.Content.ReadFromJsonAsync<ProblemDetailsPayload>();
            problem!.Status.Should().Be(409);
            problem.Title.Should().Contain("concurrent");
        }
    }

    [Fact]
    public async Task FullSync_AgainstStubbedErp_InsertsRowsAndAudits()
    {
        var prefix = $"SKU-SYNC-{Guid.NewGuid():N}";
        var (factory, client, _) = CreateFactory(items: new[]
        {
            ErpRecordFactory.Item($"{prefix}-1", quantityOnHand: "10", unitCost: "1.5000"),
            ErpRecordFactory.Item($"{prefix}-2", quantityOnHand: "20", unitCost: "2.5000"),
            ErpRecordFactory.Item($"{prefix}-BAD", quantityOnHand: "NOPE"),
        });

        using (factory)
        using (client)
        {
            var syncResponse = await client.PostAsync("/api/sync/inventory?triggeredBy=integration", null);
            syncResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var run = await syncResponse.Content.ReadFromJsonAsync<SyncRunDto>();

            run!.Status.Should().Be(SyncRunStatus.PartialSuccess);
            run.RecordsRead.Should().Be(3);
            run.RecordsInserted.Should().Be(2);
            run.RecordsFailed.Should().Be(1);

            var list = await client.GetFromJsonAsync<PagedResult<InventoryItemDto>>($"/api/inventory?search={prefix}&pageSize=50");
            list!.TotalCount.Should().Be(2);

            var audits = await client.GetFromJsonAsync<PagedResult<SyncAuditEntryDto>>($"/api/sync/runs/{run.Id}/audit?action=4");
            audits!.TotalCount.Should().Be(1);
            audits.Items.Single().EntityKey.Should().Be($"{prefix}-BAD");

            var inserts = await client.GetFromJsonAsync<PagedResult<SyncAuditEntryDto>>($"/api/sync/runs/{run.Id}/audit?action=0");
            inserts!.TotalCount.Should().Be(2);
        }
    }

    [Fact]
    public async Task Retry_RePullsOnlyFailedKeys_AndCreatesChildRun()
    {
        var prefix = $"SKU-RETRY-{Guid.NewGuid():N}";
        var (factory, client, erp) = CreateFactory(items: new[]
        {
            ErpRecordFactory.Item($"{prefix}-GOOD"),
            ErpRecordFactory.Item($"{prefix}-BAD", quantityOnHand: "NOPE"),
        });

        using (factory)
        using (client)
        {
            var syncResponse = await client.PostAsync("/api/sync/inventory?triggeredBy=integration", null);
            var parent = await syncResponse.Content.ReadFromJsonAsync<SyncRunDto>();
            parent!.Status.Should().Be(SyncRunStatus.PartialSuccess);

            erp.ReplaceItem($"{prefix}-BAD", ErpRecordFactory.Item($"{prefix}-BAD", quantityOnHand: "42"));

            var retryResponse = await client.PostAsync($"/api/sync/runs/{parent.Id}/retry", null);
            retryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var child = await retryResponse.Content.ReadFromJsonAsync<SyncRunDto>();

            child!.ParentSyncRunId.Should().Be(parent.Id);
            child.Status.Should().Be(SyncRunStatus.Succeeded);
            child.RecordsRead.Should().Be(1);
            child.RecordsInserted.Should().Be(1);
            child.RecordsFailed.Should().Be(0);

            erp.LastRequestedSkus.Should().ContainSingle().Which.Should().Be($"{prefix}-BAD");

            var list = await client.GetFromJsonAsync<PagedResult<InventoryItemDto>>($"/api/inventory?search={prefix}&pageSize=50");
            list!.TotalCount.Should().Be(2);
        }
    }

    [Fact]
    public async Task IncludeLines_LoadsLinesInOneRequest()
    {
        var prefix = $"PO-INC-{Guid.NewGuid():N}";

        var (factory, client, _) = CreateFactory();
        using (factory)
        using (client)
        {
            var createResponse = await client.PostAsJsonAsync("/api/purchaseorders", new
            {
                poNumber = $"{prefix}-1",
                vendorCode = "VND-001",
                orderDateUtc = DateTime.UtcNow.Date,
                expectedDateUtc = DateTime.UtcNow.Date.AddDays(5),
                lines = new[]
                {
                    new { sku = "SKU-INC-A", quantityOrdered = 3, unitPrice = 1.5m },
                    new { sku = "SKU-INC-B", quantityOrdered = 4, unitPrice = 2.5m },
                },
            });
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var withLines = await client.GetFromJsonAsync<PagedResult<PurchaseOrderDto>>($"/api/purchaseorders?search={prefix}&includeLines=true");
            withLines!.TotalCount.Should().Be(1);
            withLines.Items.Single().Lines.Should().HaveCount(2);

            var withoutLines = await client.GetFromJsonAsync<PagedResult<PurchaseOrderDto>>($"/api/purchaseorders?search={prefix}");
            withoutLines!.Items.Single().Lines.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task NPlusOne_IncludePattern_ReducesRoundTrips()
    {
        var prefix = $"PO-N1-{Guid.NewGuid():N}";

        using (var context = _fixture.CreateContext())
        {
            await context.Database.MigrateAsync();

            for (var i = 1; i <= 20; i++)
            {
                context.PurchaseOrders.Add(new PurchaseOrder
                {
                    PoNumber = $"{prefix}-{i:D2}",
                    VendorCode = "VND-001",
                    Status = PurchaseOrderStatus.Submitted,
                    OrderDateUtc = DateTime.UtcNow.Date,
                    ExpectedDateUtc = DateTime.UtcNow.Date.AddDays(7),
                    TotalAmount = 30m,
                    Lines = Enumerable.Range(1, 3).Select(j => new PurchaseOrderLine
                    {
                        Sku = $"SKU-N1-{j}",
                        QuantityOrdered = 10,
                        QuantityReceived = 0,
                        UnitPrice = 1m,
                    }).ToList(),
                });
            }

            await context.SaveChangesAsync();
        }

        var naiveCommands = await CountRoundTripsAsync(async (ctx) =>
        {
            var orders = await ctx.PurchaseOrders.AsNoTracking().Where(o => o.PoNumber.StartsWith(prefix)).ToListAsync();
            foreach (var order in orders)
            {
                _ = await ctx.PurchaseOrderLines.Where(l => l.PurchaseOrderId == order.Id).ToListAsync();
            }
        });

        var includedCommands = await CountRoundTripsAsync(async (ctx) =>
        {
            var orders = await ctx.PurchaseOrders.AsNoTracking()
                .Include(o => o.Lines)
                .Where(o => o.PoNumber.StartsWith(prefix))
                .ToListAsync();
            _ = orders.Sum(o => o.Lines.Count);
        });

        naiveCommands.Should().Be(21);
        includedCommands.Should().Be(1);

        _output.WriteLine($"N+1 pattern: {naiveCommands} round trips vs Include pattern: {includedCommands} round trips");
    }

    private async Task<int> CountRoundTripsAsync(Func<SyncDbContext, Task> action)
    {
        var executed = 0;
        var options = new DbContextOptionsBuilder<SyncDbContext>()
            .UseSqlServer(_fixture.ConnectionString)
            .LogTo(
                _ => { },
                (eventId, _) =>
                {
                    if (eventId == RelationalEventId.CommandExecuted)
                    {
                        executed++;
                    }

                    return true;
                })
            .Options;

        await using var db = new SyncDbContext(options);
        await action(db);
        return executed;
    }

    private sealed class SqlServerApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;
        private readonly FakeErpClient _erpClient;

        public SqlServerApiFactory(string connectionString, FakeErpClient erpClient)
        {
            _connectionString = connectionString;
            _erpClient = erpClient;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                });
            });

            builder.ConfigureServices(services =>
            {
                var dbDescriptors = services
                    .Where(d => d.ServiceType == typeof(SyncDbContext) || d.ServiceType == typeof(DbContextOptions<SyncDbContext>))
                    .ToList();
                foreach (var descriptor in dbDescriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<SyncDbContext>(o => o.UseSqlServer(_connectionString));

                services.RemoveAll(typeof(IErpClient));
                services.AddSingleton<IErpClient>(_erpClient);
            });
        }
    }

    private sealed class ValidationProblemDetailsPayload
    {
        public string? Title { get; set; }

        public int? Status { get; set; }

        public Dictionary<string, string[]> Errors { get; set; } = new();
    }

    private sealed class ProblemDetailsPayload
    {
        public string? Title { get; set; }

        public int? Status { get; set; }
    }
}
