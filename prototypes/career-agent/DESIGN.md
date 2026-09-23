---
name: AI Profile Photo Maker — Career Research Desk prototype
description: A factual career workspace with inspectable evidence and contextual assistance.
colors:
  canvas: "#f7f7f2"
  sidebar: "#e9eeeb"
  paper: "#fffefa"
  ink: "#1b2c31"
  muted-ink: "#53656a"
  rule: "#d1d8d4"
  action-teal: "#155e62"
  button-teal: "#175e61"
  selected-field: "#e2efeb"
  caution-field: "#f3e7c9"
  error-field: "#f8e7e2"
typography:
  display:
    fontFamily: "Manrope, sans-serif"
    fontSize: "clamp(27px, 3vw, 39px)"
    fontWeight: 800
    lineHeight: 1.16
    letterSpacing: "-0.035em"
  body:
    fontFamily: "DM Sans, sans-serif"
    fontSize: "16px"
    fontWeight: 400
    lineHeight: 1.5
  label:
    fontFamily: "DM Sans, sans-serif"
    fontSize: "12px"
    fontWeight: 700
    letterSpacing: "0.095em"
rounded:
  control: "9px"
  artifact: "12px"
spacing:
  compact: "12px"
  section: "27px"
  page-gutter: "48px"
components:
  button-primary:
    backgroundColor: "{colors.button-teal}"
    textColor: "#ffffff"
    rounded: "{rounded.control}"
    padding: "10px 16px"
    height: "44px"
  working-paper:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.ink}"
    rounded: "{rounded.artifact}"
    padding: "22px"
---

# Career Research Desk

Status: selected Impeccable direction for the six-page prototype, 2026-09-23. The user approved the information architecture and directed implementation to proceed. This record governs the prototype and future career-route handoff. The existing root DESIGN.md continues to govern the photo workspace.

## Overview

**Creative North Star: “The Career Research Desk.”** An experienced professional should feel that a careful researcher has laid out evidence, working materials and the next decision in one place. The visual system uses a light document field, quiet rules and a deep teal action mark. It carries six distinct artifacts without turning conversation into the only interface.

**Key Characteristics:**
- Left navigation reads like a table of contents; main content is the current artifact; the assistant reads that same artifact.
- A selected goal or action receives one clear teal mark. Source definitions and unavailable states remain visible.
- Flat papers and rules create hierarchy; photographs stay in the optional Materials area.

## Colors

Canvas and sidebar are distinct light fields. Ink carries the factual hierarchy; teal is reserved for selection and the next action. Caution and error fields are functional states, not decorative highlights.

**The Evidence Color Rule.** Unknown or suppressed evidence never shares the low-value color in charts or maps, and every color-coded measure has a text label.

## Typography

Manrope carries route headings and artifact names. DM Sans carries controls, dense evidence and explanation. A headline is large enough to identify the current task; numbers use tabular alignment when compared. A compact source label does not outrank the figure's definition.

**The Measure Rule.** Long explanations stay readable at roughly 65–75 characters per line; table labels stay plain and scannable.

## Layout

Desktop uses a 236px contents rail, flexible artifact column and 335px contextual assistant. At widths under 980px the rail becomes a navigation menu and the assistant opens as a full-height sheet. At phone widths the map gives way to a first-class market list, the multi-column forms stack, and compare actions stay visible in each row.

**The Artifact Leads Rule.** A specialist page gives its first viewport to the profile, analysis, geography, roadmap or material. The agent panel is persistent help; only the Career agent home leads with conversation and next action.

## Elevation & Depth

The page is flat at rest. Borders and tonal surfaces show grouping. A soft offset shadow appears only for floating navigation or a transient confirmation, never as a permanent card halo.

## Shapes

Controls use gently curved corners; working papers are slightly broader. Active and inactive states are named and bordered, not conveyed only by a fill change. The existing brand logo is preserved without changing its shape.

## Components

- **Primary button:** deep teal field, white text, 44px minimum height and visible amber keyboard focus.
- **Working paper:** quiet pale surface with one thin rule; content and source labels determine its size.
- **Navigation:** six labeled destinations with one solid selected state. Mobile uses the same labels inside a menu.
- **Assistant:** contextual note, scripted example response and plain input; a sheet on phones with Escape and focus return.
- **Evidence table:** source and measure remain readable; mobile rows expose labels and their Compare action without horizontal scrolling.

## Do's and Don'ts

### Do:
- **Do** label every illustrative monetary value and distinguish occupational wages from observed advertised pay.
- **Do** preserve the current brand name and logo while letting career information lead.
- **Do** show reviewable edits, stale results, loading, error, partial and quota states in plain language.

### Don't:
- **Don't** show an overall career grade or precise personal dollar value without a defensible source.
- **Don't** hide actions inside a map polygon or make photos a prerequisite for career work.
- **Don't** treat the photo editor's portrait-first composition as the career layout.
