using System.Net.Http.Json;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

using InventorySync.Core;
using InventorySync.Core.Dtos.Erp;
using InventorySync.Core.Interfaces;

using Microsoft.AspNetCore.Http;

namespace InventorySync.Infrastructure.Erp;

/// <summary>
/// HTTP implementation of the ERP client with correlation-id propagation.
/// </summary>
public class ErpHttpClient : IErpClient
{
    private const int DefaultPageSize = 500;
    private const int KeyBatchSize = 100;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    /// <summary>
    /// erp http client.
    /// </summary>
    public ErpHttpClient(HttpClient httpClient, IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets inventory records from the ERP.
    /// </summary>
    public async Task<IReadOnlyList<ErpInventoryItemRecord>> GetInventoryAsync(DateTime? modifiedSince, CancellationToken ct = default)
    {
        var results = new List<ErpInventoryItemRecord>();
        var page = 1;

        while (true)
        {
            var url = $"api/erp/inventory?page={page}&pageSize={DefaultPageSize}";
            if (modifiedSince.HasValue)
            {
                url += $"&modifiedSince={modifiedSince.Value:yyyyMMddHHmmss}";
            }

            var pageResult = await GetPageAsync<ErpInventoryItemRecord>(url, ct);
            results.AddRange(pageResult.Items);

            if (pageResult.Items.Count == 0 || results.Count >= pageResult.TotalCount)
            {
                break;
            }

            page++;
        }

        return results;
    }

    /// <summary>
    /// Gets inventory records from the ERP.
    /// </summary>
    public async Task<IReadOnlyList<ErpInventoryItemRecord>> GetInventoryBySkusAsync(IReadOnlyCollection<string> skus, CancellationToken ct = default)
    {
        var results = new List<ErpInventoryItemRecord>();

        foreach (var batch in skus.Chunk(KeyBatchSize))
        {
            var url = $"api/erp/inventory?page=1&pageSize={KeyBatchSize * 4}&skus={string.Join(',', batch)}";
            var pageResult = await GetPageAsync<ErpInventoryItemRecord>(url, ct);
            results.AddRange(pageResult.Items);
        }

        return results;
    }

    /// <summary>
    /// Gets purchase order records from the ERP.
    /// </summary>
    public async Task<IReadOnlyList<ErpPurchaseOrderRecord>> GetPurchaseOrdersAsync(DateTime? modifiedSince, CancellationToken ct = default)
    {
        var results = new List<ErpPurchaseOrderRecord>();
        var page = 1;

        while (true)
        {
            var url = $"api/erp/purchaseorders?page={page}&pageSize={DefaultPageSize}";
            if (modifiedSince.HasValue)
            {
                url += $"&modifiedSince={modifiedSince.Value:yyyyMMddHHmmss}";
            }

            var pageResult = await GetPageAsync<ErpPurchaseOrderRecord>(url, ct);
            results.AddRange(pageResult.Items);

            if (pageResult.Items.Count == 0 || results.Count >= pageResult.TotalCount)
            {
                break;
            }

            page++;
        }

        return results;
    }

    /// <summary>
    /// Gets purchase order records from the ERP.
    /// </summary>
    public async Task<IReadOnlyList<ErpPurchaseOrderRecord>> GetPurchaseOrdersByNumbersAsync(IReadOnlyCollection<string> poNumbers, CancellationToken ct = default)
    {
        var results = new List<ErpPurchaseOrderRecord>();

        foreach (var batch in poNumbers.Chunk(KeyBatchSize))
        {
            var url = $"api/erp/purchaseorders?page=1&pageSize={KeyBatchSize * 4}&poNumbers={string.Join(',', batch)}";
            var pageResult = await GetPageAsync<ErpPurchaseOrderRecord>(url, ct);
            results.AddRange(pageResult.Items);
        }

        return results;
    }

    /// <summary>
    /// Gets an item detail from the ERP SOAP endpoint.
    /// </summary>
    public async Task<ErpItemDetailRecord?> GetItemDetailAsync(string sku, CancellationToken ct = default)
    {
        var body =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
            "<soap:Body>" +
            $"<GetItemDetailRequest xmlns=\"urn:erp:inventory\"><Sku>{SecurityElement.Escape(sku)}</Sku></GetItemDetailRequest>" +
            "</soap:Body>" +
            "</soap:Envelope>";

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/erp/soap/GetItemDetail")
        {
            Content = new StringContent(body, Encoding.UTF8, "text/xml"),
        };
        AddCorrelationHeader(request);

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(ct);
        var document = XDocument.Parse(xml);

        var fault = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
        if (fault is not null)
        {
            return null;
        }

        var detail = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "ItemDetail");
        if (detail is null)
        {
            return null;
        }

        return new ErpItemDetailRecord
        {
            Sku = ReadElement(detail, "ITM_SKU"),
            Name = ReadElement(detail, "ITM_NAME"),
            Description = ReadElement(detail, "ITM_DESC"),
            QuantityOnHand = ReadElement(detail, "ITM_QTY_ON_HAND"),
            UnitCost = ReadElement(detail, "ITM_UNIT_COST"),
            WarehouseCode = ReadElement(detail, "ITM_WHSE"),
            ModifiedDtm = ReadElement(detail, "ITM_MOD_DTM"),
        };
    }

    private static string? ReadElement(XElement parent, string localName)
    {
        return parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value.Trim();
    }

    private void AddCorrelationHeader(HttpRequestMessage request)
    {
        var correlationId = _httpContextAccessor?.HttpContext?.Items[CorrelationHeaders.ItemKey] as string;

        if (!string.IsNullOrWhiteSpace(correlationId)
            && !request.Headers.Contains(CorrelationHeaders.HeaderName))
        {
            request.Headers.TryAddWithoutValidation(CorrelationHeaders.HeaderName, correlationId);
        }
    }

    private async Task<ErpPage<T>> GetPageAsync<T>(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddCorrelationHeader(request);

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var page = await response.Content.ReadFromJsonAsync<ErpPage<T>>(JsonOptions, ct);
        return page ?? new ErpPage<T>();
    }
}
