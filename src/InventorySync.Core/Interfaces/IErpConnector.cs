using InventorySync.Core.Dtos;

namespace InventorySync.Core.Interfaces;

public interface IErpConnector
{
    IAsyncEnumerable<ErpInventoryRecord> GetInventoryRecordsAsync(CancellationToken ct = default);

    IAsyncEnumerable<ErpPurchaseOrderRecord> GetPurchaseOrderRecordsAsync(CancellationToken ct = default);
}
