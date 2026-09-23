-- Q3a (BASELINE): Sync failure counts by day.
-- CONVERT(date, TimestampUtc) wraps the indexed column in a function, so the
-- range predicate is not SARGable and the planner scans the whole table.
SET QUOTED_IDENTIFIER ON;
SET STATISTICS IO, TIME ON;
GO

SELECT CONVERT(date, TimestampUtc) AS FailureDate,
       COUNT_BIG(*) AS ErrorCount
FROM dbo.SyncAuditEntries
WHERE Action = 4
  AND CONVERT(date, TimestampUtc) >= '2026-01-01'
GROUP BY CONVERT(date, TimestampUtc)
ORDER BY FailureDate;

GO
SET STATISTICS IO, TIME OFF;
