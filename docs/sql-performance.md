# SQL Performance Study: InventorySync reporting workload

This study measures four realistic reporting queries against the seeded dataset,
captures baseline `SET STATISTICS IO, TIME ON` output and execution plans, then
tunes them with a new index set, a query rewrite, a SARGability fix and an EF
Core `.Include` change. All changes ship as EF Core migration
`ReportingIndexes` plus the `includeLines` API parameter.

## Dataset and environment

| Object            | Rows    |
| ----------------- | ------- |
| `InventoryItems`  | 52,000  |
| `PurchaseOrders`  | 5,500   |
| `PurchaseOrderLines` | 30,471 |
| `SyncAuditEntries`   | 168,425 |

Environment: SQL Server 2022 (`mcr.microsoft.com/mssql/server:2022-latest`)
running under Docker. The numbers in the tables below are the median of three
runs.

Logical reads are deterministic and are the primary metric. Elapsed times are
reported as well, but on this machine the SQL Server container runs under CPU
emulation (Apple silicon), so elapsed times are noisy; treat them as indicative
rather than exact.

## Reproducing the numbers

```bash
# 1. Start the stack and seed the dataset
docker compose up -d
docker compose exec api dotnet InventorySync.Api.dll seed

# 2. Apply the index migration (this also happens automatically when the API starts)
dotnet ef database update --project src/InventorySync.Infrastructure --startup-project src/InventorySync.Api

# 3. Run the queries with STATISTICS IO, TIME ON
docs/queries/measure.sh                       # all queries
docs/queries/measure.sh docs/queries/01-low-stock-by-warehouse.sql   # one query
```

Each query file turns `STATISTICS IO, TIME ON` around a single statement, and
also issues `SET QUOTED_IDENTIFIER ON`. That last line matters more than it
looks: **filtered indexes are only considered by the query optimizer when
`QUOTED_IDENTIFIER` is ON for the session** (the .NET SqlClient provider and EF
Core set it by default, raw `sqlcmd` sessions do not). Omitting it silently
reverts the queries to full scans.

The queries are the tuning targets; the baseline numbers were captured before
migration `ReportingIndexes` was applied, the tuned numbers after.

---

## Query 1 — Low-stock items by warehouse

`docs/queries/01-low-stock-by-warehouse.sql`

```sql
SELECT WarehouseCode, COUNT_BIG(*) AS LowStockCount, SUM(QuantityOnHand) AS TotalOnHand
FROM dbo.InventoryItems
WHERE QuantityOnHand < 25
GROUP BY WarehouseCode
ORDER BY LowStockCount DESC;
```

**Baseline plan:** `Clustered Index Scan` over `PK_InventoryItems` with a
residual predicate — the whole 52,000-row table is scanned, then sorted and
aggregated.

**Tuning:** a filtered index over just the rows the report cares about.

```csharp
builder.HasIndex(x => new { x.WarehouseCode, x.QuantityOnHand })
    .HasDatabaseName("IX_InventoryItems_LowStock")
    .HasFilter("QuantityOnHand < 25");
```

**Tuned plan:** `Index Scan` over `IX_InventoryItems_LowStock` — the scan
touches only the low-stock subset of the table.

| Metric | Before | After | Improvement |
| ------ | ------ | ----- | ----------- |
| Logical reads | 1,473 | 3 | **99.8% fewer** |
| Elapsed (median) | 53 ms | ~1 ms | ~98% faster |

**Why it works:** the filter predicate is a constant comparison
(`QuantityOnHand < 25`), which SQL Server allows in a filtered index. The index
contains only rows that can satisfy the predicate, so the aggregate and the
`GROUP BY` stream over a few hundred rows instead of 52,000. The same query
with the covering index only (`IX_InventoryItems_Whse_Valuation`, no filter)
drops reads to 232; the filtered index wins because it physically excludes
irrelevant rows rather than merely covering them.

---

## Query 2 — Open purchase orders: outstanding quantity per SKU

Baseline shape: `docs/queries/02a-open-po-outstanding-baseline.sql`
Tuned shape: `docs/queries/02b-open-po-outstanding-tuned.sql`

The legacy report answers "how much is still outstanding per SKU on open
orders" with a correlated `EXISTS` subquery evaluated per line:

```sql
SELECT l.Sku, SUM(l.QuantityOrdered - l.QuantityReceived) AS OutstandingQty
FROM dbo.PurchaseOrderLines l
WHERE EXISTS (SELECT 1 FROM dbo.PurchaseOrders po
              WHERE po.Id = l.PurchaseOrderId AND po.Status IN (1, 2))
GROUP BY l.Sku
HAVING SUM(l.QuantityOrdered - l.QuantityReceived) > 0
ORDER BY OutstandingQty DESC;
```

The tuned version replaces the correlated lookup with a single join and adds a
`RANK()` window function over the aggregate, which the legacy shape cannot
express at all:

