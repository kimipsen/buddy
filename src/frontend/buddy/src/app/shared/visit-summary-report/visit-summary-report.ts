import { Component, computed, inject, input } from '@angular/core';

import { parseIsoDate, todayIsoDate } from '../../core/date-utils';
import { TAG_LABEL_KEYS, zoneInfo } from '../../core/feelings';
import type { FeelingZone } from '../../core/feelings.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';
import type { VisitSummaryReport as Report } from '../../core/visit-summaries.service';
import {
  OverviewDay,
  feelingsStats,
  medicineAdherence,
  overviewWeeks,
  periodDays,
  sleepStats,
} from '../../core/visit-summary-stats';
// The medicine page's dose-period summary, reused as is: the report shows exactly what the
// guardian sees there (docs/backend/analysis/doctor-visit-summary.md, Question 3).
import { EffectPeriods } from '../../features/guardian/medicine/effects/effect-periods/effect-periods';

// The colour of a day's sleep box: darker for a longer night, so a short night stands out.
function sleepShade(minutes: number): string {
  if (minutes < 8 * 60) {
    return 'bg-indigo-200 text-indigo-950 dark:bg-indigo-900 dark:text-indigo-100';
  }

  if (minutes < 9.5 * 60) {
    return 'bg-indigo-300 text-indigo-950 dark:bg-indigo-700 dark:text-indigo-50';
  }

  return 'bg-indigo-500 text-white dark:bg-indigo-500 dark:text-white';
}

/**
 * The doctor visit summary (docs/backend/analysis/doctor-visit-summary.md): a header, the
 * guardian's questions, a week-by-week overview, then one block per chosen section. The guardian's
 * page and the doctor's shared page both draw it, and it prints on A4 portrait: every block avoids
 * breaking across pages and colour is never the only signal (counts and words are written out).
 */
@Component({
  selector: 'app-visit-summary-report',
  imports: [EffectPeriods, TranslatePipe],
  templateUrl: './visit-summary-report.html',
})
export class VisitSummaryReport {
  private readonly translation = inject(TranslationService);

  readonly report = input.required<Report>();

  private readonly today = todayIsoDate();

  protected readonly days = computed(() => periodDays(this.report().from, this.report().to));

  protected readonly sleep = computed(() => {
    const section = this.report().sleep;
    return section ? { section, stats: sleepStats(section.entries) } : null;
  });

  protected readonly medicine = computed(() => {
    const section = this.report().medicine;
    return section ? { section, adherence: medicineAdherence(section, this.today) } : null;
  });

  protected readonly feelings = computed(() => {
    const section = this.report().feelings;
    return section ? feelingsStats(section.checkIns) : null;
  });

  protected readonly weekdays = computed(() => {
    const locale = this.translation.language();
    // 2026-03-02 is a Monday.
    return Array.from({ length: 7 }, (_, i) =>
      new Date(2026, 2, 2 + i).toLocaleDateString(locale, { weekday: 'short' }),
    );
  });

  protected readonly weeks = computed(() => {
    const report = this.report();
    return overviewWeeks(
      report.from,
      report.to,
      report.sleep?.entries ?? null,
      report.medicine,
      report.feelings?.checkIns ?? null,
      this.today,
    );
  });

  protected readonly zoneInfo = zoneInfo;
  protected readonly tagLabelKeys = TAG_LABEL_KEYS;
  protected readonly zones: readonly FeelingZone[] = ['Green', 'Blue', 'Yellow', 'Red'];

  protected formatDate(date: string, withYear = true): string {
    return parseIsoDate(date).toLocaleDateString(this.translation.language(), {
      day: 'numeric',
      month: 'short',
      ...(withYear ? { year: 'numeric' } : {}),
    });
  }

  protected printedOn(): string {
    return this.formatDate(this.today);
  }

  protected duration(minutes: number | null): string {
    if (minutes === null) {
      return this.translation.translate('visitSummaries.report.sleep.none');
    }

    return this.translation.translate('visitSummaries.report.sleep.duration', {
      hours: Math.floor(minutes / 60),
      minutes: String(minutes % 60).padStart(2, '0'),
    });
  }

  protected percent(count: number, total: number): number {
    return total === 0 ? 0 : (count / total) * 100;
  }

  protected sleepClass(day: OverviewDay): string {
    return day.sleepMinutes === null
      ? 'bg-slate-100 text-slate-400 dark:bg-slate-800 dark:text-slate-500'
      : sleepShade(day.sleepMinutes);
  }

  protected doseClass(day: OverviewDay): string {
    return day.doses && day.doses.taken === day.doses.due
      ? 'bg-slate-700 text-white dark:bg-slate-300 dark:text-slate-900'
      : 'bg-amber-100 text-amber-950 ring-1 ring-amber-400 dark:bg-amber-500/20 dark:text-amber-100';
  }

  protected dayLabel(day: OverviewDay): string {
    return this.formatDate(day.date, false);
  }

  protected perNight(count: number, nights: number): string {
    return nights === 0 ? '0' : (count / nights).toFixed(1);
  }

  protected times(times: readonly string[]): string {
    return times.map((t) => t.slice(0, 5)).join(', ');
  }
}
