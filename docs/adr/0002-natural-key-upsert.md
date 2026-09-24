# ADR 0002: Match ERP records by natural key, not by ERP record id

- **Status:** accepted
- **Date:** 2026-09-23
- **Deciders:** engineering team

## Context

The sync engine has to decide whether an incoming ERP record is new or a change to an existing local row. Two matching strategies were considered:

1. **ERP record id** (`ErpRecordId`): match on the id the ERP assigns. Requires the ERP to be the system of record for identity from day one.
2. **Natural key** (`Sku` for inventory, `PoNumber` for purchase orders): match on the business key both systems already share.

## Decision

Match by **natural key**. `ErpRecordId` is stored on each row but is never used for matching.

## Consequences

**Positive**

- **Works on first sync:** before the first sync the local database has no ERP ids at all (rows may have been seeded or created locally), so id-matching would insert duplicates instead of updating.
- **Survives ERP data migrations:** legacy ERPs renumber records, re-import data, or change id formats. The natural key is stable across all of that.
- **Idempotent and retry-friendly:** re-pulling a record always lands on the same row, which makes the retry endpoint (`RetryFailedRecordsAsync`) a simple "re-pull these keys" operation.
- **Local creation is supported:** an operator can create a PO locally, and the first ERP record with the same `PoNumber` upgrades it rather than duplicating it.

**Negative**

- Natural keys must be treated as immutable; we never expose SKU/PO renames.
- Duplicate natural keys in the ERP (two lines with the same SKU, say) surface as data problems we have to handle explicitly.
- We carry `ErpRecordId` as metadata that can drift if the ERP renumbers — acceptable, because it is diagnostic only.
