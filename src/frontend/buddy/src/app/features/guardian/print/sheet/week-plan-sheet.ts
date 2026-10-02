import { Component, computed, input } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { WeekPlanModel } from '../week-plan-model';

// Printable area inside 8 mm margins on landscape paper.
export const PAPER_MM = {
  0: { width: 281, height: 194 }, // A4: 297 x 210
  1: { width: 404, height: 281 }, // A3: 420 x 297
} as const;

// A3 is the same layout scaled by sqrt(2), the ratio between the two formats.
const BASE_FONT_PT = 9;
const A3_SCALE = Math.SQRT2;

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
    this.model().paperSize === 1 ? BASE_FONT_PT * A3_SCALE : BASE_FONT_PT,
  );

  // Header row sized to its content, then one fr per height weight, so the sheet always fills
  // exactly one page however many rows the template has.
  protected readonly gridRows = computed(() =>
    ['auto', ...this.model().rows.map((row) => `${row.heightWeight}fr`)].join(' '),
  );
}
