# Data Model: Per-User Theme Preference

**Feature**: [spec.md](spec.md) | **Date**: 2026-08-29

No schema migration is required — the storage already exists (spec 027-era
`Student.ThemePreference`, default `"System"`). This model documents what the feature
reads/writes and the two derived representations.

## Entities

### 1. User theme preference (account attribute)

**Backing store**: MSSQL `Students.ThemePreference` (Enrollment module, `Student` entity)
— already present, already written by `EnrollmentService.UpdatePreferencesAsync`.

| Field | Type | Constraints / defaults |
|-------|------|------------------------|
| `ThemePreference` | `string` | One of `System`, `Light`, `Dark`. Default `System`. Unknown/empty values are treated as `System` at every read boundary (FR-010). |

- **Ownership**: belongs to the user's account, not the device (spec assumption).
- **Write path**: `Settings` page → `EnrollmentService.UpdatePreferencesAsync` → MSSQL.
- **Read paths**: (a) Settings page load → `EnrollmentService.GetPreferencesAsync`;
  (b) every page render → auth cookie claim (below).
- **State transitions**: free switching among the three values at any time; no
  approval/verification gates.

### 2. Theme claim (derived cookie representation)

**Backing store**: ASP.NET Core auth cookie (claims), re-issued on sign-in and on theme
save.

| Claim | Type | Presence | Value |
|-------|------|----------|-------|
| `ThemePreference` | `string` | **Always present** for signed-in users | `System` \| `Light` \| `Dark` (normalized — empty/unknown → `System`, per FR-010) |

- Built by `AuthClaims.Build` (single source of truth — pin test
  `tests/Host.Tests/AuthClaimsTests.cs` MUST be updated in the same change).
- Re-issued by `AuthCookieRefresher.RefreshAsync` after a successful theme save.
- Absent for anonymous visitors ⇒ layout renders System (FR-009).
- Lifetime: until the next re-issue (theme save) or cookie expiry; always derivable from
  MSSQL on next sign-in, so a lost/stale cookie self-heals (Principle VI: losing the
  derived copy is fine).

### 3. Theme appearance (palette)

**Backing store**: CSS design tokens in `src/Host/wwwroot/css/site.css` — not data,
versioned with the code.

| Appearance | Trigger | Key attributes |
|------------|---------|----------------|
| **Light** (paper) | `data-theme="light"` **or no attribute** (default) | Warm paper: `bg #f6f1e8`, `surface #fdfbf7` (no pure white — SC-006), text `#2c2a26` |
| **Dark** (night-reading) | `data-theme="dark"` | Soft warm dark: `bg #1d1a16`, `surface #262219`, text `#e9e4da` |

- Selected by exactly one mechanism: the `data-theme` attribute on `<html>` (server-set
  for explicit preferences, inline-script-set for System — see research R2).
- All 20 color tokens are overridden in `[data-theme="dark"]`; typography/spacing/layout
  tokens are theme-invariant.
- Contrast contract (WCAG AA, computed — see research R3): body and secondary text
  ≥ 4.5:1 against their backgrounds in both palettes; semantic colors (brand/success/
  error) ≥ 4.5:1 on surfaces.

## Relationships

```text
Student (MSSQL, Enrollment)
  └── ThemePreference  1:1  (source of truth, default "System")
        ├──> auth cookie ThemePreference claim  (derived; re-issued on sign-in / save)
        │        └──> <html data-theme>  (render-time, per request)
        └──> Settings page form value  (edit UI)
```

## Validation rules

| Rule | Enforced at |
|------|-------------|
| Value ∈ {System, Light, Dark} or normalized to System | `AuthClaims.Build` (write to claim), `_Layout` (read), Settings page load (display) |
| Anonymous ⇒ System, no preference created/modified | layout (claim absent) + FR-009; no anonymous write path exists |
| Failed save ⇒ displayed theme unchanged | Settings AJAX handler returns error JSON; client applies attribute only on success (FR-011) |
| SCORM authored content unaffected | iframe document isolation (no rule to enforce) |
