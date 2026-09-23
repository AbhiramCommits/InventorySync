using System.Net;

using InventorySync.Core.Dtos.Erp;
using InventorySync.Infrastructure.Erp;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;

using Polly;
using Polly.CircuitBreaker;

namespace InventorySync.Tests;

public class ErpHttpClientTests
{
    [Fact]
    public async Task GetInventoryAsync_PagesThroughMockErp()
    {
        using var factory = CreateMockErpFactory();
        var client = CreateClient(factory);

        var records = await client.GetInventoryAsync(null);

        Assert.Equal(120, records.Count);
        Assert.Equal(120, records.Select(r => r.Sku).Distinct().Count());
    }

    [Fact]
    public async Task GetInventoryBySkusAsync_ReturnsOnlyRequested()
    {
        using var factory = CreateMockErpFactory();
        var client = CreateClient(factory);

        var records = await client.GetInventoryBySkusAsync(new[] { "SKU-000001", "SKU-000005" });

        Assert.Equal(2, records.Count);
        Assert.Contains(records, r => r.Sku == "SKU-000001");
        Assert.Contains(records, r => r.Sku == "SKU-000005");
    }

    [Fact]
    public async Task GetPurchaseOrdersAsync_ReturnsOrdersWithLines()
    {
        using var factory = CreateMockErpFactory();
        var client = CreateClient(factory);

        var records = await client.GetPurchaseOrdersAsync(null);

        Assert.Equal(50, records.Count);
        Assert.All(records, o =>
        {
            Assert.InRange(o.Lines.Count, 1, 10);
            Assert.All(o.Lines, l => Assert.NotNull(l.Sku));
        });
    }

    [Fact]
    public async Task GetItemDetailAsync_ParsesSoapResponse()
    {
        using var factory = CreateMockErpFactory();
        var client = CreateClient(factory);

        var detail = await client.GetItemDetailAsync("SKU-000001");

        Assert.NotNull(detail);
        Assert.Equal("SKU-000001", detail.Sku);
        Assert.NotNull(detail.QuantityOnHand);
        Assert.NotNull(detail.UnitCost);
        Assert.Matches(@"^\d{14}$", detail.ModifiedDtm);
    }

    [Fact]
    public async Task GetItemDetailAsync_UnknownSku_ReturnsNull()
    {
        using var factory = CreateMockErpFactory();
        var client = CreateClient(factory);

        var detail = await client.GetItemDetailAsync("SKU-DOES-NOT-EXIST");

        Assert.Null(detail);
    }

    [Fact]
    public async Task RetryPolicy_RetriesTransient503s_ThenSucceeds()
    {
        var handler = new StubHandler();
        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"items":[{"ITM_SKU":"SKU-1","ITM_NAME":"One","ITM_QTY_ON_HAND":"5","ITM_UNIT_COST":"1.25","ITM_WHSE":"WH01","ITM_MOD_DTM":"20240101120000"}],"page":1,"pageSize":500,"totalCount":1}""", System.Text.Encoding.UTF8, "application/json"),
        });

        var client = CreateClient(handler);
        var records = await client.GetInventoryAsync(null);

        Assert.Single(records);
        Assert.Equal("SKU-1", records[0].Sku);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task CircuitBreaker_OpensAfterConsecutiveFailedCalls()
    {
        var handler = new StubHandler();
        for (var i = 0; i < 10; i++)
        {
            handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }

        var client = CreateClient(handler);

        await Assert.ThrowsAnyAsync<Exception>(() => client.GetInventoryAsync(null));
        await Assert.ThrowsAnyAsync<Exception>(() => client.GetInventoryAsync(null));
        Assert.Equal(6, handler.CallCount);

        await Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(() => client.GetInventoryAsync(null));
        Assert.Equal(6, handler.CallCount);
    }

    private static ErpHttpClient CreateClient(HttpMessageHandler handler)
    {
        var policyHandler = new PolicyHttpMessageHandler(ErpPolicies.CircuitBreakerPolicy())
        {
            InnerHandler = new PolicyHttpMessageHandler(ErpPolicies.RetryPolicy())
            {
                InnerHandler = handler,
            },
        };

        var http = new HttpClient(policyHandler)
        {
            BaseAddress = new Uri("http://erp.test/"),
        };

        return new ErpHttpClient(http);
    }

    private static ErpHttpClient CreateClient(WebApplicationFactory<MockErp.Program> factory)
    {
        return CreateClient(factory.Server.CreateHandler());
    }

    private static WebApplicationFactory<MockErp.Program> CreateMockErpFactory()
    {
        return new WebApplicationFactory<MockErp.Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Erp:InventoryCount"] = "120",
                    ["Erp:PurchaseOrderCount"] = "50",
                    ["Erp:Seed"] = "20240101",
                    ["Erp:SkuOffset"] = "0",
                    ["Erp:PoNumberOffset"] = "0",
                });
            });
        });
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();

        public int CallCount { get; private set; }

        public void Enqueue(Func<HttpResponseMessage> response)
        {
            _responses.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CallCount++;
            var response = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();
            return Task.FromResult(response());
        }
    }
}
