-- Q1: Low-stock items by warehouse (reporting threshold: < 25 units)
-- Tuned by filtered index IX_InventoryItems_LowStock (WarehouseCode, QuantityOnHand)
-- WHERE QuantityOnHand < 25.
SET QUOTED_IDENTIFIER ON;
SET STATISTICS IO, TIME ON;
GO

SELECT WarehouseCode,
       COUNT_BIG(*) AS LowStockCount,
       SUM(QuantityOnHand) AS TotalOnHand
FROM dbo.InventoryItems
WHERE QuantityOnHand < 25
GROUP BY WarehouseCode
ORDER BY LowStockCount DESC;

GO
SET STATISTICS IO, TIME OFF;