```sql
SELECT Sku, OpenLineCount, OutstandingQty,
       RANK() OVER (ORDER BY OutstandingQty DESC) AS OutstandingRank
FROM (SELECT l.Sku,
             COUNT_BIG(*) AS OpenLineCount,
             SUM(l.QuantityOrdered - l.QuantityReceived) AS OutstandingQty
      FROM dbo.PurchaseOrderLines l
      INNER JOIN dbo.PurchaseOrders po ON po.Id = l.PurchaseOrderId
      WHERE po.Status IN (1, 2)
        AND l.QuantityReceived < l.QuantityOrdered
      GROUP BY l.Sku) AS openLines
ORDER BY OutstandingQty DESC;
```

**Tuning:** two indexes.

```csharp
// PurchaseOrders: filtered index over open statuses only
builder.HasIndex(x => x.Status)
    .HasDatabaseName("IX_PurchaseOrders_OpenByStatus")
    .HasFilter("Status IN (1, 2)")
    .IncludeProperties(x => new { x.Id, x.PoNumber });

// PurchaseOrderLines: covering index for SKU-level aggregation
builder.HasIndex(x => x.Sku)
    .HasDatabaseName("IX_PurchaseOrderLines_Sku_Open")
    .IncludeProperties(x => new { x.QuantityOrdered, x.QuantityReceived });
```

| Metric | 02a before | 02a after | 02b before | 02b after |
| ------ | ---------- | --------- | ---------- | --------- |
| Logical reads (POs) | 97 | 13 | 97 | 13 |
| Logical reads (lines) | 283 | 283 | 283 | 283 |
| Total logical reads | 380 | 296 | 380 | 296 |
| Elapsed (median) | 136 ms | 33 ms | 113 ms | ~60–90 ms (noisy) |

**Why the PO side improved 87%:** `Status IN (1, 2)` is a constant predicate,
so the filtered index `IX_PurchaseOrders_OpenByStatus` applies, and both the
legacy and tuned shapes now `Index Seek` into the ~2,000 open POs instead of
scanning all 5,500. The rewrite removes the per-row correlated evaluation and
lets the planner pick a single hash join.

**Why the lines side plateaued (and why that is expected):** we *wanted* a
filtered index on the open lines — `WHERE QuantityReceived < QuantityOrdered` —
but SQL Server rejects two-column comparison predicates on filtered indexes:

```text
Msg 10735: Incorrect WHERE clause for filtered index '...' on table '...'.
```

(reproducible with any `WHERE colA < colB` predicate; constant-comparison
predicates work fine). The covering index is the next-best option, but the
aggregation over *open lines per SKU* still has to read every open line, and at
30k rows the optimizer prefers the clustered scan because the covering index
cannot also supply `PurchaseOrderId` for the join. The remaining reads are
bounded by the size of the lines table, not by the number of orders. The honest
takeaway for this query: the join rewrite and the filtered PO index do the
heavy lifting; the ranking capability of the window function is the bonus the
legacy query cannot provide.

---

## Query 3 — Sync failure counts by day

Baseline: `docs/queries/03a-sync-failures-baseline.sql`
Tuned: `docs/queries/03b-sync-failures-tuned.sql`

The legacy query wraps the timestamp column in `CONVERT(date, ...)` and compares
the *result*:

```sql
WHERE Action = 4 AND CONVERT(date, TimestampUtc) >= '2026-01-01'
```

`CONVERT(date, TimestampUtc)` is a function around an indexed column, so the
range predicate is **not SARGable**: the planner cannot seek into any index on
`TimestampUtc` and must scan the whole `SyncAuditEntries` table (168k rows),
evaluating `CONVERT` on every row.

The tuned query compares the raw column with a half-open range:

```sql
WHERE Action = 4
  AND TimestampUtc >= '2026-01-01T00:00:00'
  AND TimestampUtc <  '2027-01-01T00:00:00'
```

**Tuning:** a supporting index for the error-audit reporting shape.

```csharp
builder.HasIndex(x => new { x.Action, x.TimestampUtc })
    .HasDatabaseName("IX_SyncAuditEntries_Action_TimestampUtc")
    .IncludeProperties(x => new { x.SyncRunId });
```

**Plans:** baseline is a full `Clustered Index Scan` with the computed
predicate in the residual filter. After the index, both shapes are `Index Seek
(Action = 4, TimestampUtc range)` — 8 logical reads instead of 2,636.

| Metric | 03a before | 03a after | 03b before | 03b after |
| ------ | ---------- | --------- | ---------- | --------- |
| Logical reads | 2,636 | 8 | 2,636 | 8 |
| Elapsed (median) | 166 ms | ~2 ms | 30 ms | ~1 ms |

