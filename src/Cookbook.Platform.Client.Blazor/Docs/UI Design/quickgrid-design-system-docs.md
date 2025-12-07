# QuickGrid Refined Minimalism - Design System Documentation

## Overview

This design system transforms the QuickGrid demo pages from generic Bootstrap-inspired styling to a refined minimalist aesthetic that emphasizes precision, restraint, and technical sophistication.

## Philosophy

### Core Principles

1. **Every Element Earns Its Place** - No decorative elements without purpose
2. **Whitespace Creates Hierarchy** - Generous spacing guides the eye
3. **Subtle Details Discovered** - Sophistication in the details, not loud announcements
4. **Mathematical Precision** - 8pt grid system for spatial harmony
5. **Single Accent Color** - Restraint in color usage for maximum impact

### Design Goals

- **Technical Clarity** - Optimized for data-focused interfaces
- **Reading Comfort** - Warm neutrals reduce eye strain
- **Professional Polish** - Production-grade attention to detail
- **Accessibility** - WCAG AA compliant with refined aesthetics

## Design Tokens

### Color System

#### Temperature Strategy
We use **warm neutrals** (`#FAFAF9` canvas) instead of pure white to reduce eye strain during extended reading sessions. This creates a more comfortable, paper-like quality.

```css
--color-canvas: #FAFAF9    /* Warm off-white base */
--color-surface: #FFFFFF    /* Pure white for cards */
```

#### Text Hierarchy
Never use pure black (`#000000`). Near-black provides better readability and feels more refined:

```css
--color-text-primary: #1A1A1A      /* Primary content */
--color-text-secondary: #525252    /* Supporting text */
--color-text-tertiary: #737373     /* Labels, captions */
--color-text-disabled: #A3A3A3     /* Inactive states */
```

#### Border Strategy
Multiple border weights create subtle separation without visual noise:

```css
--color-border-subtle: #F5F5F4     /* Nearly invisible */
--color-border-default: #E7E5E4    /* Standard separation */
--color-border-emphasis: #D6D3D1   /* Important boundaries */
--color-border-strong: #A8A29E     /* Maximum contrast */
```

#### Accent Color
**Single accent color** used sparingly for maximum impact. We chose a classic blue that conveys trust and professionalism:

```css
--color-accent-primary: #2563EB
```

Use this ONLY for:
- Primary actions (submit buttons)
- Critical navigation elements
- Section emphasis (sparse underlines)
- Focus states

**Never** use accent color for:
- Body text
- Multiple UI elements simultaneously
- Background fills (only subtle tints)

### Typography System

#### Font Families

**UI Text**: Inter (sans-serif)
- Clean, modern, optimized for UI
- Excellent weight range (300-600)
- Superior small-size legibility

**Code/Technical**: JetBrains Mono (monospace)
- Designed for developers
- Clear character distinction
- Ligature support for code

```css
--font-sans: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
--font-mono: 'JetBrains Mono', 'SF Mono', 'Consolas', monospace;
```

#### Font Scale
Modular scale based on 16px base with precise increments:

| Token | Size | Pixels | Use Case |
|-------|------|--------|----------|
| `--font-size-xs` | 0.75rem | 12px | Labels, metadata |
| `--font-size-sm` | 0.875rem | 14px | Grid content, UI text |
| `--font-size-base` | 1rem | 16px | Body text |
| `--font-size-lg` | 1.125rem | 18px | Subtitles |
| `--font-size-2xl` | 1.5rem | 24px | Section headers |
| `--font-size-4xl` | 2.25rem | 36px | Page titles |

#### Weight Hierarchy

| Weight | Value | Use Case |
|--------|-------|----------|
| Light | 300 | Large display text only |
| Normal | 400 | Body text, descriptions |
| Medium | 500 | UI elements, buttons |
| Semibold | 600 | Headings, emphasis |

**Never use bold (700)** - it's too heavy for refined minimalism.

