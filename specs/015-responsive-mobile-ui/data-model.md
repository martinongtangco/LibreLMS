# Data Model: Responsive Mobile UI

## Summary

This feature introduces **no new data entities, tables, or database changes**. It is a pure presentation-layer enhancement.

## Existing Entities Affected

None. No domain models, application services, or infrastructure layers are modified. All changes are within `src/Host/Pages/` (Razor Pages and partials) and a new `src/Host/wwwroot/css/site.css` stylesheet.

## CSS Custom Properties (Design Tokens)

The following CSS custom properties will be defined in `site.css` to centralize the design tokens currently scattered across inline styles:

```css
:root {
  /* Colors */
  --color-bg:          #f5f5f5;
  --color-surface:     #ffffff;
  --color-text:        #333333;
  --color-text-muted:  #666666;
  --color-text-faint:  #999999;
  --color-brand:       #1a1a2e;
  --color-brand-hover: #16213e;
  --color-border:      #eeeeee;
  --color-border-strong: #dddddd;

  /* Semantic colors */
  --color-category-bg:   #e3f2fd;
  --color-category-text: #1565c0;
  --color-duration-bg:   #f3e5f5;
  --color-duration-text: #7b1fa2;
  --color-success-bg:    #e8f5e9;
  --color-success-text:  #2e7d32;
  --color-error-bg:      #fdecea;
  --color-error-border:  #c62828;

  /* Typography */
  --font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  --font-size-sm: 0.8rem;
  --font-size-base: 0.9rem;
  --font-size-lg: 1.1rem;
  --font-size-xl: 1.3rem;

  /* Spacing */
  --spacing-xs: 0.25rem;
  --spacing-sm: 0.5rem;
  --spacing-md: 1rem;
  --spacing-lg: 1.5rem;
  --spacing-xl: 2rem;

  /* Layout */
  --container-max-width: 960px;
  --container-padding: 1rem;
  --border-radius: 8px;
  --border-radius-sm: 4px;
  --touch-target-min: 44px;

  /* Shadows */
  --shadow-card: 0 1px 3px rgba(0, 0, 0, 0.1);
}
```

These tokens enable consistent styling and easy future theming without touching individual page files.
