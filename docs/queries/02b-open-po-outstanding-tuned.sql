-- Q2b (TUNED): Open purchase orders — outstanding quantity per SKU.
-- Correlated subquery replaced by a single JOIN, with a RANK() window function
-- over the aggregated result. Uses IX_PurchaseOrders_OpenByStatus (filtered)
-- and IX_PurchaseOrderLines_Sku_Open (covering).
SET QUOTED_IDENTIFIER ON;
SET STATISTICS IO, TIME ON;
GO

SELECT Sku,
       OpenLineCount,
       OutstandingQty,
       RANK() OVER (ORDER BY OutstandingQty DESC) AS OutstandingRank
FROM (
    SELECT l.Sku,
           COUNT_BIG(*) AS OpenLineCount,
           SUM(l.QuantityOrdered - l.QuantityReceived) AS OutstandingQty
    FROM dbo.PurchaseOrderLines l
    INNER JOIN dbo.PurchaseOrders po ON po.Id = l.PurchaseOrderId
    WHERE po.Status IN (1, 2)
      AND l.QuantityReceived < l.QuantityOrdered
    GROUP BY l.Sku
) AS openLines
ORDER BY OutstandingQty DESC;

GO
SET STATISTICS IO, TIME OFF;
