---
name: pictogram
description: Draw one or more Buddy pictograms as hand-written SVG files that all share one visual style (96x96 tile, fixed palette, rounded 4px outlines, no text), then lint and render them for a visual check. Use when asked to "make a pictogram", "draw an icon for brushing teeth", "generate pictograms for these routine steps/tasks/meals", or when a prompt mentions $pictogram.
---

# Pictogram

Buddy is used by children with ADHD, many of whom don't read yet. A pictogram replaces or sits next to
a word on a task, routine step, meal or board card. It must be recognisable at 48px, in under a
second, by a five-year-old. Every pictogram follows the style guide below, so a set drawn on different
days by different runs still looks like one family.

## Inputs

The caller gives you, per pictogram:

- **slug**: kebab-case English file name, e.g. `brush-teeth`.
- **concept**: what it shows, e.g. "a child brushing their teeth".
- **output directory** (default `src/frontend/buddy/public/pictograms/`).
- optional **notes**: details to include or avoid, or existing pictograms to match.

If a concept is ambiguous, pick the most concrete everyday reading (a child doing it at home) and say
which one you picked in your final message. Don't ask; nobody is there to answer.

## Workflow

1. Read 2-3 existing `*.svg` files in the output directory, if any, and match their proportions
   (head size, line weight, how much of the tile is used).
2. Sketch the composition in words first: the one main subject, at most two supporting objects, the
   view (front or side), what sits where on the 96 grid.
3. Write the SVG by hand following the style guide. Use the template below as the starting point.
4. Lint: `node .agents/skills/pictogram/scripts/check-pictogram.mjs <files...>`. Fix every error.
5. Render: `node .agents/skills/pictogram/scripts/render-pictograms.mjs <out.png> <files...>`. It
   writes a contact sheet with each pictogram at 96px and 48px on a light and a dark background.
   Open the PNG and check: is the concept recognisable at 48px? Are the pictograms the same weight
   as their neighbours? Fix and re-render until yes. Put the PNG in a temp directory, not in the repo.
6. Final message: one line per file (path, what it shows, any interpretation you chose) and the
   contact sheet path.

Only create or change the requested `.svg` files. Don't edit other repo files and don't commit.

## Style guide

### Canvas

- `viewBox="0 0 96 96"`, `width="96" height="96"`.
- First element is the tile: `<rect x="0" y="0" width="96" height="96" rx="20" fill="#F8FAFC"/>`.
  The tile keeps the outline readable on both the light and the dark app theme.
- Artwork stays inside the safe area x/y 10..86. Only ground lines or water may touch 10 or 86.
- The main subject fills roughly 60-75% of the safe area and is optically centred.
- Integer or `.5` coordinates only. Keep paths short; prefer `circle`, `ellipse`, `rect` with `rx`,
  `line`, `polyline`, simple `path`s.

### Line

- Every outline: `stroke="#1F2937"`, `stroke-width="4"`, `stroke-linecap="round"`,
  `stroke-linejoin="round"`. Set these on a `<g>` and let shapes inherit them.
- Interior detail (mouth, fingers, folds, motion lines): `stroke-width="3"`, same colour.
- No other stroke widths, no dashed lines, no hairlines.
- Filled shapes have an outline. Detail lines don't have a fill (`fill="none"`).

### Palette (the only allowed colours)

| Use                                           | Hex       |
| --------------------------------------------- | --------- |
| Outline and detail                            | `#1F2937` |
| Tile                                          | `#F8FAFC` |
| White (teeth, paper, plates, eyes of animals) | `#FFFFFF` |
| Skin                                          | `#F2C094` |
| Hair                                          | `#7C4A2D` |
| Red (stop, hot, apple)                        | `#EF4444` |
| Orange                                        | `#F59E0B` |
| Yellow (sun, light, star)                     | `#FACC15` |
| Green (go, plants, done)                      | `#22C55E` |
| Blue (water, clothing default)                | `#3B82F6` |
| Light blue (sky, glass, cold)                 | `#93C5FD` |
| Purple                                        | `#A855F7` |
| Pink                                          | `#F472B6` |
| Brown (wood, bread, dirt)                     | `#A16207` |
| Grey (metal, stone, tech)                     | `#9CA3AF` |
| Light grey (secondary surfaces)               | `#E5E7EB` |

Flat fills only: no gradients, opacity, shadows, filters, patterns or masks. Use at most four fill
colours besides skin, white and the tile, so the main subject stands out.

### People

- One gender-neutral child unless the concept needs a grown-up (then the grown-up is a head taller).
- Head: a circle about `r="13"`, skin fill. Hair: a simple cap shape over the top of the head.
- Face: two dot eyes (`r="2.5"`, outline colour fill, no stroke) and a curved mouth. Neutral or happy
  unless the concept is a feeling.
- Body: rounded shapes, blue top by default; hands are circles, no fingers unless the action needs them.
- Feelings pictograms (happy, sad, angry, scared, tired, calm) show only a face, filling the safe area,
  with one clear cue each (tears, eyebrows, zzz, ...).

### Meaning

- One concept per pictogram. Show the action with the object (a toothbrush in the mouth, not a
  toothbrush alone), unless the concept is the object itself ("apple", "backpack").
- No letters, digits or words: Buddy is English and Danish, and many children can't read.
- No brands, logos, screens with content, or culture-specific symbols where a neutral one exists.
- Motion: two or three short curved `stroke-width="3"` lines beside the moving part.
- "Don't" / "stop": draw the concept, then a red (`#EF4444`) circle with a diagonal bar on top,
  `stroke-width="6"`, from top-left to bottom-right.
- Time of day: a yellow sun (morning/day) or a yellow crescent moon on a light-blue disc (evening/night)
  in the top-right corner, `r` about 10.

### File

- Root: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 96 96" width="96" height="96" role="img" aria-labelledby="title">`.
- First child: `<title id="title">` with a short English label, e.g. `Brush teeth`.
- Allowed elements: `svg`, `title`, `g`, `rect`, `circle`, `ellipse`, `line`, `polyline`, `polygon`,
  `path`. Nothing else: no `style`, `script`, `image`, `text`, `use`, `defs`, `foreignObject`,
  no `class`/`style`/`on*` attributes, no external references.
- Under 4 KB, two-space indentation, one element per line, file ends with a newline.

## Template

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 96 96" width="96" height="96" role="img" aria-labelledby="title">
  <title id="title">Drink water</title>
  <rect x="0" y="0" width="96" height="96" rx="20" fill="#F8FAFC"/>
  <g stroke="#1F2937" stroke-width="4" stroke-linecap="round" stroke-linejoin="round">
    <path d="M34 24 H62 L58 78 H38 Z" fill="#93C5FD"/>
    <path d="M36 44 H60 L58 78 H38 Z" fill="#3B82F6"/>
    <line x1="42" y1="30" x2="41" y2="38" stroke-width="3"/>
  </g>
</svg>
```
