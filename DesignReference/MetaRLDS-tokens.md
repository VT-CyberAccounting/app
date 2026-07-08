# Meta Horizon OS — Reality Labs Design System (RLDS) tokens

Extracted from the official "Meta Horizon OS UI Set (Community)" Figma export (SVG, June 2026).
Source SVGs kept locally in ~/Downloads (not committed — ~160 MB, mostly embedded raster).
Values read directly from SVG `fill` / `fill-opacity` / `rx` markup (exact, not pixel-sampled).

## Typeface
- **Inter** (open-source, freely shippable → exact match, no substitute needed).
- Type ramp names present: Headline 1/2/3, Subheadline, Body 1 / Body 1 Emphasized, Body 2 / Body 2 Emphasized.
- Exact px sizes/weights NOT captured — text was outlined to paths on export. Pull from the Typography frame's Inspect (or Dev Mode) if precise sizes are needed.

## Accent (blue)
- Primary accent: `#0173EC`
- Light accent (hover/secondary): `#64B5FF`
- (Decision still open: app may keep its existing cyan brand instead of adopting Meta blue.)

## Neutral ramp (dark → light)
`#020202` · `#030303` · `#0A0A0A` · `#0D0D0D` · `#272727` · `#414141` · `#5A5A5A` · `#747474` · `#8E8E8E` · `#C0C0C0` · `#D9D9D9` · `#F2F2F2`
- **Primary surface:** `#272727` (dominant across all components)
- **Secondary surface / border:** `#414141`
- **Primary text:** `#F2F2F2`; secondary text likely `#C0C0C0` / `#747474`

## Semantic colors
- Success green: `#0B8A1B` (darker `#006622`, `#003D1E`; bright `#2AD116`) — heavy presence on Toggle components; possible switch-ON color (CONFIRM).
- Destructive red: `#DD1535` (darker `#AA0A1E`, `#6D020A`; light `#F7818C`, `#EC374E`, `#F45B6B`)
- Warning orange: `#A94302` · `#ED780E` · `#FC9435`
- Accent purple: `#6441D2` · `#9C94F8` · `#5F6ECD`

## Alpha-overlay ramp (state tints)
`fill-opacity` in 5% steps: 0.05, 0.10, 0.15, 0.20, 0.25, 0.30, 0.35, 0.40, 0.45, 0.50, … — overlays for hover/press/disabled over a base color.

## Corner-radius scale
`2 · 4 · 6 · 8 · 12 · 22 · 40 · 60`
- chips/small `4`; cards/buttons `8` and `12`; pill / switch track `22`; circular (knobs, icon buttons) `60`.

## Components present in the UI Set
Buttons (Primary/Secondary/Borderless/Destructive/TextTile/ButtonShelf + icon/circle), Cards (Primary/Secondary/Outlined), Controls (SpatialCheckbox/RadioButton/Switch=Toggle), Dialogs, Dropdowns, Inputs (TextField/SearchBar), SideNavItem, Sliders (L/M/S), Tooltip.

## Derived component geometry (parsed from Components SVG rects — "close")
- Switch track: **44 × 26**, pill radius ~13; knob ~**20** circle, ~3px padding (travel ≈ 21/3).
- Button height: **48**, radius **8**; widths seen 147 / 336.
- Card/tile: **228 × 128**, radius 12.
- Icon: **24** (small 16); touch target **44**; circular buttons 44 (rx22) / 72 (rx36).
- Radius ladder: chip 4, button 8, card 12, pill/switch 13, circular = full.
- Applied to the toggle in DataPanelUI.CreateToggleRow (track 44×26, knob 20). Buttons (height 48 / radius 8) are scene-authored — apply in the editor.

## Type ramp (Inter) — EXACT, read from the Typography spec table SVG (Style | Weight | # | Size | Leading)
- Headline 1 — Bold 700 — 32 / 36
- Headline 2 — Bold 700 — 20 / 24
- Headline 3 — Bold 700 — 17 / 20
- Body 1 Emphasized — Bold 700 — 14 / 20
- Body 1 — **Medium 500 — 14 / 20**  (toggle/list label)
- Body 2 Emphasized — Bold 700 — 12 / 16
- Body 2 — Medium 500 — 12 / 16
- Subheadline — Bold 700 — 12 / 16
- Caption ("Meta") — Regular 400 — 11 / 16
- Weights to import: **Inter-Medium (body) + Inter-Bold (headlines/emphasized)**; Regular only for 11px caption. NOT Regular+SemiBold.

## List / container geometry (parsed from Components SVG rects)
- **List row: 360 × 72**, content area 336 × 48 → **internal padding 12px all around**.
- Card width **384** (heights 136/164/205/340 for multi-line); list column 360.
- 8px spacer/divider elements (360×8 rx4).
- Applied to DataPanelUI.CreateToggleRow: row height 72, label font 16 (Body 1). Panel container width (Meta ~360-384, NOT old 500) + surface #272727 are scene-side.

## Gaps to fill from Inspect (not in SVG)
- Exact type px sizes/weights (text outlined).
- Exact per-component pixel dimensions (switch track/knob size, button height/padding) — many overlapping variants flatten together in the SVG; pull from a single component's Inspect when retuning it.
- Switch ON-state color (green vs blue) — confirm.
