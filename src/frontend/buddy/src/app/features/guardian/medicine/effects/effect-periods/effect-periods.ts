import { Component, computed, inject, input } from '@angular/core';

import { parseIsoDate } from '../../../../../core/date-utils';
import { TranslatePipe } from '../../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../../core/i18n/translation.service';
import { DosePeriod, LevelCount, groupByDosePeriod } from '../../../../../core/medicine-effects';
import { MedicineEffectNote } from '../../../../../core/medicines.service';
import { TimeOfDayPipe } from '../../../../../core/time-of-day.pipe';
import { Card } from '../../../../../shared/card/card';

// Bar segment colours per level, worst to best read left to right as the scale is listed.
const LEVEL_COLOURS: Record<string, string> = {
  Poor: 'bg-red-400 dark:bg-red-500',
  Okay: 'bg-amber-300 dark:bg-amber-500',
  Good: 'bg-emerald-500 dark:bg-emerald-400',
  None: 'bg-slate-300 dark:bg-slate-600',
  Mild: 'bg-amber-200 dark:bg-amber-700',
  Moderate: 'bg-orange-400 dark:bg-orange-500',
  Severe: 'bg-red-500 dark:bg-red-400',
};

interface BarSegment {
  value: string;
  count: number;
  percent: number;
  colour: string;
}

interface PeriodRow {
  period: DosePeriod;
  range: string;
  effects: { key: string; label: string; segments: BarSegment[] }[];
  sideEffects: { key: string; label: string; segments: BarSegment[] }[];
}

// The dose-period summary (docs/backend/analysis/medicine-effect-notes.md, Question 6): one block
// per run of days on the same regimen, each rated item as a stacked bar with its counts written
// out, so it reads (and prints) without a chart library.
@Component({
  selector: 'app-effect-periods',
  imports: [Card, TimeOfDayPipe, TranslatePipe],
  templateUrl: './effect-periods.html',
})
export class EffectPeriods {
  private readonly translation = inject(TranslationService);

  readonly notes = input.required<MedicineEffectNote[]>();

  protected readonly rows = computed((): PeriodRow[] => {
    const t = (key: string, params?: Record<string, string | number>) =>
      this.translation.translate(key, params);
    const locale = this.translation.language();
    const format = (date: string) =>
      parseIsoDate(date).toLocaleDateString(locale, { month: 'short', day: 'numeric' });

    return groupByDosePeriod(this.notes())
      .reverse()
      .map((period) => ({
        period,
        range:
          period.from === period.to
            ? format(period.from)
            : t('medicine.effects.periods.range', {
                from: format(period.from),
                to: format(period.to),
              }),
        effects: period.effects.map(({ area, counts }) => ({
          key: area,
          label: t(`medicine.effects.areas.${area}`),
          segments: this.segments(counts),
        })),
        sideEffects: period.sideEffects.map(({ kind, counts }) => ({
          key: kind,
          label: t(`medicine.effects.sideEffects.${kind}`),
          segments: this.segments(counts),
        })),
      }));
  });

  protected countsText(segments: BarSegment[], scale: 'levels' | 'severities'): string {
    return segments
      .filter((s) => s.count > 0)
      .map(
        (s) => `${this.translation.translate(`medicine.effects.${scale}.${s.value}`)} ${s.count}`,
      )
      .join(', ');
  }

  private segments(counts: LevelCount<string>[]): BarSegment[] {
    const total = counts.reduce((sum, c) => sum + c.count, 0);

    return counts.map(({ value, count }) => ({
      value,
      count,
      percent: total === 0 ? 0 : (count / total) * 100,
      colour: LEVEL_COLOURS[value] ?? '',
    }));
  }
}
