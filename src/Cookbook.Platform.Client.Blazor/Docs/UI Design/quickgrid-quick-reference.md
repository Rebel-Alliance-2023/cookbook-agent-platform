# QuickGrid Refined Minimalism - Quick Reference

## Common Page Structure

```html
<!-- Standard page layout -->
<div class="qg-container">
  
  <!-- Page Header -->
  <header class="qg-page-header">
    <h1 class="qg-page-title">Page Title</h1>
    <p class="qg-page-subtitle">Brief description of the page purpose</p>
  </header>
  
  <!-- Content Section -->
  <section class="qg-section">
    
    <!-- Section Header -->
    <div class="qg-section-header">
      <h2 class="qg-section-title">Section Title</h2>
      <p class="qg-section-description">Brief description</p>
    </div>
    
    <!-- Grid -->
    <div class="qg-grid-container">
      <table class="qg-grid">
        <thead>
          <tr><th>Column 1</th><th>Column 2</th></tr>
        </thead>
        <tbody>
          <tr><td>Value 1</td><td>Value 2</td></tr>
        </tbody>
      </table>
    </div>
    
  </section>
  
</div>
```

## Class Quick Reference

### Layout
```
.qg-container         → Max-width 1200px, centered, responsive padding
.qg-section          → 64px bottom margin
.qg-card             → White background, subtle border, minimal shadow
.qg-card-raised      → Card with slightly more elevation
```

### Headers
```
.qg-page-header      → Page-level header with bottom border
.qg-page-title       → 36px, semibold, tight letter-spacing
.qg-page-subtitle    → 18px, secondary color, relaxed line-height

.qg-section-header   → Section-level header with bottom border
.qg-section-title    → 24px, semibold, tight letter-spacing
.qg-section-description → 14px, tertiary color
```

### Grid/Table
```
.qg-grid-container   → Wrapper with border and shadow
.qg-grid             → Table with refined styling
```

### Forms
```
.qg-label            → Uppercase, wide spacing, tertiary color
.qg-input            → Text input with refined borders
.qg-select           → Dropdown with refined styling
```

### Buttons
```
.qg-btn .qg-btn-primary    → Blue, white text
.qg-btn .qg-btn-secondary  → White, bordered
.qg-btn .qg-btn-ghost      → Transparent, minimal
.qg-btn .qg-btn-sm         → Smaller variant
```

### Badges
```
.qg-badge .qg-badge-success  → Green tint
.qg-badge .qg-badge-warning  → Orange tint
.qg-badge .qg-badge-error    → Red tint
.qg-badge .qg-badge-info     → Blue tint
.qg-badge .qg-badge-neutral  → Gray tint
```

### Utilities
```
/* Spacing */
.qg-mb-8, .qg-mb-12, .qg-mb-16, .qg-mb-24, .qg-mb-32
.qg-mt-8, .qg-mt-12, .qg-mt-16, .qg-mt-24, .qg-mt-32

/* Text */
.qg-text-sm, .qg-text-base, .qg-text-lg
.qg-text-primary, .qg-text-secondary, .qg-text-tertiary
.qg-font-normal, .qg-font-medium, .qg-font-semibold
.qg-mono

/* Layout */
.qg-flex, .qg-flex-col
.qg-items-center, .qg-justify-between
.qg-gap-4, .qg-gap-8, .qg-gap-12
.qg-grid, .qg-grid-2, .qg-grid-3, .qg-grid-4
```

## Color Token Reference

```css
/* Canvas & Surfaces */
--color-canvas         #FAFAF9  (warm off-white)
--color-surface        #FFFFFF  (pure white)

/* Text */
--color-text-primary   #1A1A1A  (near-black)
--color-text-secondary #525252  (medium gray)
--color-text-tertiary  #737373  (light gray)
--color-text-disabled  #A3A3A3  (very light gray)

/* Borders */
--color-border-subtle  #F5F5F4  (barely visible)
--color-border-default #E7E5E4  (standard)
--color-border-emphasis #D6D3D1 (prominent)

/* Accent */
--color-accent-primary #2563EB  (blue)

/* Semantic */
--color-success        #059669  (green)
--color-warning        #D97706  (orange)
--color-error          #DC2626  (red)
--color-info           #0284C7  (blue)
```

## Spacing Token Reference

```css
/* Common spacing values (8pt grid) */
--space-4     0.5rem    (8px)   → Small gaps
--space-6     0.75rem   (12px)  → Input padding
--space-8     1rem      (16px)  → Standard spacing
--space-12    1.5rem    (24px)  → Card padding
--space-16    2rem      (32px)  → Section spacing
--space-24    3rem      (48px)  → Large gaps
--space-32    4rem      (64px)  → Section breaks
```

