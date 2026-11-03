---
target: Calendar month picker screenshot
total_score: 22
max_score: 40
na_heuristics: 
p0_count: 0
p1_count: 0
target_identity: "file:D:\\ClassFinance_WPF_App\\ClassFinance\\Resources\\Styles.xaml"
target_fingerprint: "sha256:abe767639c4cd057c3fa3f5fa7f7690ce8a77be54569f2a82edd225aa8f97fe8"
target_path: "D:\\ClassFinance_WPF_App\\ClassFinance\\Resources\\Styles.xaml"
timestamp: 2026-11-03T05-54-02Z
slug: classfinance-resources-styles-xaml
closed: true
---
# Impeccable critique: Calendar month picker

**Design specificity: Weak.** The rounded month tiles fit Class Finance’s utility-focused style, but this view reads as a generic calendar grid.

## Heuristic scores

| Heuristic | Score | Note |
|---|---:|---|
| Visibility of state | 2/4 | Current or selected month is not apparent in the screenshot. |
| Match to real-world ordering | 3/4 | Familiar Jan–Dec order. |
| User control | 3/4 | Month navigation exists; keyboard dismissal and return behavior were not verified. |
| Consistency | 2/4 | Rounded tiles fit, but the grid looks compact. |
| Error prevention | 2/4 | Similar-looking months may be easier to misread. |
| Recognition | 3/4 | All months are visible, but the current month cue is unclear. |
| Efficiency | 2/4 | Month navigation works; alternate keyboard navigation was not confirmed. |
| Aesthetic and minimalist design | 2/4 | Compact, but the spacing and label presentation feel cramped. |
| Error recovery | 2/4 | Recovery from choosing the wrong month was not established. |
| Help and documentation | 1/4 | No contextual guidance is visible; the task itself is simple. |

**Total: 22/40.**

## Cognitive load and emotional journey

Cognitive load is moderate. The familiar Jan–Dec order helps, but the screenshot does not show which month is current or selected. The tight grid makes that distinction harder to scan. This should be a quick, low-stakes choice; unclear selection and subdued labels can make users pause to confirm what they are choosing.

## Strengths

- The 4×3 layout presents all months at once in familiar order.
- Rounded tiles fit the app’s visual language.
- Source code names the previous/next arrows for accessibility.

## Prioritized issues

1. **P2 — Make the current or selected month clear.** The visible tiles look alike, so users may not know which month the calendar is on.
2. **P2 — Improve tile separation.** The grid looks compact; more breathing room would help the months read as distinct choices. The shared style currently sets a 2-pixel margin.
3. **P2 — Check the rendered label contrast.** Labels look subdued in the screenshot, while the source theme specifies a light foreground on a dark tile. The native app view was unavailable for verification, so confirm the effective rendered color before changing the palette.
4. **P3 — Add an explicit keyboard-focus cue.** The custom month-button template defines hover and pressed states, but no explicit focus state.

## Persona red flags

- **Alex, power user:** The 12 visible months are efficient, but cramped targets and unconfirmed keyboard navigation could slow selection.
- **Sam, accessibility-dependent:** The subdued labels and lack of an explicit focus style warrant a rendered contrast and keyboard check.
- **Jordan, first-timer:** A clear current-month cue would reduce uncertainty.

## Minor observations and evidence limits

Impeccable’s detector returned no findings. Its browser preview does not render this native WPF popup; this review uses the user-provided screenshot and source inspection.

## Provocative questions

- Should the picker emphasize the current month, the chosen month, or both?
- Is choosing a month meant to navigate the calendar, or complete the date-filter action?