#### Letter Spacing

Optical adjustments for improved legibility:

```css
--letter-spacing-tight: -0.01em    /* Headers (reduce visual weight) */
--letter-spacing-normal: 0         /* Body text */
--letter-spacing-wide: 0.02em      /* Labels (improve scanning) */
```

**Rule**: Tighten large text, widen small text.

### Spacing System

**8pt Grid** - All spacing in multiples of 8px for mathematical harmony:

```
2px   4px   6px   8px   12px  16px  24px  32px  48px  64px  96px  128px
↓     ↓     ↓     ↓     ↓     ↓     ↓     ↓     ↓     ↓     ↓     ↓
1     2     3     4     6     8     12    16    24    32    48    64
```

#### Spacing Patterns

**Component Internal Spacing**:
- Input padding: `12px × 8px` (horizontal × vertical)
- Card padding: `16px`
- Grid cell padding: `12px × 8px`

**Component External Spacing**:
- Between sections: `64px` (--space-32)
- Between cards: `16px` (--space-16)
- Paragraph spacing: `24px` (--space-12)

**Vertical Rhythm**:
```
Page title       → 24px gap
Section header   → 32px gap
Content blocks   → 16px gap
Inline elements  → 8px gap
```

### Shadows & Elevation

Minimal shadows for subtle depth perception:

```css
--shadow-xs: 0 1px 2px rgba(0, 0, 0, 0.04)   /* Cards */
--shadow-sm: 0 1px 3px rgba(0, 0, 0, 0.06)   /* Raised cards */
--shadow-md: 0 2px 6px rgba(0, 0, 0, 0.08)   /* Modals */
```

**Never** use shadows above 0.10 opacity - it creates visual noise.

### Motion & Animation

#### Timing

```css
--duration-instant: 100ms  /* Hover feedback */
--duration-fast: 150ms     /* Most transitions */
--duration-normal: 200ms   /* Smooth changes */
--duration-slow: 300ms     /* Enter/exit animations */
```

#### Easing

```css
--ease-in-out: cubic-bezier(0.4, 0, 0.2, 1)  /* Default */
```

**Precise, not bouncy** - avoid elastic or spring easing. Use linear acceleration curves.

#### What to Animate

✅ **Do Animate**:
- Background color on hover
- Border color on focus
- Opacity for show/hide
- Transform (2-4px) for subtle lift

❌ **Don't Animate**:
- Width/height (causes reflow)
- Box-shadow (expensive)
- Multiple properties simultaneously

## Component Classes

### Naming Convention

All classes use the `qg-` prefix to avoid conflicts:

```
qg-container
qg-section
qg-card
qg-grid
qg-btn
qg-badge
```

### Layout Components

#### Container
```html
<div class="qg-container">
  <!-- Max-width: 1200px, centered, responsive padding -->
</div>
```

#### Section
```html
<section class="qg-section">
  <!-- 64px bottom margin for vertical rhythm -->
</section>
```

#### Card
```html
<div class="qg-card">
  <!-- Subtle border, minimal shadow, 16px padding -->
</div>
```

### Header Components

#### Page Header
```html
<header class="qg-page-header">
  <h1 class="qg-page-title">Conditional Styling Column</h1>
  <p class="qg-page-subtitle">
    Dynamic CSS classes, icons, and tooltips based on cell values
  </p>
</header>
```

#### Section Header
```html
<div class="qg-section-header">
  <h2 class="qg-section-title">Sales Performance Dashboard</h2>
  <p class="qg-section-description">
    Revenue values styled with traffic light system
  </p>
</div>
```

### Grid Components

#### Grid Container
```html
<div class="qg-grid-container">
  <table class="qg-grid">
    <thead>
      <tr>
        <th>Revenue</th>
        <th>Target</th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td>$45,200</td>
        <td>$40,000</td>
      </tr>
    </tbody>
  </table>
</div>
```

