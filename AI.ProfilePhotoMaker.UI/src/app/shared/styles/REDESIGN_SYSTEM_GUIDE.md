# Landing Page Redesign - Design System Guide

This guide documents the extended design system created for the landing page
redesign. The system provides modern design patterns including glassmorphism
effects, bento grid layouts, and micro-interaction animations.

## Overview

The redesign system extends the existing shared styles with:

- **Extended CSS Variables**: New color palettes, depth layers, and animation
  timing
- **Glassmorphism Mixins**: Reusable frosted glass effects with backdrop blur
- **Bento Grid System**: Flexible grid layout with varied sizing utilities
- **Animation Library**: Keyframes and utilities for modern micro-interactions

## File Structure

```
src/app/shared/styles/
├── index.sass                  # Main entry point (imports all styles)
├── _mixins.sass               # Original shared mixins
├── _utilities.sass            # Original utility classes
└── _redesign-system.sass      # NEW: Redesign system extensions
```

## Usage

Import the shared styles in your component SASS file:

```sass
@use '../../shared/styles/index' as shared

.my-component
  @include shared.glass-card
  @include shared.bento-grid(4, 24px)
```

## CSS Variables

### Extended Color Palette

```sass
// Accent Gradients
--accent-gradient-1: linear-gradient(135deg, #4fd1c7 0%, #3b82f6 100%)
--accent-gradient-2: linear-gradient(135deg, #667eea 0%, #764ba2 100%)
--accent-gradient-3: linear-gradient(135deg, #f093fb 0%, #f5576c 100%)
--accent-gradient-4: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%)
```

### Glassmorphism Variables

```sass
--glass-bg: rgba(255, 255, 255, 0.1)           // Base glass background
--glass-bg-light: rgba(255, 255, 255, 0.05)    // Subtle glass
--glass-bg-strong: rgba(255, 255, 255, 0.15)   // Prominent glass
--glass-border: rgba(255, 255, 255, 0.2)       // Glass border
--glass-shadow: 0 8px 32px rgba(0, 0, 0, 0.1)  // Glass shadow
```

### Depth Layers

Progressive elevation shadows for visual hierarchy:

```sass
--depth-1: 0 4px 16px rgba(0, 0, 0, 0.08)   // Subtle elevation
--depth-2: 0 8px 32px rgba(0, 0, 0, 0.12)   // Medium elevation
--depth-3: 0 16px 48px rgba(0, 0, 0, 0.16)  // High elevation
--depth-4: 0 24px 64px rgba(0, 0, 0, 0.20)  // Maximum elevation
```

### Animation Timing

```sass
--timing-fast: 200ms                                    // Quick transitions
--timing-normal: 300ms                                  // Standard transitions
--timing-slow: 500ms                                    // Slow transitions
--timing-slower: 800ms                                  // Very slow transitions
--easing-smooth: cubic-bezier(0.4, 0, 0.2, 1)          // Material Design easing
--easing-bounce: cubic-bezier(0.68, -0.55, 0.265, 1.55) // Bounce effect
```

## Glassmorphism Mixins

### Basic Glassmorphism

```sass
.my-card
  @include glass-base
  // Applies: background blur, border, shadow
```

### Variants

```sass
@include glass-light      // Subtle glass effect
@include glass-strong     // Prominent glass effect
@include glass-card       // Glass card with hover effect
@include glass-overlay    // For modals/overlays
@include glass-fallback   // Fallback for unsupported browsers
```

### Example Usage

```sass
.feature-card
  @include glass-card
  padding: 32px
  border-radius: 20px

  &:hover
    transform: translateY(-4px)
```

## Bento Grid System

The bento grid system creates varied-size layouts inspired by Japanese bento
boxes.

### Grid Container

```sass
.features-section
  @include bento-grid(4, 24px)
  // Creates 4-column grid with 24px gap
  // Automatically responsive
```

### Grid Item Sizing

```sass
.feature-item
  @include bento-item-base    // Base styling
  @include bento-item-2x2     // 2 columns × 2 rows
```

Available sizes:

- `bento-item-1x1` - Single cell (1 column × 1 row)
- `bento-item-2x1` - Wide (2 columns × 1 row)
- `bento-item-1x2` - Tall (1 column × 2 rows)
- `bento-item-2x2` - Large (2 columns × 2 rows)
- `bento-item-3x1` - Extra wide (3 columns × 1 row)

### Responsive Behavior

The bento grid automatically adapts:

- **Desktop (1025px+)**: 4 columns
- **Tablet (769-1024px)**: 3 columns
- **Mobile (481-768px)**: 2 columns
- **Small Mobile (≤480px)**: 1 column

### Complete Example

```sass
.features-bento
  @include bento-grid(4, 24px)

  .feature-primary
    @include bento-item-base
    @include bento-item-2x2
    @include glass-card

  .feature-secondary
    @include bento-item-base
    @include bento-item-1x1
    @include glass-light
```

## Animation Library

### Keyframe Animations

Available animations:

