using System.Net;
using System.Net.Http.Json;
using InventorySync.Core.Dtos;
using InventorySync.Core.Enums;
using InventorySync.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventorySync.Tests;

public class ApiIntegrationTests
{
    [Fact]
    public async Task InventoryEndpoints_WorkEndToEnd()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/inventory", new
        {
            sku = "SKU-API-1",
            name = "Api Item",
            description = (string?)null,
            quantityOnHand = 5,
            unitCost = 10.5m,
            warehouseCode = "WH01",
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<InventoryItemDto>();
        Assert.NotNull(created);

        var getResponse = await client.GetAsync($"/api/inventory/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getBySkuResponse = await client.GetAsync("/api/inventory/sku/SKU-API-1");
        Assert.Equal(HttpStatusCode.OK, getBySkuResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/inventory?warehouseCode=WH01");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var paged = await listResponse.Content.ReadFromJsonAsync<PagedResult<InventoryItemDto>>();
        Assert.NotNull(paged);
        Assert.Equal(1, paged.TotalCount);

        var missingResponse = await client.GetAsync("/api/inventory/999999");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    [Fact]
    public async Task SyncEndpoints_RunAndReport()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var syncResponse = await client.PostAsync("/api/sync/inventory?triggeredBy=integration-test", null);
        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);
        var result = await syncResponse.Content.ReadFromJsonAsync<SyncResultDto>();
        Assert.NotNull(result);
        Assert.Equal(SyncRunStatus.Succeeded, result.Status);
        Assert.True(result.RecordsInserted > 0);

        var runsResponse = await client.GetAsync("/api/sync/runs");
        Assert.Equal(HttpStatusCode.OK, runsResponse.StatusCode);
        var runs = await runsResponse.Content.ReadFromJsonAsync<PagedResult<SyncRunDto>>();
        Assert.NotNull(runs);
        Assert.True(runs.TotalCount >= 1);

        var auditsResponse = await client.GetAsync($"/api/sync/runs/{result.SyncRunId}/audits?pageSize=10");
        Assert.Equal(HttpStatusCode.OK, auditsResponse.StatusCode);
        var audits = await auditsResponse.Content.ReadFromJsonAsync<List<SyncAuditEntryDto>>();
        Assert.NotNull(audits);
        Assert.Equal(10, audits.Count);
    }

    [Fact]
    public async Task PurchaseOrderLifecycle_WorksThroughApi()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var request = new
        {
            poNumber = "PO-API-1",
            vendorCode = "VND-001",
            orderDateUtc = DateTime.UtcNow.Date,
            expectedDateUtc = DateTime.UtcNow.Date.AddDays(7),
            lines = new[]
            {
                new { sku = "SKU-X", quantityOrdered = 5, unitPrice = 3.5m },
            },
        };

        var createResponse = await client.PostAsJsonAsync("/api/purchaseorders", request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();
        Assert.NotNull(created);

        var submitResponse = await client.PostAsync($"/api/purchaseorders/{created.Id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        var receiveResponse = await client.PostAsJsonAsync(
            $"/api/purchaseorders/{created.Id}/lines/{created.Lines[0].Id}/receive",
            new { quantity = 5 });
        Assert.Equal(HttpStatusCode.OK, receiveResponse.StatusCode);
        var received = await receiveResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();
        Assert.NotNull(received);
        Assert.Equal(PurchaseOrderStatus.Received, received.Status);

        var overReceiveResponse = await client.PostAsJsonAsync(
            $"/api/purchaseorders/{created.Id}/lines/{created.Lines[0].Id}/receive",
            new { quantity = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, overReceiveResponse.StatusCode);
    }

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"api-tests-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(SyncDbContext));
                if (dbContextDescriptor is not null)
                {
                    services.Remove(dbContextDescriptor);
                }

                var optionsDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<SyncDbContext>));
                if (optionsDescriptor is not null)
                {
                    services.Remove(optionsDescriptor);
                }

                services.AddDbContext<SyncDbContext>(o => o.UseInMemoryDatabase(_databaseName));
            });
        }
    }
}