Features:
- Uppercase labels with wide letter-spacing
- Subtle alternating rows
- 150ms hover transition
- Minimal borders

### Form Components

```html
<label class="qg-label">Filter Value</label>
<input class="qg-input" type="text" placeholder="Enter value...">

<select class="qg-select">
  <option>Equals</option>
  <option>Greater Than</option>
</select>
```

### Button Components

```html
<!-- Primary action -->
<button class="qg-btn qg-btn-primary">Save Changes</button>

<!-- Secondary action -->
<button class="qg-btn qg-btn-secondary">Cancel</button>

<!-- Tertiary/ghost -->
<button class="qg-btn qg-btn-ghost">Clear All</button>

<!-- Small variant -->
<button class="qg-btn qg-btn-sm qg-btn-secondary">Edit</button>
```

### Badge Components

```html
<span class="qg-badge qg-badge-success">Active</span>
<span class="qg-badge qg-badge-warning">Pending</span>
<span class="qg-badge qg-badge-error">Error</span>
<span class="qg-badge qg-badge-info">Info</span>
<span class="qg-badge qg-badge-neutral">Draft</span>
```

## Migration Guide

### Step 1: Add Global CSS Reference

In your `_Host.cshtml` or `_Layout.cshtml`:

```html
<link rel="stylesheet" href="~/css/quickgrid-refined-minimalism.css">
```

### Step 2: Class Mapping

Replace existing classes with refined equivalents:

| Old Class | New Class | Notes |
|-----------|-----------|-------|
| `.demo-container` | `.qg-container` | Same max-width, refined padding |
| `.demo-header` | `.qg-page-header` | Removed background, cleaner |
| `.demo-section` | `.qg-section` | Removed shadow, minimal border |
| `.demo-grid` | `.qg-grid` | Place inside `.qg-grid-container` |
| `.btn-primary` | `.qg-btn qg-btn-primary` | Refined spacing |
| `.section-description` | `.qg-section-description` | Better typography |

### Step 3: Remove Custom Colors

Delete all color hex codes from component CSS files. Use design tokens:

**Before**:
```css
.demo-header {
  background: #f8f9fa;
  color: #2c3e50;
}
```

**After**:
```css
.qg-page-header {
  /* Color from global system */
}
```

### Step 4: Standardize Spacing

Replace arbitrary padding/margin values with spacing tokens:

**Before**:
```css
.demo-section {
  padding: 1.5rem;
  margin-bottom: 2rem;
}
```

**After**:
```css
.qg-section {
  padding: var(--space-16);
  margin-bottom: var(--space-32);
}
```

### Step 5: Update Typography

Use font scale tokens instead of arbitrary sizes:

**Before**:
```css
h2 {
  font-size: 1.75rem;
  font-weight: 600;
}
```

**After**:
```css
.qg-section-title {
  font-size: var(--font-size-2xl);
  font-weight: var(--font-weight-semibold);
  letter-spacing: var(--letter-spacing-tight);
}
```

## Component-Specific Patterns

### Conditional Styling Cells

**Before** (colorful, distracting):
```css
.cell-success {
  background-color: #d4edda;
  color: #155724;
  border-left: 3px solid #28a745;
}
```

**After** (refined, subtle):
```css
.qg-cell-success {
  background-color: var(--color-success-subtle);
  color: var(--color-success);
  border-left: 1px solid var(--color-success-border);
}
```

**Changes**:
- Thinner left border (1px vs 3px)
- Use design tokens
- Softer color palette

### Filterable Column Toolbar

**Before** (busy, inconsistent):
```css
.filter-toolbar {
  display: flex;
  gap: 0.75rem;
  padding: 1rem;
  background: #fff;
  border-radius: 8px;
}
```

**After** (clean, systematic):
```css
.qg-filter-toolbar {
  display: flex;
  gap: var(--space-8);
  padding: var(--space-12);
  background-color: var(--color-surface);
  border: 1px solid var(--color-border-subtle);
  border-radius: var(--card-radius);
}
```

