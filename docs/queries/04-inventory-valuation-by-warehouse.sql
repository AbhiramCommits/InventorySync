-- Q4: Inventory valuation by warehouse.
-- The same statement before and after tuning; the improvement comes from the
-- covering index IX_InventoryItems_Whse_Valuation
-- (WarehouseCode) INCLUDE (QuantityOnHand, UnitCost), which turns the scan into
-- a narrow index-only scan.
SET QUOTED_IDENTIFIER ON;
SET STATISTICS IO, TIME ON;
GO

SELECT WarehouseCode,
       SUM(QuantityOnHand) AS TotalUnits,
       SUM(QuantityOnHand * UnitCost) AS Valuation
FROM dbo.InventoryItems
GROUP BY WarehouseCode
ORDER BY Valuation DESC;

GO
SET STATISTICS IO, TIME OFF;
