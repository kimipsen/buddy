import { Component, computed, inject, input, output } from '@angular/core';

import { addDaysIso, parseIsoDate } from '../../core/date-utils';
import { TAG_LABEL_KEYS, toMinutes, zoneInfo } from '../../core/feelings';
import type { FeelingCheckIn } from '../../core/feelings.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

// The time axis: 06:00 to 22:00. A check-in or dose outside it sits on the nearest edge.
const AXIS_START = 6 * 60;
const AXIS_END = 22 * 60;
export const AXIS_HOURS = [6, 10, 14, 18, 22];

interface Mark {
  id: string;
  left: number;
  face: string;
  markClass: string;
  selfReport: boolean;
  summary: string;
}

interface Row {
  date: string;
  label: string;
  sleep: { hours: number; minutes: number } | null;
  marks: Mark[];
  doses: { time: string; left: number }[];
}

export function axisPercent(time: string): number {
  const minutes = Math.min(AXIS_END, Math.max(AXIS_START, toMinutes(time)));
  return ((minutes - AXIS_START) / (AXIS_END - AXIS_START)) * 100;
}

/**
 * The 14-day grid (docs/backend/analysis/feelings-check-in.md, Question 7): one row per day, each
 * check-in a face on a time-of-day axis -- filled for the child's own, outlined for an adult's
 * observation -- with taken doses as lines and the previous night's sleep in its own column. A
 * plain table, no chart library; colour is never the only signal (face, shape and a screen-reader
 * summary per mark). The guardian page and the doctor's shared page both draw it; the shared page
 * leaves out sleep and doses.
 */
@Component({
  selector: 'app-feelings-trend',
  imports: [TranslatePipe],
  templateUrl: './feelings-trend.html',
})
export class FeelingsTrend {
  private readonly translation = inject(TranslationService);

  readonly days = input.required<readonly string[]>();
  readonly checkIns = input.required<readonly FeelingCheckIn[]>();
  // Sleep minutes by the night's date (the evening it started); day D shows the night of D-1.
  readonly sleepByNight = input<Readonly<Record<string, number | null>> | null>(null);
  // Taken dose times by date; null leaves the markers out.
  readonly dosesByDate = input<Readonly<Record<string, readonly string[]>> | null>(null);
  readonly selectedDate = input<string | null>(null);
  readonly selectable = input(true);

  readonly selectDate = output<string>();

  // The first and last labels sit inside the axis rather than centred on its edges.
  protected readonly hours = AXIS_HOURS.map((hour, index) => ({
    label: `${String(hour).padStart(2, '0')}:00`,
    left: axisPercent(`${String(hour).padStart(2, '0')}:00`),
    align:
      index === 0
        ? 'translate-x-0'
        : index === AXIS_HOURS.length - 1
          ? '-translate-x-full'
          : '-translate-x-1/2',
  }));

  protected readonly rows = computed((): Row[] => {
    const locale = this.translation.language();
    const sleep = this.sleepByNight();
    const doses = this.dosesByDate();
    const byDate = new Map<string, FeelingCheckIn[]>();

    for (const checkIn of this.checkIns()) {
      byDate.set(checkIn.date, [...(byDate.get(checkIn.date) ?? []), checkIn]);
    }

    return this.days().map((date) => {
      const day = parseIsoDate(date);
      const night = sleep?.[addDaysIso(date, -1)] ?? null;

      return {
        date,
        label: day.toLocaleDateString(locale, { weekday: 'short', day: 'numeric', month: 'short' }),
        sleep: night === null ? null : { hours: Math.floor(night / 60), minutes: night % 60 },
        marks: (byDate.get(date) ?? []).map((checkIn) => this.mark(checkIn)),
        doses: (doses?.[date] ?? []).map((time) => ({ time, left: axisPercent(time) })),
      };
    });
  });

  protected readonly showSleep = computed(() => this.sleepByNight() !== null);

  private mark(checkIn: FeelingCheckIn): Mark {
    const info = zoneInfo(checkIn.zone);
    const tags = checkIn.tags.map((tag) => this.translation.translate(TAG_LABEL_KEYS[tag]));

    return {
      id: checkIn.id,
      left: axisPercent(checkIn.time),
      face: info.face,
      markClass: checkIn.isSelfReport ? info.dotClass : `${info.outlineClass} border-dashed`,
      selfReport: checkIn.isSelfReport,
      summary: this.translation.translate(
        checkIn.isSelfReport
          ? 'feelings.guardian.trend.summarySelf'
          : 'feelings.guardian.trend.summaryObservation',
        {
          time: checkIn.time,
          zone: `${this.translation.translate(info.labelKey)} (${this.translation.translate(info.colourKey)})`,
          tags: tags.length > 0 ? `: ${tags.join(', ')}` : '',
        },
      ),
    };
  }
}
