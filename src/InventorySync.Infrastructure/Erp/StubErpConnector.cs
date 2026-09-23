using System.Runtime.CompilerServices;
using InventorySync.Core.Dtos;
using InventorySync.Core.Interfaces;

namespace InventorySync.Infrastructure.Erp;

public sealed class StubErpConnector : IErpConnector
{
    public const int InventoryRecordCount = 2000;

    public const int PurchaseOrderRecordCount = 500;

    public async IAsyncEnumerable<ErpInventoryRecord> GetInventoryRecordsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var record in ErpDataGenerator.GenerateInventory(InventoryRecordCount, ErpDataGenerator.DefaultSeed))
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return record;
        }
    }

    public async IAsyncEnumerable<ErpPurchaseOrderRecord> GetPurchaseOrderRecordsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var record in ErpDataGenerator.GeneratePurchaseOrders(
                     PurchaseOrderRecordCount,
                     ErpDataGenerator.DefaultSeed,
                     InventoryRecordCount))
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return record;
        }
    }
}
