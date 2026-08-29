# Plan: Deterministic Dashboard Percent Format

**Input**: [spec.md](spec.md)

## Summary

Replace the two culture-dependent `ToString("P1")` calls in
`src/Host/Pages/Admin/Dashboard/Index.cshtml.cs` with the custom,
culture-proof format `"0.#%"`. No model, service, view, or test changes —
the bug-039 guard's contract (space-free percent) is the target rendering.

## Technical Approach

- **File**: `src/Host/Pages/Admin/Dashboard/Index.cshtml.cs` (2 sites:
  super-user branch ~line 60, OrgAdmin branch ~line 69).
- **Change**: `ToString("P1")` → `ToString("0.#%")`.
- **Why custom format, not `CultureInfo.InvariantCulture`**: verified
  in-container that .NET 10 invariant `P1` still inserts the culture
  percent space (`"0.0 %"`); custom numeric formats bypass
  `PercentPositivePattern` entirely and are stable across cultures and
  runtime versions.
- **Rounding**: `0.#%` rounds to 1 decimal (like `P1`) — display parity.
- **No unit-test pin exists** for the dashboard string (checked
  `tests/Host.Tests`); the E2E bug-039 guard is the pin.

## Verification (Principle XIII)

1. Rebuild (`rm -rf src/Host/obj src/Host/bin && dotnet build src/Host`) +
   restart; show "Now listening" + 200.
2. `npx playwright test tests/04-admin-dashboard.spec.ts` → all pass.
3. Full `npx playwright test` → green (this is what was blocking spec 042's
   T023/T025).

## Risks

- Cosmetic render change for zero (`0.0 %` → `0%`) — no dependent CSS/JS.
- Other `P*` standard formats in the app: grepped `src/Host` +
  `src/Modules` — these two are the **only** standard percent formats.
