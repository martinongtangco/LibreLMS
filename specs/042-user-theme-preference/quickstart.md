# Quickstart: Per-User Theme Preference — Validation Guide

**Feature**: [spec.md](spec.md) | **Date**: 2026-08-29

Runnable validation scenarios proving the feature works end-to-end. See
[contracts/theme-ui.md](contracts/theme-ui.md) for the exact page/endpoint contracts and
[data-model.md](data-model.md) for the entity/claim model.

## Prerequisites

- Inside `.devcontainer` at repo root; `docker compose up` already running `mssql` + `valkey`.
- .NET 10 SDK per `global.json`; Playwright browsers installed (`cd tests/Playwright.Tests && npx playwright install` if new).
- A test account exists (seeder) — any signed-in learner.

## Start the app (after code changes — Razor views are precompiled)

```bash
# Clean rebuild (Principle XIII, gate 1 — show 'Build succeeded')
rm -rf src/Host/obj src/Host/bin && dotnet build src/Host

# Launch detached, then confirm the "Now listening" lines
cd src/Host && setsid nohup dotnet run --urls "https://localhost:7095;http://localhost:5000" \
  > /tmp/lms-host.log 2>&1 < /dev/null &
grep 'Now listening on' /tmp/lms-host.log
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/Courses   # expect 200
```

## Manual validation (browser, http://localhost:5000)

1. **Default System** — sign in; with device dark mode ON the app is dark, with it OFF the
   app is paper-light. Inspect `<html>`: no `data-theme` attribute server-side; the inline
   head script sets it before first paint (no visible flash).
2. **Select Dark** — Settings → Theme: Dark. The whole page switches instantly (no
   reload); navigate to Browse Courses / My Courses / a course — all dark. View source:
   `<html … data-theme="dark">` (cookie claim re-issued on save).
3. **Select Light (paper)** — same flow; page background and card surfaces are warm paper
   tone, **no pure-white surface** (DevTools: computed `background-color` of `.card` ≠
   `#ffffff`).
4. **Persistence** — sign out, close the browser, sign in again → saved theme restored
   (account-level, not device-level).
5. **System live-follow** — set Theme: System; toggle the OS/browser dark mode with a page
   open → app follows within a second, no reload.
6. **Anonymous** — private/incognito window browsing → follows device setting (System);
   no account preference is created.
7. **SCORM isolation** — launch a SCORM course in Dark mode: shell chrome dark, authored
   content inside the iframe keeps its own appearance.
8. **Save failure** (optional, dev only) — stop the app, keep the browser page open, change
   the theme → error alert appears, displayed theme unchanged.

## E2E validation (Principle XIII, gate 2)

```bash
cd tests/Playwright.Tests
npx playwright test tests/18-theme-preference.spec.ts   # the feature's spec
npx playwright test                                     # full suite — no regressions
```

The spec (per contracts) covers at minimum:
- System default + device-follow (Playwright `colorScheme: 'dark'|'light'` emulation)
- Dark save → applied on Settings without reload, persists across navigation and
  sign-out/sign-in
- Light paper surfaces (no pure white on standard pages)
- AA contrast spot-checks via computed styles (body/muted text vs. background)
- No-flash: `data-theme` present in the first received HTML document (System) or correct
  attribute (Light/Dark) — asserted on document receipt, not after settle
- Anonymous → System
- SCORM shell themed, iframe content untouched

## Post-merge regression (Principle XIII, gate 3)

After merging `story/042-user-theme-preference` into `master`:

```bash
git checkout master && git pull
rm -rf src/Host/obj src/Host/bin && dotnet build src/Host
# restart the app (commands above), then:
cd tests/Playwright.Tests && npx playwright test
```

All three gates must show concrete passing output before the feature is claimed complete.

## Architecture gate

```bash
dotnet test tests/ArchitectureTests   # Principle III — must pass (module boundaries)
dotnet test tests/Host.Tests          # includes the updated AuthClaims claim-set pin
```
