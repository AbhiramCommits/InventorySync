using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using MockErp.Erp;
using MockErp.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MockErpOptions>(builder.Configuration.GetSection(MockErpOptions.SectionName));
builder.Services.Configure<FaultOptions>(builder.Configuration.GetSection(FaultOptions.SectionName));
builder.Services.AddSingleton<ErpStore>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/erp/inventory", (
        HttpContext context,
        ErpStore store,
        IOptions<FaultOptions> faultOptions,
        int page = 1,
        int pageSize = 100,
        string? modifiedSince = null,
        string? skus = null,
        double? failRate = null,
        double? malformedRate = null) =>
    HandlePageRequest(
        context,
        store,
        faultOptions,
        page,
        pageSize,
        modifiedSince,
        skus,
        failRate,
        malformedRate,
        isOrders: false));

app.MapGet("/api/erp/purchaseorders", (
        HttpContext context,
        ErpStore store,
        IOptions<FaultOptions> faultOptions,
        int page = 1,
        int pageSize = 100,
        string? modifiedSince = null,
        string? poNumbers = null,
        double? failRate = null,
        double? malformedRate = null) =>
    HandlePageRequest(
        context,
        store,
        faultOptions,
        page,
        pageSize,
        modifiedSince,
        poNumbers,
        failRate,
        malformedRate,
        isOrders: true));

app.MapPost("/api/erp/soap/GetItemDetail", HandleGetItemDetailAsync);

app.Run();

static IResult HandlePageRequest(
    HttpContext context,
    ErpStore store,
    IOptions<FaultOptions> faultOptions,
    int page,
    int pageSize,
    string? modifiedSince,
    string? keys,
    double? failRate,
    double? malformedRate,
    bool isOrders)
{
    var fail = failRate ?? faultOptions.Value.DefaultFailRate;
    var malformed = malformedRate ?? faultOptions.Value.DefaultMalformedRate;

    if (FaultInjector.ShouldFailPage(Math.Max(1, page), fail))
    {
        context.Response.Headers.RetryAfter = "2";
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    page = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 1, 1000);

    DateTime? modifiedSinceUtc = null;
    if (!string.IsNullOrWhiteSpace(modifiedSince))
    {
        if (ErpDateFormats.TryParse(modifiedSince, out var parsed))
        {
            modifiedSinceUtc = parsed;
        }
        else
        {
            return Results.BadRequest(new { error = "modifiedSince must use the yyyyMMddHHmmss format." });
        }
    }

    var keyFilter = string.IsNullOrWhiteSpace(keys)
        ? null
        : keys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    if (isOrders)
    {
        var filtered = store.GetOrders(modifiedSinceUtc, keyFilter);
        var slice = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var items = slice
            .Select((order, index) => FaultInjector.CorruptOrder(order, malformed, page, index))
            .ToList();

        return Results.Ok(new PageEnvelope<ErpOrderRecord>(items, page, pageSize, filtered.Count));
    }
    else
    {
        var filtered = store.GetItems(modifiedSinceUtc, keyFilter);
        var slice = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var items = slice
            .Select((item, index) => FaultInjector.CorruptItem(item, malformed, page, index))
            .ToList();

        return Results.Ok(new PageEnvelope<ErpItemRecord>(items, page, pageSize, filtered.Count));
    }
}

static async Task<IResult> HandleGetItemDetailAsync(HttpContext context, ErpStore store)
{
    string body;
    using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
    {
        body = await reader.ReadToEndAsync(context.RequestAborted);
    }

    context.Response.ContentType = "text/xml; charset=utf-8";

    string? sku = null;
    try
    {
        var document = XDocument.Parse(body);
        sku = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Sku")?.Value.Trim();
    }
    catch (XmlException)
    {
    }

    if (string.IsNullOrEmpty(sku) || store.FindItem(sku) is not { } item)
    {
        return Results.Text(SoapFaultEnvelope($"Item '{sku}' was not found."), "text/xml", Encoding.UTF8);
    }

    return Results.Text(ItemDetailEnvelope(item), "text/xml", Encoding.UTF8);
}

static string SoapFaultEnvelope(string message)
{
    XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";

    var document = new XDocument(
        new XElement(soap + "Envelope",
            new XElement(soap + "Body",
                new XElement(soap + "Fault",
                    new XElement("faultcode", "soap:Client"),
                    new XElement("faultstring", message)))));

    return document.Declaration is null
        ? document.ToString(SaveOptions.DisableFormatting)
        : document.Declaration + document.ToString(SaveOptions.DisableFormatting);
}

static string ItemDetailEnvelope(ErpItemRecord item)
{
    XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
    XNamespace erp = "urn:erp:inventory";

    var document = new XDocument(
        new XDeclaration("1.0", "utf-8", null),
        new XElement(soap + "Envelope",
            new XElement(soap + "Body",
                new XElement(erp + "GetItemDetailResponse",
                    new XElement(erp + "ItemDetail",
                        new XElement(erp + "ITM_SKU", item.Sku),
                        new XElement(erp + "ITM_NAME", item.Name),
                        item.Description is null ? null : new XElement(erp + "ITM_DESC", item.Description),
                        new XElement(erp + "ITM_QTY_ON_HAND", item.QuantityOnHand),
                        new XElement(erp + "ITM_UNIT_COST", item.UnitCost),
                        new XElement(erp + "ITM_WHSE", item.WarehouseCode),
                        new XElement(erp + "ITM_MOD_DTM", item.ModifiedDtm))))));

    return document.ToString(SaveOptions.DisableFormatting);
}

namespace MockErp
{
    public partial class Program
    {
    }
}