**Why it works:** with the composite index the seek lands directly on the
`Action = 4` slice in timestamp order. The SARGable half-open range is the
recommended shape even after indexing: it avoids the per-row `CONVERT` entirely,
which is why 03b keeps a small CPU edge over 03a. (Note how 03a also collapses
to 8 reads once the index exists — the residual `CONVERT` predicate is applied
only to the small seeked slice, which is why the absolute difference between
the two shapes is tiny at this data size. On a larger table the difference
between scanning the error slice and seeking into it grows, and the half-open
range is what keeps the predicate seekable.)

---

## Query 4 — Inventory valuation by warehouse

`docs/queries/04-inventory-valuation-by-warehouse.sql`

```sql
SELECT WarehouseCode, SUM(QuantityOnHand) AS TotalUnits,
       SUM(QuantityOnHand * UnitCost) AS Valuation
FROM dbo.InventoryItems
GROUP BY WarehouseCode
ORDER BY Valuation DESC;
```

**Baseline plan:** full `Clustered Index Scan` over 52,000 rows (1,473 reads).

**Tuning:** a covering index that turns the scan into a narrow, index-only scan.

```csharp
builder.HasIndex(x => x.WarehouseCode)
    .HasDatabaseName("IX_InventoryItems_Whse_Valuation")
    .IncludeProperties(x => new { x.QuantityOnHand, x.UnitCost });
```

**Tuned plan:** `Index Scan` over `IX_InventoryItems_Whse_Valuation` with no
key lookups.

| Metric | Before | After | Improvement |
| ------ | ------ | ----- | ----------- |
| Logical reads | 1,473 | 232 | **84.2% fewer** |
| Elapsed (median) | 65 ms | ~23 ms | ~65% faster |

**Why it works:** every column the query needs is either the index key or an
`INCLUDE`d column, so the engine reads ~6x fewer pages and never touches the
clustered index. A covering index is the right tool here because there is no
selective predicate to exploit — this report legitimately aggregates the whole
table — so the only win available is making the unavoidable scan as narrow as
possible.

---

## EF Core N+1: order lines loaded one-by-one

The orders list endpoint originally returned purchase-order headers only, so
consumers that wanted lines had to issue one extra query per order — the
classic N+1. The fix exposes `includeLines=true` on `GET /api/purchaseorders`,
backed by a single `.Include(...)` in the repository:

```csharp
if (query.IncludeLines)
{
    filtered = filtered.Include(x => x.Lines);
}
```

The round-trip count is asserted in
`SqlServerIntegrationTests.NPlusOne_IncludePattern_ReducesRoundTrips`, which
counts `RelationalEventId.CommandExecuted` events with `DbContext.LogTo`:

| Pattern | Round trips for 20 orders |
| ------- | ------------------------- |
| N+1 (headers, then one query per order) | **21** |
| `.Include(o => o.Lines)` projection | **1** |

Both patterns return identical data; the Include version does it in one query
(`LEFT JOIN` on the lines table). The API also supports projection-friendly
paging through the existing `PurchaseOrderQuery`.

---

## Results at a glance

| Query | Technique | Logical reads | Δ | Elapsed (median) | Δ |
| ----- | --------- | ------------- | - | ---------------- | - |
| 1. Low-stock by warehouse | filtered index (`QuantityOnHand < 25`) | 1,473 → 3 | −99.8% | 53 ms → ~1 ms | −98% |
| 2a. Open POs, legacy correlated subquery | filtered index on status (read drop on PO side) | 380 → 296 | −22% | 136 ms → ~33 ms | −76% |
| 2b. Open POs, join + `RANK()` window | rewrite + filtered index | 380 → 296 | −22% | 113 ms → ~60–90 ms | noisy |
| 3. Sync failures by day | SARGable half-open range + `(Action, TimestampUtc)` index | 2,636 → 8 | −99.7% | 30–166 ms → ~1–2 ms | −97% |
| 4. Valuation by warehouse | covering index | 1,473 → 232 | −84.2% | 65 ms → ~23 ms | −65% |
| EF Core order lines | `.Include` instead of N+1 | 21 queries → 1 query | −95% | — | — |

## Notes and caveats

- **Two-column predicates are rejected on filtered indexes** (error 10735).
  Keep filtered-index predicates to constant comparisons; this is why the open
  lines index is a plain covering index.
- **`QUOTED_IDENTIFIER ON` is required** for the optimizer to consider filtered
  indexes. .NET clients set it by default; raw `sqlcmd` does not — the query
  files set it explicitly so measurements are reproducible.
- Elapsed times were measured under CPU emulation and vary between runs;
  logical reads are stable across runs and should be treated as the primary
  metric.
- The index changes are versioned in migration `ReportingIndexes` and the
  `includeLines` parameter is covered by the Testcontainers integration suite,
  so the tuning is part of the normal CI pipeline rather than a one-off
  database tweak.