### Editable Cell States

Add subtle state indicators:

```css
.qg-editable-cell {
  position: relative;
  cursor: pointer;
  transition: background-color var(--duration-fast) var(--ease-in-out);
}

.qg-editable-cell:hover {
  background-color: var(--color-canvas);
}

.qg-editable-cell.editing {
  background-color: var(--color-accent-primary-subtle);
  outline: 1px solid var(--color-accent-primary);
}
```

## Best Practices

### Do's ✅

1. **Use Design Tokens** - Never hardcode colors or spacing
2. **Embrace Whitespace** - Let content breathe
3. **Subtle Interactions** - 2-4px movements, 150ms transitions
4. **Consistent Hierarchy** - Follow the type scale
5. **Single Accent** - Use primary color sparingly
6. **Test Accessibility** - Maintain 4.5:1 contrast minimum

### Don'ts ❌

1. **Don't Use Gradients** - Solid colors only
2. **Don't Use Heavy Shadows** - Max 0.10 opacity
3. **Don't Center Everything** - Left-align for readability
4. **Don't Mix Font Weights** - Stick to 400, 500, 600
5. **Don't Add Decorative Elements** - Every element must serve a purpose
6. **Don't Use Multiple Accent Colors** - One color maximum

## Responsive Strategy

### Breakpoint
Single breakpoint at 768px for simplicity:

```css
@media (max-width: 768px) {
  /* Reduce spacing */
  --container-padding: var(--space-12);
  --section-gap: var(--space-24);
  
  /* Scale down typography */
  .qg-page-title {
    font-size: var(--font-size-3xl);
  }
}
```

### Mobile Patterns

- Stack grid columns vertically
- Reduce padding by 25%
- Maintain touch targets (44px minimum)
- Simplify navigation

## Performance Considerations

### CSS Optimization

1. **Use Custom Properties** - One source of truth, easier theming
2. **Limit Animations** - Only background-color, opacity, transform
3. **Avoid Reflows** - No width/height animations
4. **Use containment** - `contain: layout style paint` for cards

### Loading Strategy

```html
<!-- Inline critical CSS for above-fold content -->
<style>
  /* Minimal base styles */
</style>

<!-- Async load full design system -->
<link rel="preload" href="quickgrid-refined-minimalism.css" as="style" onload="this.onload=null;this.rel='stylesheet'">
```

## Accessibility

### Color Contrast

All text meets WCAG AA standards:

- Primary text: 11.7:1 (AAA)
- Secondary text: 7.2:1 (AAA)
- Tertiary text: 4.6:1 (AA)
- Disabled text: Used for non-essential content only

### Focus States

Clear, consistent focus indicators:

```css
:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring);
}
```

### Screen Readers

Use semantic HTML:
- `<header>` for page headers
- `<section>` for logical sections
- `<table>` with proper `<thead>` and `<tbody>`
- ARIA labels where appropriate

## Testing Checklist

Before deploying:

- [ ] All spacing uses 8pt grid multiples
- [ ] No hardcoded colors (except in global CSS)
- [ ] Contrast ratios meet WCAG AA
- [ ] Typography follows scale (no arbitrary sizes)
- [ ] Focus states visible and consistent
- [ ] Hover states have 150ms transition
- [ ] Mobile layout tested at 768px
- [ ] Print styles tested
- [ ] No console errors related to missing fonts

## Examples

See the redesigned demo pages for reference implementations:

1. **Index.razor** - Homepage with feature grid
2. **ConditionalStyleDemo.razor** - Complex data visualization
3. **FilterableColumnDemo.razor** - Form-heavy interface
4. **EditableColumnDemo.razor** - Interactive editing states

## Support

For questions about the design system:
- Review this documentation
- Examine redesigned demo pages
- Test in isolation before applying broadly
- Maintain consistency across all pages
