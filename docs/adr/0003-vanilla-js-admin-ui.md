# ADR 0003: Vanilla JavaScript for the operations console, not a SPA framework

- **Status:** accepted
- **Date:** 2026-09-23
- **Deciders:** engineering team

## Context

The service needs an operations console: inventory editing, purchase-order browsing, sync triggering with live polling, and an audit-entry explorer. The team considered two approaches:

1. **A SPA framework** (React/Vue/Angular + bundler): component ecosystems, tooling, and hiring familiarity, at the cost of a Node toolchain, a build step, and framework churn.
2. **Vanilla JavaScript** (ES6 modules, HTML5, CSS custom properties): no build, no dependencies, served directly from `wwwroot`.

## Decision

Use **vanilla ES6+ modules with no framework and no build step**.

## Consequences

**Positive**

- **Zero toolchain:** the UI is 15 small modules served as-is by `UseStaticFiles`; there is no `node_modules`, no bundler, and no version-locked framework to migrate every few years.
- **Fast onboarding:** a reviewer can read `js/api.js`, `js/router.js`, and the four view modules in minutes; the rendering model (DOM nodes, `textContent` everywhere) is explicit.
- **Long-term stability:** the interface contract (hash routes + typed API client) is stable; the only "dependency" is the browser platform itself.
- **Linting without a build:** ESLint flat config with `eslint:recommended` runs directly over the sources in CI.

**Negative**

- We own the boilerplate a framework would give us (listeners, re-rendering, dialogs) — mitigated by the shared components (`DataTable`, `Pager`, `FilterBar`, `Toast`, `ConfirmDialog`, `SyncRunTimeline`).
- No declarative state management; views manage their own state and clean up listeners in `destroy()`. This is a deliberate simplicity trade for a console of this size.
- Contributors used to JSX will need a moment to adjust to `document.createElement` and `el()` helpers.

If the UI ever grows beyond a single operations console (multi-tenant dashboards, offline support, i18n at scale), this decision should be revisited; the API surface would carry over unchanged.
