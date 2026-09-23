-- Q2a (BASELINE): Open purchase orders — outstanding quantity per SKU.
-- Written the way legacy reports often are: a correlated EXISTS subquery that
-- is evaluated once per PurchaseOrderLines row.
SET QUOTED_IDENTIFIER ON;
SET STATISTICS IO, TIME ON;
GO

SELECT l.Sku,
       SUM(l.QuantityOrdered - l.QuantityReceived) AS OutstandingQty
FROM dbo.PurchaseOrderLines l
WHERE EXISTS (SELECT 1
              FROM dbo.PurchaseOrders po
              WHERE po.Id = l.PurchaseOrderId
                AND po.Status IN (1, 2))
GROUP BY l.Sku
HAVING SUM(l.QuantityOrdered - l.QuantityReceived) > 0
ORDER BY OutstandingQty DESC;

GO
SET STATISTICS IO, TIME OFF;