- `fade-in-up` - Fade in with upward motion
- `fade-in-down` - Fade in with downward motion
- `fade-in-left` - Fade in from left
- `fade-in-right` - Fade in from right
- `scale-in` - Scale in animation
- `float-gentle` - Gentle floating effect
- `pulse-glow` - Pulsing glow effect
- `shimmer` - Shimmer loading effect
- `gradient-shift` - Animated gradient
- `rotate` - Continuous rotation
- `bounce` - Bounce animation
- `slide-in-bottom` - Slide in from bottom
- `slide-in-top` - Slide in from top
- `ripple` - Ripple effect
- `glow-pulse` - Glowing pulse for buttons

### Animation Utility Classes

```html
<div class="animate-fade-in-up delay-2">Content fades in with 0.2s delay</div>

<div class="animate-float-gentle">Content floats gently</div>

<div class="animate-pulse-glow">Content pulses with glow</div>
```

### Delay Utilities

Add staggered animations:

```html
<div class="animate-fade-in-up delay-1">First</div>
<div class="animate-fade-in-up delay-2">Second</div>
<div class="animate-fade-in-up delay-3">Third</div>
```

Delays range from `delay-1` (0.1s) to `delay-10` (1.0s).

## Micro-Interaction Utilities

### Hover Effects

```html
<div class="hover-lift">
  <!-- Lifts up on hover -->
</div>

<div class="hover-scale">
  <!-- Scales up on hover -->
</div>

<div class="hover-glow">
  <!-- Glows on hover -->
</div>
```

### Transition Utilities

```html
<div class="smooth-color">
  <!-- Smooth color transitions -->
</div>

<div class="smooth-border">
  <!-- Smooth border transitions -->
</div>
```

## Gradient Text

Create gradient text effects:

```html
<h1 class="gradient-text">Beautiful Gradient Text</h1>

<h2 class="gradient-text-2">Alternative Gradient</h2>
```

## Accessibility

### Reduced Motion Support

The system automatically respects user preferences for reduced motion:

```sass
@media (prefers-reduced-motion: reduce)
  // All animations are disabled or minimized
  // Transforms are removed
  // Scroll behavior becomes instant
```

Users who prefer reduced motion will see:

- No animations
- No transforms on hover
- Instant transitions
- Smooth scroll disabled

## Dark Theme Support

All variables automatically adapt to dark theme:

```sass
[data-theme="dark"]
  --glass-bg: rgba(30, 41, 59, 0.3)
  --glass-border: rgba(255, 255, 255, 0.15)
  --depth-1: 0 4px 16px rgba(0, 0, 0, 0.3)
  // ... etc
```

## Browser Support

### Glassmorphism Fallbacks

For browsers that don't support `backdrop-filter`:

```sass
.my-glass-element
  @include glass-card
  @include glass-fallback
  // Provides solid background fallback
```

### Supported Browsers

- Chrome/Edge 76+
- Firefox 103+
- Safari 9+
- iOS Safari 9+

## Best Practices

### Performance

1. **Use GPU-accelerated properties**: Prefer `transform` and `opacity` over
   layout properties
2. **Limit backdrop-filter usage**: Can be expensive on low-end devices
3. **Use will-change sparingly**: Only for elements that will definitely animate

### Accessibility

1. **Always test with reduced motion**: Ensure functionality works without
   animations
2. **Maintain color contrast**: Glassmorphism can reduce contrast
3. **Provide keyboard navigation**: All interactive elements must be keyboard
   accessible

### Responsive Design

1. **Mobile-first approach**: Start with mobile layouts
2. **Test all breakpoints**: Verify bento grid adapts correctly
3. **Touch targets**: Ensure minimum 44×44px on mobile

## Examples

### Glass Card with Bento Grid

```sass
.features-section
  @include bento-grid(4, 24px)
  padding: 80px 24px

  .feature-card
    @include bento-item-base
    @include glass-card

    &.large
      @include bento-item-2x2

    &.wide
      @include bento-item-2x1
```

### Animated Hero Section

```sass
.hero-content
  .hero-title
    @extend .animate-fade-in-up
    @extend .delay-1
    @extend .gradient-text

  .hero-subtitle
    @extend .animate-fade-in-up
    @extend .delay-2

  .hero-cta
    @extend .animate-fade-in-up
    @extend .delay-3
    @extend .hover-lift
```

### Interactive Card

```sass
.style-card
  @include glass-card
  @extend .hover-lift
  @extend .smooth-color

  transition: all var(--timing-normal) var(--easing-smooth)

  &:hover
    border-color: var(--accent-primary)
```

## Migration from Old Styles

If migrating existing components:

1. Replace manual backdrop-filter with `@include glass-base`
2. Replace custom grids with `@include bento-grid()`
3. Replace manual animations with utility classes
4. Use CSS variables instead of hardcoded values

## Testing

### Visual Testing

Test the design system across:

- Multiple browsers (Chrome, Firefox, Safari, Edge)
- Multiple devices (desktop, tablet, mobile)
- Both light and dark themes
- With reduced motion enabled

### Performance Testing

Monitor:

- Animation frame rates (should maintain 60fps)
- Paint times with backdrop-filter
- Layout shifts during animations

## Support

For questions or issues with the redesign system:

1. Check this guide first
2. Review the source code in `_redesign-system.sass`
3. Test in isolation before integrating
4. Verify browser support for advanced features

## Version History

- **v1.0.0** (2024-01-16): Initial release
  - Extended CSS variables
  - Glassmorphism mixins
  - Bento grid system
  - Animation library
  - Micro-interaction utilities
