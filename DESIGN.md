# Design — ClipBind PDF homepage

World: **Konbini Receipt.** The homepage is one thermal receipt lying on a shop counter. It itemizes the visitor's clipboard history and totals to "1 PDF". Applies to `site/` only; the desktop app keeps its own Fluent-style system (see `UiKit.cs`).

## Tokens

| Token | Light | Dark | Use |
| --- | --- | --- | --- |
| `--counter` | #e6e4dd | #141516 | Page ground (the counter) |
| `--counter-ink` / `-2` | #3b3c3f / #6a6b6f | #d9d8d3 / #9c9b97 | Text outside the receipt |
| `--paper` | #fbfaf6 | #ecebe3 | Receipt surface (stays paper in dark mode, dimmed) |
| `--paper-edge` | #efede6 | #dedcd2 | Keycaps, image well |
| `--ink` / `--ink-2` | #232427 / #5f6064 | #1f2023 / #55565a | Thermal ink, secondary ink |
| `--rule` | #a9aaad | #9c9d9f | Dashed and dotted rules |
| `--red` / `--red-press` | #c8322a / #a82720 | same | The only spot colour: TOTAL and the download action |
| `--slot` | #2b2c2f | #050505 | Printer slot the receipt feeds from |

## Type

- Display and receipt figures: **DotGothic16** (dot-matrix, JA + Latin). Use it for h1, h2, line items, totals and buttons.
- Body: **BIZ UDGothic** 400/700.
- h1 clamp(29px, 7.2vw, 40px). TOTAL figure clamp(54px, 15vw, 80px), the largest thing on the page. Tabular numerals for all figures.

## Components

- **Receipt**: max 520px, centred. Sections are separated by 2px dashed rules (1px for sub-rules). The bottom edge is a zig-zag tear mask.
- **Line item**: 46px monochrome SVG thumbnail, name with time and pixel size beneath, page number on the right.
- **Leader row**: label, dotted fill, price (`¥0`).
- **Primary action**: full-width red block, dot type, download icon. Exactly one per page.
- **Cut line**: dashed rule with scissors and the label キリトリ / CUT HERE. It separates the first-viewport purchase from the details.
- **Customer copy**: the app screenshot printed in grayscale. It develops to full colour on hover, focus or tap.
- **Barcode foot**: generated bars linking to GitHub, followed by the sign-off.

## Motion

One timeline: the receipt feeds out of the slot (900ms expo-out), then its lines print one by one. Lines below the cut print as they scroll into view, using a scroll-driven animation where supported. Everything is visible without motion, and `prefers-reduced-motion` disables all of it.

## Language

Paired `<span lang="ja">` / `<span lang="en">`. The page shows the language in `data-lang` on `<html>`, chosen from localStorage, then the browser language.

## Rules

- Red appears only on the TOTAL and the download button.
- No cards, no eyebrows, no gradients, no stock icons; icons are authored SVG strokes.
- Example data on the receipt is labelled as an example. Never add user counts, ratings or testimonials; none exist.
