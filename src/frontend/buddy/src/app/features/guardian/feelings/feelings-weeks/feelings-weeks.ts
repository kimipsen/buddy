import { Component, computed, inject, input } from '@angular/core';

import { addDaysIso, parseIsoDate } from '../../../../core/date-utils';
import { ZONES, ZoneInfo } from '../../../../core/feelings';
import type { FeelingCheckIn } from '../../../../core/feelings.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';

export const WEEKS_SHOWN = 8;

interface WeekRow {
  start: string;
  label: string;
  total: number;
  segments: { zone: ZoneInfo; count: number }[];
}

/**
 * The 8-week summary (Question 7): zone counts per week as a stacked bar of four flex segments,
 * with the counts written out, so the bar is never the only way to read them. Zones have no
 * natural order, so it's a distribution, not a line.
 */
@Component({
  selector: 'app-feelings-weeks',
  imports: [TranslatePipe],
  templateUrl: './feelings-weeks.html',
})
export class FeelingsWeeks {
  private readonly translation = inject(TranslationService);

  // The last day shown; the eight weeks end on it.
  readonly end = input.required<string>();
  readonly checkIns = input.required<readonly FeelingCheckIn[]>();

  protected readonly weeks = computed((): WeekRow[] => {
    const locale = this.translation.language();
    const firstStart = addDaysIso(this.end(), -(WEEKS_SHOWN * 7 - 1));

    return Array.from({ length: WEEKS_SHOWN }, (_, index) => {
      const start = addDaysIso(firstStart, index * 7);
      const end = addDaysIso(start, 6);
      const inWeek = this.checkIns().filter((c) => c.date >= start && c.date <= end);

      return {
        start,
        label: parseIsoDate(start).toLocaleDateString(locale, { day: 'numeric', month: 'short' }),
        total: inWeek.length,
        segments: ZONES.map((zone) => ({
          zone,
          count: inWeek.filter((c) => c.zone === zone.zone).length,
        })),
      };
    });
  });
}
