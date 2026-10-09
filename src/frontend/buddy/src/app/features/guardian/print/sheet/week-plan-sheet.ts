import { Component, computed, input } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { WeekPlanModel } from '../week-plan-model';

// Printable area inside 8 mm margins on landscape paper.
export const PAPER_MM = {
  A4: { width: 281, height: 194 }, // 297 x 210
  A3: { width: 404, height: 281 }, // 420 x 297
} as const;

// A3 is the same layout scaled by sqrt(2), the ratio between the two formats.
const BASE_FONT_PT = 9;
const A3_SCALE = Math.SQRT2;

// A pickup cell is split diagonally, drop-off top-left and pickup bottom-right. A name fits its
// half at full size; a longer label (a playdate's "Playdate: <host>") steps down so its wrapped
// lines stay on its own side of the line.
const PICKUP_LABEL_FULL_SIZE_CHARS = 10;
const PICKUP_LABEL_EM = 1.2;
const PICKUP_LONG_LABEL_EM = 0.9;

// Presentational only: one WeekPlanModel in, one page out, no service calls -- so the print page,
// its on-screen preview and the editor's live preview are the same component. It always renders
// light with its own colors (never theme tokens): paper is white whatever the app theme is.
@Component({
  selector: 'app-week-plan-sheet',
  imports: [TranslatePipe],
  templateUrl: './week-plan-sheet.html',
})
export class WeekPlanSheet {
  readonly model = input.required<WeekPlanModel>();

  protected readonly paper = computed(() => PAPER_MM[this.model().paperSize]);

  protected readonly fontPt = computed(() =>
    this.model().paperSize === 'A3' ? BASE_FONT_PT * A3_SCALE : BASE_FONT_PT,
  );

  protected pickupLabelEm(text: string): number {
    return text.length > PICKUP_LABEL_FULL_SIZE_CHARS ? PICKUP_LONG_LABEL_EM : PICKUP_LABEL_EM;
  }

  // Header row sized to its content, then one fr per height weight, so the sheet always fills
  // exactly one page however many rows the template has.
  protected readonly gridRows = computed(() =>
    ['auto', ...this.model().rows.map((row) => `${row.heightWeight}fr`)].join(' '),
  );
}
