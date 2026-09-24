# Contributing to InventorySync

Thanks for contributing. This file covers the workflow so that a PR is reviewable in minutes.

## Branches

- `main` is the only long-lived branch and is always releasable.
- Work in a short-lived feature branch named with a type prefix and a slug:
  - `feat/<slug>` — new functionality
  - `fix/<slug>` — bug fixes
  - `docs/<slug>` — documentation only
  - `perf/<slug>` — performance work
  - `ci/<slug>` — build/test pipeline changes
  - Example: `fix/sync-retry-child-runs`

## Commits

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
feat(sync): add retry endpoint for failed records
fix(mapper): reject thousands separators in decimal parsing
docs(sql): record baseline measurements for reporting queries
```

Types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`.
Keep the body focused on *why*.

## Pull request checklist

Before opening a PR, confirm:

- [ ] `dotnet build InventorySync.sln` succeeds with zero warnings (`TreatWarningsAsErrors` is on)
- [ ] `dotnet test InventorySync.sln` passes locally (needs Docker for the Testcontainers suite)
- [ ] `dotnet format InventorySync.sln --verify-no-changes` is clean
- [ ] `npm ci && npm run lint` is clean (admin UI JavaScript)
- [ ] New public members in `Core` and `Infrastructure` have XML doc comments (CS1591 is an error there)
- [ ] New behavior is covered by tests; Core+Infrastructure line coverage stays ≥ 70% (`.github/scripts/check-coverage.py`)
- [ ] API surface changes are reflected in the README reference table
- [ ] No secrets are committed (connection strings go in user-secrets or environment variables)

## Running the test suite locally

```bash
# Unit + integration tests (the Testcontainers suite needs Docker running)
docker compose up -d            # not required for tests, but handy for smoke runs
dotnet test InventorySync.sln

# With coverage
dotnet test InventorySync.sln --collect:"XPlat Code Coverage" --results-directory coverage
python3 .github/scripts/check-coverage.py coverage

# Formatting and linting (the CI lint job runs exactly these)
dotnet format InventorySync.sln --verify-no-changes
npm ci
npm run lint
```

The Testcontainers tests spin up a real SQL Server container (`mcr.microsoft.com/mssql/server:2022-latest`).
On machines where Docker is unavailable they are skipped by CI configuration — but CI runs them, so keep them green.

## Design conventions

- Layering: `Api` → controllers only; `Core` → entities, DTOs, interfaces; `Infrastructure` → EF Core, repositories, services, ERP client. Keep `Core` free of persistence and ASP.NET dependencies (only `CorrelationHeaders` constants are shared).
- Sync engine: never abort a run for a single bad record — isolate, audit, and continue.
- UI: no inline event handlers, no frameworks, escape every interpolated value (`escapeHtml` / `textContent`), and always clean up listeners in `destroy()`.
- Errors: throw `EntityNotFoundException` (404) / `ConflictException` (409) / `InvalidOperationException` (400); the middleware renders ProblemDetails.

## Release process

1. Merge to `main` and confirm the CI workflow is green.
2. Tag with an annotated tag: `git tag -a v<semver> -m "Release v<semver>"` and push the tag.
