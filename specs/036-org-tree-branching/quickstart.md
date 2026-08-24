# Quickstart & Validation Guide: Organization Tree Branching

**Feature**: `specs/036-org-tree-branching` | **Date**: 2026-08-24

Runbook for validating the feature end-to-end. Implementation details belong in `tasks.md`;
this is the **verify-it-works** guide. UI structure assertions reference
[contracts/organization-tree-ui.md](./contracts/organization-tree-ui.md) (rules C-01…C-12);
data shapes reference [data-model.md](./data-model.md).

## 1. Prerequisites

- Inside `.devcontainer` (Constitution V); `docker compose up` brings up `mssql` + `valkey`.
- .NET 10 SDK per `global.json`.
- Playwright installed: `cd tests/Playwright.Tests && npm ci && npx playwright install` (skip if already installed).
- App credentials (persistent dev DB `LearningLms`):
  - SuperUser: `admin@librelms.local` / `Admin@12345`
  - OrgAdmin: `admin@example.com` / `Admin@12345`

## 2. Build & run the app

```bash
cd /workspace
./scripts/restart-app.sh --background
```

Verify (gate 1 of Constitution XIII — show evidence, don't assume):
- Script prints `Build succeeded`.
- `/tmp/lms-host.log` contains `Now listening on: ... http://localhost:5000`.
- `curl -I http://localhost:5000/` responds 200/302.

> Razor views are precompiled — a restart is mandatory after any `.cshtml`/CSS change (see the
> `restart-host-app` skill for the explicit-kill pitfall: `pkill -f 'bin/Debug/net10.0/Host'`
> first, confirm ports 5000/7095 are free).

## 3. Seed the standard test hierarchy

The standard acceptance hierarchy (spec US1/US2): **Root Organization → Finance, Sales;
Finance → Billing**. The DB is persistent (seeders only run on an empty DB), so create it via the
UI on first validation:

1. Log in as SuperUser at `http://localhost:5000/Account/Login`.
2. Open **Admin → Organizations → Create Organization**: create **Finance** (parent: Root
   Organization) and **Sales** (parent: Root Organization).
3. Create **Billing** (parent: Finance).
4. *Optional, for C-08*: disable a node via the Org Chart context menu (spec 013) to exercise the
   disabled-subtree treatment; re-enable afterwards.

## 4. Automated validation (gate 2 — E2E)

```bash
cd tests/Playwright.Tests
npx playwright test tests/06-admin-organizations.spec.ts
```

Expected: all tests pass, including the extended tree assertions:

- Root renders once, as the only top-level node, with `Root` badge (C-01, C-07).
- Billing's node is a DOM descendant of Finance's, not of Sales' (C-02/C-03) — the user's exact
  reported scenario.
- Finance and Sales share one parent `<ul>` (C-04).
- Non-root nodes have CSS connector lines; root does not (C-06).
- Disabled node + descendants carry the disabled treatment (C-08, if seeded).
- At 375px viewport: `scrollWidth <= clientWidth` on the tree container (C-11).
- Pre-existing tests (root org visible, create form reachable) still pass — no regression (B-03, C-01).

Full-suite regression (same command without the file filter) must also pass before claiming done.

## 5. Manual walkthrough (maps to spec acceptance scenarios)

With the standard hierarchy seeded, open **Admin → Organizations** and check:

| # | Check | Spec ref |
|---|-------|----------|
| M1 | Root row is visually strongest (badge + styling); Finance/Sales one indent deeper; Billing one deeper than Finance | US1-A1, FR-002, FR-005 |
| M2 | Tracing Billing's connector line leads to Finance only; Finance/Sales lines lead to Root | US1-A2, US2-A2, FR-003 |
| M3 | Finance and Sales visibly grouped as root's children (same indent, same branch) | US2-A1, FR-004 |
| M4 | No dangling lines under leaf nodes (Finance has a child; Sales/Billing don't) | US1-A4, C-05 |
| M5 | Edit on any row opens the existing edit screen unchanged | US3-A1, FR-006 |
| M6 | Create Organization → Org Chart View links behave as before | US3-A4, FR-011 |
| M7 | Colors/fonts/radii match the Organic system (compare against Dashboard styling) | FR-008, SC-005 |
| M8 | Browser dev-tools viewport at 375px: no horizontal scroll, all names readable | US4-A1, FR-009 |
| M9 | (Optional deep tree) seed 7 levels: still no horizontal scroll, deepest node traceable | US4-A1, FR-010 |

**Success bar** (spec Success Criteria): an admin can name any node's parent in < 5 s from this
page alone (SC-001), and 100% of a relationship-identification trial on the standard hierarchy is
answered correctly (SC-002).

## 6. Post-merge regression (gate 3 — Constitution XIII)

After `story/036-org-tree-branching` merges to `master`:

```bash
cd /workspace && git checkout master
./scripts/restart-app.sh --background        # rebuild + restart on merged code
cd tests/Playwright.Tests
npx playwright test tests/06-admin-organizations.spec.ts
```

Expected: passing output on the merged build. Also run `dotnet test tests/ArchitectureTests` to
confirm Principle III still holds. Only after all three gates show evidence may the feature be
claimed complete; then return to `master` and confirm the session ends there (Principle XII).
