-- Q3b (TUNED): Sync failure counts by day.
-- SARGable half-open range on the raw column, served by
-- IX_SyncAuditEntries_Action_TimestampUtc (Action, TimestampUtc) INCLUDE (SyncRunId).
SET QUOTED_IDENTIFIER ON;
SET STATISTICS IO, TIME ON;
GO

SELECT CAST(TimestampUtc AS date) AS FailureDate,
       COUNT_BIG(*) AS ErrorCount
FROM dbo.SyncAuditEntries
WHERE Action = 4
  AND TimestampUtc >= '2026-01-01T00:00:00'
  AND TimestampUtc <  '2027-01-01T00:00:00'
GROUP BY CAST(TimestampUtc AS date)
ORDER BY FailureDate;

GO
SET STATISTICS IO, TIME OFF;
