# ADR-001: Responsive CSS Organization

## Context

The Libre LMS web portal used inline `<style>` blocks in `_Layout.cshtml` and `style=""` attributes on individual Razor Page elements. This approach had several problems:

1. **No responsive support**: All styles were fixed-width, unsuitable for mobile/tablet viewports
2. **Duplication**: Identical styles repeated across 30+ Razor Pages
3. **No design tokens**: Colors, spacing, and typography values scattered inline
4. **Maintenance burden**: Changing a single design token required editing every file that uses it

The spec (015-responsive-mobile-ui) requires full responsive support across mobile (≤ 480px), tablet (481–768px), and desktop (≥ 769px) viewports while preserving the existing desktop appearance.

## Decision

Extract all styles into a single `wwwroot/css/site.css` stylesheet with:

1. **CSS custom properties (design tokens)** for colors, spacing, typography, shadows, and layout values
2. **Mobile-first media queries** with three breakpoints: 480px, 768px, and 1024px
3. **Semantic BEM-style class names** (e.g., `.metric-card`, `.course-card-badges`, `.my-course-row`)
4. **Hamburger menu** with vanilla JS toggle for viewports ≤ 480px
5. **Horizontal scroll wrappers** for data tables on narrow viewports

Replace all `style=""` attributes with class names. Keep inline styles only for:
- Dynamic Razor-generated values (`@bgColor`, `@textColor`)
- Intentional semantic colors (danger red, warning orange)
- Functional layout properties not worth a CSS class (`white-space: pre-wrap`)
- SVG-specific rendering properties

## Consequences

### Positive
- **Single source of truth**: All styles in one file
- **Easy theming**: Change a design token to update the entire UI
- **Responsive**: Mobile-first CSS with media queries handles all breakpoints
- **No new dependencies**: Plain CSS, no framework needed
- **Desktop parity**: ≥ 1024px breakpoint restores original layout exactly

### Negative
- **CSS file size**: ~20KB uncompressed (acceptable for this codebase)
- **One-time migration cost**: 30+ files touched
- **Hamburger JS**: Minimal vanilla JS (~10 lines) added to `_Layout.cshtml`

### Neutral
- **Standalone pages** (SCORM Launch, Logout) still use minimal inline styles or their own `<link>` tag
- **SVG chart** keeps SVG-internal `<style>` block for node styling (SVG-specific, not page-specific)
