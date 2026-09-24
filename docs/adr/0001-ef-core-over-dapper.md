# ADR 0001: Use Entity Framework Core instead of Dapper

- **Status:** accepted
- **Date:** 2026-09-23
- **Deciders:** engineering team

## Context

The service must persist a small, well-understood domain model (inventory items, purchase orders, lines, sync runs, audit entries) to SQL Server, keep the schema under version control, and evolve it safely as the integration surface grows. Two candidates were considered:

- **Dapper** — hand-written SQL, minimal abstraction, maximum control over query shape.
- **EF Core** — LINQ queries, change tracking, migrations, provider-agnostic model.

## Decision

Use **Entity Framework Core 8** with a code-first model and fluent configurations.

## Consequences

**Positive**

- **Schema as code:** `dotnet ef migrations` gives us reviewable, repeatable schema changes (e.g. `ReportingIndexes`, `LocalModificationTrackingAndRetryRuns`). Dapper would require hand-maintained SQL scripts.
- **Compile-time safety:** repository queries are LINQ, so renames and type changes break the build instead of failing at runtime in raw SQL strings.
- **Concurrency primitives for free:** `IsRowVersion()` gives optimistic-concurrency `WHERE RowVersion = @p` clauses without hand-writing them everywhere.
- **Change tracking** makes the sync engine simpler: load a batch, mutate properties, and `SaveChanges` diffs exactly what changed — which is what the field-level audit trail depends on.
- **Testability:** the same model runs on InMemory for fast unit tests and on real SQL Server for Testcontainers integration tests.

**Negative**

- Less control over exact SQL; hot paths (bulk seeding) need batching and `AutoDetectChangesEnabled = false` to stay fast.
- A steeper learning curve for contributors unfamiliar with the change tracker.

The one place raw SQL is unavoidable — the reporting workload — is deliberately kept out of the application layer and lives in `docs/queries/` where execution plans can be reviewed.
