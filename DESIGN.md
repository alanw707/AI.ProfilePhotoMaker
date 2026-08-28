---
name: AI Profile Photo Maker — Studio Proof Desk
description: A portrait-first proofing system that makes professional-photo package fulfillment visible and trustworthy.
colors:
  proof-paper: "#f7f4ed"
  proof-paper-deep: "#ece7dc"
  production-ink: "#202529"
  proof-muted: "#4d555b"
  proof-rule: "#b9b2a5"
  proof-cobalt: "#2457c5"
  proof-cobalt-deep: "#173f99"
  proof-red: "#9f3028"
  completion-green: "#246b4b"
typography:
  display:
    fontFamily: "Archivo, sans-serif"
    fontSize: "clamp(2.375rem, 6vw, 4.375rem)"
    fontWeight: 800
    lineHeight: 0.95
    letterSpacing: "-0.04em"
  headline:
    fontFamily: "Archivo, sans-serif"
    fontSize: "clamp(1.875rem, 4vw, 2.875rem)"
    fontWeight: 700
    lineHeight: 1
    letterSpacing: "-0.025em"
  body:
    fontFamily: "Archivo, sans-serif"
    fontSize: "1rem"
    fontWeight: 400
    lineHeight: 1.55
  label:
    fontFamily: "Archivo, sans-serif"
    fontSize: "0.75rem"
    fontWeight: 700
    lineHeight: 1.3
    letterSpacing: "0.06em"
rounded:
  mark: "4px"
  control: "9px"
  ticket: "12px"
  surface: "14px"
spacing:
  tight: "8px"
  compact: "12px"
  control: "14px"
  section: "22px"
  roomy: "26px"
components:
  button-primary:
    backgroundColor: "{colors.proof-cobalt}"
    textColor: "#ffffff"
    rounded: "{rounded.control}"
    padding: "11px 18px"
    height: "46px"
  button-primary-hover:
    backgroundColor: "{colors.proof-cobalt-deep}"
    textColor: "#ffffff"
  proof-surface:
    backgroundColor: "#fffdf8"
    textColor: "{colors.production-ink}"
    rounded: "{rounded.surface}"
    padding: "22px"
  fulfillment-ticket:
    backgroundColor: "#fffdf8"
    textColor: "{colors.production-ink}"
    rounded: "{rounded.ticket}"
    padding: "18px 20px"
---

# Design System: Studio Proof Desk

## Overview

**Creative North Star: "The Studio Proof Desk"**

The system behaves like a portrait studio presenting finished proofs, not an AI dashboard selling generation volume. Real photography is always the largest material. Package status reads like a clipped production ticket: factual, compact, and separate from the image itself.

This world is currently implemented for `/app/enhance`. Unrelated routes retain their incumbent visual systems until explicitly migrated; do not copy route-specific proofing composition into marketing, admin, or settings surfaces by default.

**Key Characteristics:**
- Portrait-first scale with source and candidate context kept nearby.
- Warm matte paper, charcoal production ink, cobalt proof marks, and restrained status stamps.
- One dominant action advances the current fulfillment milestone.
- Candidate, refinement, premium add-on, and export allowances stay visibly separate.
- Mobile composition preserves the same sequence and adds a safe-area-aware fulfillment action.

## Colors

A restrained light palette reflects a proofing table used on phones and office screens under ordinary ambient light. Cobalt is operational, not decorative; proof red and completion green are reserved for explicit status.

### Primary
- **Proof Cobalt:** advances the active step, selected proof, progress, focus, and primary action.
- **Deep Proof Cobalt:** hover states and high-contrast text on pale cobalt fields.

### Secondary
- **Proof Red:** limited to stamped package-status language such as in-progress or complete.
- **Completion Green:** completed step numerals and positive completion states.

### Neutral
- **Proof Paper:** route ground and the dominant physical scene.
- **Deep Proof Paper:** progress tracks, adjustment panels, and quiet grouped regions.
- **Production Ink:** all primary text and strong rules.
- **Proof Muted:** explanatory copy, dimensions, and secondary status.
- **Proof Rule:** fine dividers and component outlines.

**The Allowance Color Rule.** Color never merges candidate, refinement, augmentation, or export meaning; labels and counts remain explicit.

**The Cobalt Rarity Rule.** Cobalt marks selection, progress, focus, or the one primary action. It does not decorate passive containers.

## Typography

**Display Font:** Archivo with a sans-serif fallback
**Body Font:** Archivo with a sans-serif fallback

**Character:** Archivo’s width range and sturdy terminals feel like editorial production notation without becoming technical cosplay. Heavy compressed display text creates authorship; body and label roles remain plain and highly scannable.

