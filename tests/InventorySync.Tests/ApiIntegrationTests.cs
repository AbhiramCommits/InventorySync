using System.Net;
using System.Net.Http.Json;

using InventorySync.Core.Dtos;
using InventorySync.Core.Enums;
using InventorySync.Core.Interfaces;
using InventorySync.Core.Options;
using InventorySync.Infrastructure.Data;
using InventorySync.Infrastructure.Erp;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace InventorySync.Tests;

public class ApiIntegrationTests
{
    [Fact]
    public async Task InventoryEndpoints_WorkEndToEnd()
    {
        using var factory = CreateApiFactory();
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

        var searchResponse = await client.GetAsync("/api/inventory?search=API-1&sort=-sku");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        var paged = await searchResponse.Content.ReadFromJsonAsync<PagedResult<InventoryItemDto>>();
        Assert.NotNull(paged);
        Assert.Equal(1, paged.TotalCount);

        var invalidResponse = await client.PostAsJsonAsync("/api/inventory", new
        {
            sku = "",
            name = "",
            quantityOnHand = -1,
            unitCost = 1m,
            warehouseCode = "",
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        var problem = await invalidResponse.Content.ReadFromJsonAsync<ValidationProblemDetailsPayload>();
        Assert.NotNull(problem);
        Assert.True(problem.Errors.Count > 0);

        var missingResponse = await client.GetAsync("/api/inventory/999999");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    [Fact]
    public async Task SyncEndpoints_RunAgainstMockErp_AndReportAudits()
    {
        using var factory = CreateApiFactory();
        using var client = factory.CreateClient();

        var syncResponse = await client.PostAsync("/api/sync/inventory?triggeredBy=integration-test", null);
        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);
        var run = await syncResponse.Content.ReadFromJsonAsync<SyncRunDto>();
        Assert.NotNull(run);
        Assert.Equal(SyncRunStatus.Succeeded, run.Status);
        Assert.Equal(300, run.RecordsRead);
        Assert.Equal(300, run.RecordsInserted);
        Assert.Equal(0, run.RecordsFailed);

        var runsResponse = await client.GetAsync("/api/sync/runs?status=Succeeded");
        Assert.Equal(HttpStatusCode.OK, runsResponse.StatusCode);
        var runs = await runsResponse.Content.ReadFromJsonAsync<PagedResult<SyncRunDto>>();
        Assert.NotNull(runs);
        Assert.Equal(1, runs.TotalCount);

        var runByIdResponse = await client.GetAsync($"/api/sync/runs/{run.Id}");
        Assert.Equal(HttpStatusCode.OK, runByIdResponse.StatusCode);

        var auditsResponse = await client.GetAsync($"/api/sync/runs/{run.Id}/audit?action=Insert&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, auditsResponse.StatusCode);
        var audits = await auditsResponse.Content.ReadFromJsonAsync<PagedResult<SyncAuditEntryDto>>();
        Assert.NotNull(audits);
        Assert.Equal(300, audits.TotalCount);
        Assert.Equal(10, audits.Items.Count);
    }

    [Fact]
    public async Task PurchaseOrderSync_And_Retry_Flow_Work()
    {
        using var factory = CreateApiFactory();
        using var client = factory.CreateClient();

        var syncResponse = await client.PostAsync("/api/sync/purchase-orders?triggeredBy=integration-test", null);
        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);
        var run = await syncResponse.Content.ReadFromJsonAsync<SyncRunDto>();
        Assert.NotNull(run);
        Assert.Equal(SyncRunStatus.Succeeded, run.Status);
        Assert.Equal(80, run.RecordsInserted);

        var retryResponse = await client.PostAsync($"/api/sync/runs/{run.Id}/retry", null);
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        var child = await retryResponse.Content.ReadFromJsonAsync<SyncRunDto>();
        Assert.NotNull(child);
        Assert.Equal(run.Id, child.ParentSyncRunId);
        Assert.Equal(0, child.RecordsRead);
        Assert.Equal(SyncRunStatus.Succeeded, child.Status);
    }

    [Fact]
    public async Task PurchaseOrderLifecycle_WorksThroughApi()
    {
        using var factory = CreateApiFactory();
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
        Assert.NotNull(received.LocallyModifiedUtc);

        var overReceiveResponse = await client.PostAsJsonAsync(
            $"/api/purchaseorders/{created.Id}/lines/{created.Lines[0].Id}/receive",
            new { quantity = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, overReceiveResponse.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_ReportsDatabaseAndErp()
    {
        using var factory = CreateApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("database", body);
        Assert.Contains("erp", body);
        Assert.Contains("Healthy", body);
    }

    [Fact]
    public async Task StaticUi_IsServedFromWwwroot()
    {
        using var factory = CreateApiFactory();
        using var client = factory.CreateClient();

        var index = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        Assert.Contains("text/html", index.Content.Headers.ContentType!.MediaType);
        var html = await index.Content.ReadAsStringAsync();
        Assert.Contains("InventorySync", html);
        Assert.Contains("/js/main.js", html);

        var js = await client.GetAsync("/js/main.js");
        Assert.Equal(HttpStatusCode.OK, js.StatusCode);
        Assert.Contains("text/javascript", js.Content.Headers.ContentType!.MediaType);

        var css = await client.GetAsync("/css/site.css");
        Assert.Equal(HttpStatusCode.OK, css.StatusCode);
        Assert.Contains("text/css", css.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Cors_AppliesConfiguredPolicy()
    {
        using var factory = CreateApiFactory();
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/inventory?pageSize=1");
        request.Headers.Add("Origin", "http://localhost:5173");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    private static TestApiFactory CreateApiFactory()
    {
        var mockErpFactory = new MockErpTestFactory();
        var apiFactory = new TestApiFactory(mockErpFactory);
        return apiFactory;
    }

    private sealed class MockErpTestFactory : WebApplicationFactory<MockErp.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Erp:InventoryCount"] = "300",
                    ["Erp:PurchaseOrderCount"] = "80",
                    ["Erp:Seed"] = "20240101",
                });
            });
        }
    }

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"api-tests-{Guid.NewGuid():N}";
        private readonly MockErpTestFactory _mockErp;

        public TestApiFactory(MockErpTestFactory mockErp)
        {
            _mockErp = mockErp;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=InventorySync;User Id=sa;Password=unused;TrustServerCertificate=True",
                });
            });

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

                var erpClientName = typeof(IErpClient).FullName!;
                var staleClientConfigs = services
                    .Where(d => d.ServiceType == typeof(IConfigureOptions<HttpClientFactoryOptions>)
                        && d.ImplementationInstance is ConfigureNamedOptions<HttpClientFactoryOptions> named
                        && named.Name == erpClientName)
                    .ToList();

                foreach (var descriptor in staleClientConfigs)
                {
                    services.Remove(descriptor);
                }

                services.RemoveAll<IErpClient>();
                services.RemoveAll<ErpHttpClient>();

                services.AddHttpClient<IErpClient, ErpHttpClient>(client => client.BaseAddress = new Uri("http://mock-erp.test/"))
                    .AddPolicyHandler(ErpPolicies.RetryPolicy())
                    .AddPolicyHandler(ErpPolicies.CircuitBreakerPolicy())
                    .ConfigurePrimaryHttpMessageHandler(() => _mockErp.Server.CreateHandler());

                services.AddHttpClient("erp-health")
                    .ConfigurePrimaryHttpMessageHandler(() => _mockErp.Server.CreateHandler());

                services.Configure<ErpOptions>(o => o.BaseUrl = "http://mock-erp.test/");
            });
        }
    }

    private sealed class ValidationProblemDetailsPayload
    {
        public Dictionary<string, string[]> Errors { get; set; } = new();
    }
}
