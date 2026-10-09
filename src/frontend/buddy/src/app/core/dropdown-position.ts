// Places a `position: fixed` dropdown list (meal picker, task picker) next to its input. Fixed
// lists escape the week tables' `overflow-x-auto` boxes, but they don't move with the page, so
// they have to fit inside the viewport when they open:
// - below the input, unless the list doesn't fit there and there is more room above, then above;
// - capped to the room on that side (at most `maxHeight`), so the list scrolls instead of
//   running off the screen;
// - at least `minWidth` wide (the input's width otherwise), never wider than the viewport minus a
//   margin, and shifted left when it would stick out on the right.

export interface DropdownSize {
  // The list's full height (its `max-h-*` class), in px.
  readonly maxHeight: number;
  // The list's narrowest width (its `min-w-*` class), in px.
  readonly minWidth: number;
}

export interface Viewport {
  readonly width: number;
  readonly height: number;
}

// Gap between input and list, and the margin kept to the viewport's edges, in px.
export const DROPDOWN_GAP = 4;
export const VIEWPORT_MARGIN = 16;
// Never squeeze the list below about three rows, even on a tiny screen.
const MIN_HEIGHT = 96;

export function dropdownPosition(
  anchor: Pick<DOMRect, 'top' | 'bottom' | 'left' | 'width'>,
  size: DropdownSize,
  viewport: Viewport,
): Record<string, string> {
  const roomBelow = viewport.height - anchor.bottom - DROPDOWN_GAP - VIEWPORT_MARGIN;
  const roomAbove = anchor.top - DROPDOWN_GAP - VIEWPORT_MARGIN;
  const flip = roomBelow < size.maxHeight && roomAbove > roomBelow;

  const width = Math.min(
    Math.max(anchor.width, size.minWidth),
    viewport.width - 2 * VIEWPORT_MARGIN,
  );
  const left = Math.max(
    VIEWPORT_MARGIN,
    Math.min(anchor.left, viewport.width - VIEWPORT_MARGIN - width),
  );
  const maxHeight = Math.max(MIN_HEIGHT, Math.min(size.maxHeight, flip ? roomAbove : roomBelow));

  return {
    position: 'fixed',
    // Above the input, the list is anchored by its bottom edge, so a short list hugs the input.
    ...(flip
      ? { bottom: `${viewport.height - anchor.top + DROPDOWN_GAP}px` }
      : { top: `${anchor.bottom + DROPDOWN_GAP}px` }),
    left: `${left}px`,
    width: `${width}px`,
    'max-height': `${maxHeight}px`,
  };
}