### Hierarchy
- **Display** (800, responsive 38–70px, 0.95): route thesis only; active-work states reduce it substantially.
- **Headline** (700, responsive 30–46px, 1): current outcome or major task.
- **Title** (700, 22–32px): proofing tools, refinement, and export regions.
- **Body** (400, 16px, 1.55): guidance, recovery, and package explanation; target 65–72 characters per line.
- **Label** (700, 11–12px, 0.06em, uppercase): package terms, measurements, and status labels—not promotional eyebrows.

**The Face-First Type Rule.** Typography frames the portrait and task; it never competes with the selected proof at equal scale inside the workspace.

## Layout

The route uses a centered 1180px working surface with 20px desktop gutters and 8–10px phone gutters. Setup uses source context beside the current creation task. Review uses a dominant selected proof, a smaller source proof, and a horizontal contact sheet.

At 900px, dense allowance detail collapses from four to two columns and adjustment regions stack. At 640px, the experience becomes one column, portrait choices become a horizontal snap strip, and the active paid-fulfillment action becomes fixed above the safe area. The DOM sequence remains source → create → proof/export at every width.

Spacing is tighter inside a task group and expands between milestones. Headings always have more space above than below. Local horizontal scrolling is permitted for candidate/style strips; page-level horizontal overflow is not.

## Elevation & Depth

The system is flat by default. Fine production rules and paper tone establish hierarchy; soft ambient elevation appears only under the current working sheet, selected portrait, sticky action, or floating status utility.

### Shadow Vocabulary
- **Working sheet:** `0 12px 34px rgba(55, 48, 38, 0.08)` for major route surfaces.
- **Selected proof:** `0 18px 40px rgba(55, 48, 38, 0.18)` so the chosen image lifts above the paper.
- **Sticky action:** `0 14px 34px rgba(55, 48, 38, 0.18–0.24)` for viewport-anchored actions only.

**The Flat-Until-Active Rule.** Passive controls and add-ons use borders without shadows. Elevation communicates working focus or viewport attachment.

## Shapes

Corners are gently production-made rather than pillowy: 9px for controls, 12px for tickets, and 14px for major surfaces. Candidate proofs use tighter 3–7px corners to preserve photographic sharpness. Small status marks may use 4–6px corners and a slight one- or two-degree rotation.

Pills are not a container language. Circular geometry is reserved for sequence numerals and compact candidate numbers where the shape carries identification.

## Components

### Buttons
- **Shape:** compact production control (9px radius), at least 44px high.
- **Primary:** Proof Cobalt with white text; one per active milestone.
- **Hover / Focus:** Deep Proof Cobalt on hover; a 3px cobalt focus ring with 3px offset.
- **Secondary:** paper-white field, fine proof-rule border, production-ink text.
- **Disabled:** remains legible while its nearby copy names the missing requirement.

### Cards / Containers
- **Corner Style:** 12–14px on tickets and working sheets; tighter on photo proofs.
- **Background:** paper white on the warm proof-paper ground.
- **Shadow Strategy:** flat unless current, selected, or sticky.
- **Border:** one fine neutral rule; never pair a decorative border with a broad shadow.
- **Internal Padding:** 14–26px according to density.

### Inputs / Fields
- **Style:** native controls on paper-white fields with fine neutral strokes and 9px corners.
- **Focus:** cobalt outline and native keyboard behavior.
- **Range controls:** cobalt track accent, tabular live value, and a full text label.
- **Error / Disabled:** nearby plain-language reason; color never carries the state alone.

### Navigation

The shared application header remains incumbent. Inside the route, the three-step rail uses numbered semantic list items, a pale cobalt active field, green completed numerals, and a cobalt lower rule. Mobile keeps all three stages visible and removes descriptive subcopy before hiding stage identity.

### Fulfillment Ticket

The ticket is the route’s signature status component. It always names the selected package, generated/total candidates, separate remaining allowances, export availability, and a textual status stamp. Its progressbar has semantic values and a plain-language equivalent.

### Contact Sheet

Candidates are native buttons containing a real runtime portrait, numeric identity, recommendation label when available, and `aria-pressed` selection state. The selected proof receives the cobalt mark; the strip scrolls locally on narrow screens.

## Do's and Don'ts

### Do:
- **Do** make the selected portrait the largest element in candidate review.
- **Do** keep package fulfillment and allowance counts visible in plain text.
- **Do** expose one dominant next action and keep it reachable on phones.
- **Do** preserve native controls, focus states, 44px targets, reduced motion, and non-color status labels.
- **Do** use real user/provider photography; controls and notation remain semantic code.

### Don't:
- **Don't** present candidate generation as regeneration or consume refinement/add-on allowance for initial fulfillment.
- **Don't** show refinement, premium add-on, or download actions as the paid package’s next step while candidate slots remain.
- **Don't** turn the route into a grid of equal-weight feature cards or generic AI gradient chrome.
- **Don't** use emoji or Unicode glyphs as the route’s icon system.
- **Don't** promote proof marks, job tickets, or route composition into unrelated surfaces without a deliberate migration.
