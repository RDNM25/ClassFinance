---
name: Class Finance
description: A clear, utilitarian interface for managing classroom funds and payments.
colors:
  class-finance-red: "#D32F2F"
  deep-red: "#B71C1C"
  pale-red: "#FFEBEE"
  app-background: "#F5F5F7"
  surface: "#FFFFFF"
  card-tint: "#EAE6EF"
  border: "#E0E0E0"
  text: "#212121"
  text-muted: "#5F6368"
  status-green: "#1B5E20"
  status-amber: "#8D4B00"
  transaction-amber: "#FFE0B2"
  dark-app-background: "#18181C"
  dark-surface: "#24242A"
  dark-card-tint: "#2B282F"
  dark-text: "#F3F4F6"
  dark-text-muted: "#B4B4BE"
typography:
  display:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "30px"
    fontWeight: 700
  title:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "24px"
    fontWeight: 700
  body:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "14px"
    fontWeight: 400
  label:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "12px"
    fontWeight: 400
rounded:
  control: "22px"
  button: "24px"
  card: "14px"
  row: "12px"
  compact: "8px"
spacing:
  list-row-gap: "10px"
  row-padding: "16px"
  card-padding: "20px"
components:
  primary-button:
    backgroundColor: "{colors.class-finance-red}"
    textColor: "#FFFFFF"
    rounded: "{rounded.button}"
  card:
    backgroundColor: "{colors.surface}"
    rounded: "{rounded.card}"
---

# Design System: Class Finance

## Overview

**Creative North Star: “The Modern Classroom Ledger”**

The current interface is formal and administrative, designed around clear, utilitarian controls for everyday class-finance work. Class Finance Red identifies primary actions and important financial totals; neutral surfaces keep student, bill, and transaction information easy to scan. The app supports both light and dark themes.

This guide records the existing Windows WPF implementation. It is not a web design specification, and Impeccable Live's browser preview does not directly render this native XAML interface.

**Key Characteristics:**
- Formal and administrative tone
- Red action color on quiet neutral surfaces
- Segoe UI Variable Text throughout
- Rounded controls and compact information rows
- Light and dark theme support

## Colors

The palette pairs a clear red accent with neutral surfaces, using green and amber to distinguish status and date information.

### Primary
- **Class Finance Red** (`#D32F2F`): Primary filled actions and income emphasis in light mode.
- **Deep Red** (`#B71C1C`): Sidebar, text accent, and hover state for primary actions.
- **Pale Red** (`#FFEBEE`): Selected and highlighted control backgrounds in light mode.

### Neutral
- **App Background** (`#F5F5F7`): Main window background in light mode.
- **Surface White** (`#FFFFFF`): Cards, dialogs, and control surfaces in light mode.
- **Card Tint** (`#EAE6EF`): List row background in light mode.
- **Border Gray** (`#E0E0E0`): Control and surface borders.
- **Text Charcoal** (`#212121`): Main text in light mode.
- **Muted Gray** (`#5F6368`): Secondary text in light mode.

### Status and date accents
- **Status Green** (`#1B5E20`): Paid and successful status.
- **Status Amber** (`#8D4B00`): Attention state.
- **Transaction Amber** (`#FFE0B2` background, `#6D3C00` text): Transaction date emphasis.

### Dark theme
- **Night Background** (`#18181C`): Main background.
- **Night Surface** (`#24242A`): Cards and controls.
- **Night Card Tint** (`#2B282F`): List rows.
- **Night Text** (`#F3F4F6`) and **Night Muted Text** (`#B4B4BE`): Primary and secondary text.
- The accent and status colors are brightened for dark backgrounds; retain the theme-specific brush values in `MainWindow.xaml.cs`.

## Typography

**Display Font:** Segoe UI Variable Text (fallback: Segoe UI, sans-serif)
**Body Font:** Segoe UI Variable Text (fallback: Segoe UI, sans-serif)

**Character:** Familiar system typography keeps the interface direct and practical. Bold weights mark page headings, key totals, and action labels.

### Hierarchy
- **Display** (bold, 30px): Primary financial total on the dashboard.
- **Headline** (bold, 26px): Dashboard page title.
- **Title** (bold, 24px): Secondary page titles and login heading.
- **Body** (regular, 14px): Main labels and content.
- **Label** (regular, 12–13px): Supporting details, dates, and field labels.

## Layout

The application is a fixed-window desktop interface with a 1200×750 default size and 900×600 minimum. After login, navigation sits in a left sidebar and the selected page occupies the remaining content area. Dashboard and management screens organize information in cards and vertically scrolling lists. The current layout does not define responsive breakpoints; preserve the minimum window size and scrolling behavior.

Spacing values in shared styles include 16px list-row padding, 20px card padding, and 10px between list rows. Page-level margins vary by screen.

## Elevation & Depth

The surfaces are mostly flat and separated by color and borders. Current cards use a subtle shadow (14px blur, 0.08 opacity, 2px depth); dialogs use a more visible shadow to distinguish the overlay. Keep shadows restrained and reserve stronger elevation for modal layers.

## Shapes

The interface uses rounded rectangles throughout. Shared controls use 22px corners, primary and outline buttons use pill-like 24px corners, cards use 14px corners, list rows use 12px, and compact menus or sidebar items use 8–10px. Focus uses a visible 2px accent outline with a 6px corner radius.

## Components

Controls should feel clear and utilitarian, with color and state carrying meaning.

### Buttons
- **Primary:** Red fill (`#D32F2F`), white bold text, 24px corners, and 18×12px padding; hover deepens to `#B71C1C`.
- **Pop:** White pill with red text for actions placed on the red balance card; hover switches to red fill and white text.
- **Outline:** White surface, red 1.5px outline, and 20px corners.
- **Sidebar:** Transparent rounded rows; active navigation receives the primary red background.
- **Focus:** A 2px accent outline with a 6px corner radius.

### Cards / Containers
- **Cards:** White surface, 14px corners, 20px padding, and a subtle shadow.
- **List rows:** Tinted surface, 12px corners, 16px padding, and 10px vertical gap.
- **Dialogs:** Centered modal surface with 16px corners and stronger shadow than content cards.

### Inputs
- Text and password inputs use white/dark surface fills, rounded 22px corners, and the shared focus outline.
- Combo boxes and date pickers use rounded surfaces and red-tinted selected states.

### Status
- Preserve distinct paid/success, attention, and error colors; pair color with visible status text or icon.

## Do's and Don'ts

- Do keep financial totals, dates, and payment statuses visually distinct.
- Do preserve Indonesian labels and rupiah formatting already used by the app.
- Do retain a usable light and dark theme.
- Do keep primary actions prominent without spreading red across every surface.
- Don't treat this WPF guide as browser implementation instructions; use native XAML resources and control templates for this project.
- Don't use color alone to communicate a payment status.
