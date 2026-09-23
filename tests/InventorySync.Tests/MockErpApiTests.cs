using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace InventorySync.Tests;

public class MockErpApiTests
{
    private const int InventoryCount = 300;
    private const int PurchaseOrderCount = 80;

    [Fact]
    public async Task InventoryPage_ReturnsErpShapedEnvelope()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/erp/inventory?page=1&pageSize=10");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(10, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(InventoryCount, root.GetProperty("totalCount").GetInt32());

        var items = root.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(10, items.Count);

        var first = items[0];
        Assert.True(first.TryGetProperty("ITM_SKU", out _));
        Assert.True(first.TryGetProperty("ITM_QTY_ON_HAND", out var qty));
        Assert.Equal(JsonValueKind.String, qty.ValueKind);
        Assert.True(first.TryGetProperty("ITM_UNIT_COST", out var cost));
        Assert.Equal(JsonValueKind.String, cost.ValueKind);
        Assert.True(first.TryGetProperty("ITM_WHSE", out _));
        Assert.True(first.TryGetProperty("ITM_MOD_DTM", out var modified));
        Assert.Equal(JsonValueKind.String, modified.ValueKind);
        Assert.Matches(@"^\d{14}$", modified.GetString());
    }

    [Fact]
    public async Task InventoryPages_AreDistinctAndComplete()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var page1 = await client.GetFromJsonAsync<ErpPageEnvelope>("/api/erp/inventory?page=1&pageSize=200");
        var page2 = await client.GetFromJsonAsync<ErpPageEnvelope>("/api/erp/inventory?page=2&pageSize=200");

        Assert.NotNull(page1);
        Assert.NotNull(page2);
        Assert.Equal(200, page1.Items.Count);
        Assert.Equal(100, page2.Items.Count);
        Assert.Empty(page1.Items.Select(i => i.Sku).Intersect(page2.Items.Select(i => i.Sku)));
    }

    [Fact]
    public async Task InventoryPage_ModifiedSince_FiltersRecords()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var future = await client.GetFromJsonAsync<ErpPageEnvelope>("/api/erp/inventory?page=1&pageSize=10&modifiedSince=20250101000000");
        Assert.NotNull(future);
        Assert.Equal(0, future.TotalCount);
    }

    [Fact]
    public async Task InventoryPage_SkusFilter_ReturnsOnlyRequested()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<ErpPageEnvelope>("/api/erp/inventory?page=1&pageSize=100&skus=SKU-000001,SKU-000002,SKU-000003");
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCount);
        Assert.All(result.Items, i => Assert.Contains(i.Sku, new[] { "SKU-000001", "SKU-000002", "SKU-000003" }));
    }

    [Fact]
    public async Task InventoryPage_FailRate_Returns503()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/erp/inventory?page=1&pageSize=10&failRate=1");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task InventoryPage_MalformedRate_CorruptsRecords()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<ErpPageEnvelope>("/api/erp/inventory?page=1&pageSize=50&malformedRate=1");
        Assert.NotNull(result);
        Assert.Equal(50, result.Items.Count);
        Assert.All(result.Items, i =>
        {
            var missingSku = string.IsNullOrEmpty(i.Sku);
            var badQuantity = i.QuantityOnHand is not null && !int.TryParse(i.QuantityOnHand, out _);
            Assert.True(missingSku || badQuantity);
        });
    }

    [Fact]
    public async Task PurchaseOrdersPage_ReturnsErpShapedEnvelope()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/erp/purchaseorders?page=1&pageSize=5");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(PurchaseOrderCount, root.GetProperty("totalCount").GetInt32());
        var first = root.GetProperty("items").EnumerateArray().First();
        Assert.True(first.TryGetProperty("PO_NUMBER", out _));
        Assert.True(first.TryGetProperty("PO_STATUS", out var status));
        Assert.Contains(status.GetString(), new[] { "DRF", "SUB", "PAR", "REC", "CAN" });
        Assert.True(first.TryGetProperty("PO_LINES", out var lines));
        Assert.True(lines.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task SoapGetItemDetail_ReturnsXmlEnvelope()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var requestBody =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
            "<soap:Body><GetItemDetailRequest xmlns=\"urn:erp:inventory\"><Sku>SKU-000001</Sku></GetItemDetailRequest></soap:Body>" +
            "</soap:Envelope>";

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/erp/soap/GetItemDetail")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "text/xml"),
        };

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        Assert.Contains("text/xml", response.Content.Headers.ContentType!.MediaType);
        var xml = await response.Content.ReadAsStringAsync();
        Assert.Contains("GetItemDetailResponse", xml);
        Assert.Contains("<ITM_SKU>SKU-000001</ITM_SKU>", xml);
        Assert.Contains("<ITM_QTY_ON_HAND>", xml);
    }

    [Fact]
    public async Task SoapGetItemDetail_UnknownSku_ReturnsFault()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var requestBody =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
            "<soap:Body><GetItemDetailRequest xmlns=\"urn:erp:inventory\"><Sku>SKU-MISSING</Sku></GetItemDetailRequest></soap:Body>" +
            "</soap:Envelope>";

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/erp/soap/GetItemDetail")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "text/xml"),
        };

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync();
        Assert.Contains("<Fault", xml);
        Assert.Contains("not found", xml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static WebApplicationFactory<MockErp.Program> CreateFactory()
    {
        return new WebApplicationFactory<MockErp.Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Erp:InventoryCount"] = InventoryCount.ToString(),
                    ["Erp:PurchaseOrderCount"] = PurchaseOrderCount.ToString(),
                    ["Erp:Seed"] = "20240101",
                    ["Erp:SkuOffset"] = "0",
                    ["Erp:PoNumberOffset"] = "0",
                });
            });
        });
    }

    private sealed class ErpPageEnvelope
    {
        public List<ErpItemDto> Items { get; set; } = new();

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalCount { get; set; }
    }

    private sealed class ErpItemDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("ITM_SKU")]
        public string? Sku { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("ITM_QTY_ON_HAND")]
        public string? QuantityOnHand { get; set; }
    }
}