## Typography Token Reference

```css
/* Font Sizes */
--font-size-xs    0.75rem   (12px)  → Labels, metadata
--font-size-sm    0.875rem  (14px)  → Grid content, UI
--font-size-base  1rem      (16px)  → Body text
--font-size-lg    1.125rem  (18px)  → Subtitles
--font-size-2xl   1.5rem    (24px)  → Section headers
--font-size-4xl   2.25rem   (36px)  → Page titles

/* Font Weights */
--font-weight-normal    400  → Body text
--font-weight-medium    500  → UI elements
--font-weight-semibold  600  → Headings

/* Letter Spacing */
--letter-spacing-tight  -0.01em  → Large text
--letter-spacing-wide   0.02em   → Small text (labels)
```

## Common Patterns

### Filter Toolbar
```html
<div class="qg-card qg-mb-16">
  <div class="qg-flex qg-gap-8 qg-items-center">
    <div>
      <label class="qg-label">Filter</label>
      <select class="qg-select">
        <option>Equals</option>
      </select>
    </div>
    <div>
      <label class="qg-label">Value</label>
      <input class="qg-input" type="text">
    </div>
    <button class="qg-btn qg-btn-secondary qg-btn-sm">Clear</button>
  </div>
</div>
```

### Status Badge in Grid
```html
<span class="qg-badge qg-badge-success">Active</span>
```

### Action Button Group
```html
<div class="qg-flex qg-gap-4">
  <button class="qg-btn qg-btn-primary">Save</button>
  <button class="qg-btn qg-btn-ghost">Cancel</button>
</div>
```

### Info Card
```html
<div class="qg-card">
  <h3 class="qg-mb-8">Card Title</h3>
  <p class="qg-text-secondary">Card description text</p>
</div>
```

## Migration Checklist

When converting a page:

1. ✅ Replace `.demo-container` → `.qg-container`
2. ✅ Replace `.demo-header` → `.qg-page-header`
3. ✅ Replace `.demo-section` → `.qg-section`
4. ✅ Wrap tables in `.qg-grid-container`
5. ✅ Replace table classes with `.qg-grid`
6. ✅ Update button classes to `.qg-btn` variants
7. ✅ Replace status classes with `.qg-badge` variants
8. ✅ Remove hardcoded colors from component CSS
9. ✅ Use spacing tokens instead of arbitrary values
10. ✅ Update font sizes to use design system scale

## Custom Component CSS Template

```css
/* Component-specific styles (e.g., ConditionalStyleDemo.razor.css) */

/* Only include styles that extend the base system */
/* Never override global styles without good reason */

/* Example: Custom cell styling */
::deep .custom-cell {
  /* Use design tokens */
  padding: var(--space-4) var(--space-6);
  background-color: var(--color-surface);
  border: 1px solid var(--color-border-default);
  border-radius: var(--card-radius);
  
  /* Refined transition */
  transition: all var(--duration-fast) var(--ease-in-out);
}

::deep .custom-cell:hover {
  background-color: var(--color-canvas);
  border-color: var(--color-border-emphasis);
}

/* Component states using semantic colors */
::deep .custom-cell-success {
  background-color: var(--color-success-subtle);
  border-color: var(--color-success-border);
  color: var(--color-success);
}
```

## Do's and Don'ts

### ✅ DO
- Use design tokens exclusively
- Follow 8pt spacing grid
- Use single accent color sparingly
- Embrace whitespace
- Test on mobile (768px)
- Maintain WCAG AA contrast

### ❌ DON'T
- Hardcode colors (#FFFFFF)
- Use arbitrary spacing (1.3rem)
- Use gradients or heavy shadows
- Mix font weights (stick to 400, 500, 600)
- Center-align everything
- Use decorative elements

## Quick Testing

Verify your implementation:
```
✓ All spacing is 8pt multiples (8, 16, 24, 32, 48, 64)
✓ No hardcoded colors in component CSS
✓ Font sizes use design system scale
✓ Buttons use .qg-btn classes
✓ Grid wrapped in .qg-grid-container
✓ Page has proper header structure
✓ Mobile tested at 768px
```

## Need Help?

1. Check full documentation: `quickgrid-design-system-docs.md`
2. Review redesigned demo pages for examples
3. Search for similar patterns in global CSS
4. When in doubt, use less (minimalism!)
