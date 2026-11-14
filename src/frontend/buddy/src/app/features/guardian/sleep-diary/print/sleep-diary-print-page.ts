import { Component, computed, DOCUMENT, effect, inject, resource, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { addDaysIso, parseIsoDate, todayIsoDate } from '../../../../core/date-utils';
import { GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { injectPrintPageStyle } from '../../../../core/print-page-style';
import { SleepDiaryService } from '../../../../core/sleep-diary.service';
import { DateSelect } from '../../../../shared/date-select/date-select';
import { LoadingSpinner } from '../../../../shared/loading-spinner/loading-spinner';
import { SleepDiarySheet } from '../../../../shared/sleep-diary-sheet/sleep-diary-sheet';
import { NIGHTS_SHOWN } from '../sleep-diary';
import { NightDutyCell, SleepNightDuty } from '../sleep-night-duty';
import { isIsoDate, printPeriod } from './print-period';

interface PrintRequest {
  from: string;
  to: string;
}

// The A4 landscape printout of the sleep diary for a chosen period (GitHub issue #97), outside
// GuardianShell like the other printouts so no navigation ends up on paper. The period is kept in
// the URL (?from=&to=), so a reload or a bookmark keeps it.
@Component({
  selector: 'app-sleep-diary-print-page',
  imports: [DateSelect, LoadingSpinner, RouterLink, SleepDiarySheet, TranslatePipe],
  templateUrl: './sleep-diary-print-page.html',
})
export class SleepDiaryPrintPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly guardians = inject(GuardiansService);
  private readonly sleepDiary = inject(SleepDiaryService);
  private readonly sleepNightDuty = inject(SleepNightDuty);
  private readonly translation = inject(TranslationService);
  private readonly pageStyle = injectPrintPageStyle('sleep-diary-print');

  protected readonly childId = this.route.snapshot.paramMap.get('childId') ?? '';

  // A query parameter that isn't a real date falls back to the history's default window.
  private readonly initialTo = this.route.snapshot.queryParamMap.get('to') ?? '';
  private readonly initialFrom = this.route.snapshot.queryParamMap.get('from') ?? '';
  protected readonly to = signal(isIsoDate(this.initialTo) ? this.initialTo : todayIsoDate());
  protected readonly from = signal(
    isIsoDate(this.initialFrom) ? this.initialFrom : addDaysIso(this.to(), -(NIGHTS_SHOWN - 1)),
  );

  protected readonly period = computed(() => printPeriod(this.from(), this.to()));

  private readonly request = computed(
    (): PrintRequest | undefined => {
      const period = this.period();
      return period.kind === 'valid' ? { from: period.from, to: period.to } : undefined;
    },
    { equal: (a, b) => a?.from === b?.from && a?.to === b?.to },
  );

  protected readonly child = resource({
    params: () => this.request(),
    loader: async () => {
      const child = (await this.guardians.listMyChildren()).find((c) => c.id === this.childId);

      if (!child) {
        throw new Error('The child is not one of the guardian’s children.');
      }

      return child;
    },
  });
  protected readonly diary = resource({
    params: () => this.request(),
    loader: ({ params }) => this.sleepDiary.listEntries(this.childId, params.from, params.to),
  });
  // Never errors: null hides the column when the feature is off or night duty can't be read.
  private readonly nightDuty = resource({
    params: () => this.request(),
    loader: ({ params }) => this.sleepNightDuty.load(this.childId, params.from, params.to),
  });

  protected readonly nightDutyByDate = computed(
    (): Partial<Record<string, NightDutyCell>> | null => {
      const data = this.nightDuty.hasValue() ? this.nightDuty.value() : null;
      return data ? this.sleepNightDuty.cells(data) : null;
    },
  );

  // The valid period, all three reads settled, and the child and the diary there.
  protected readonly ready = computed(
    () =>
      this.request() !== undefined &&
      !this.child.isLoading() &&
      !this.diary.isLoading() &&
      !this.nightDuty.isLoading() &&
      this.child.hasValue() &&
      this.diary.hasValue(),
  );
  protected readonly failed = computed(
    () => this.request() !== undefined && (!!this.child.error() || !!this.diary.error()),
  );

  // Formatted in the guardian's language: DatePipe would print English dates, and
  // new Date('YYYY-MM-DD') parses as UTC midnight, the previous day west of UTC.
  protected readonly periodLabel = computed(() => {
    const period = this.period();

    if (period.kind !== 'valid') {
      return '';
    }

    const format = new Intl.DateTimeFormat(this.translation.language(), { dateStyle: 'long' });
    return `${format.format(parseIsoDate(period.from))} – ${format.format(parseIsoDate(period.to))}`;
  });

  protected readonly childName = computed(() => {
    const name = this.child.hasValue() ? this.child.value().name : null;
    return name ? `${name.givenName} ${name.familyName}` : '';
  });

  constructor() {
    effect(() => {
      if (this.ready()) {
        this.pageStyle.apply({ size: 'A4', orientation: 'landscape', margin: '10mm' });
      }
    });
  }

  protected setFrom(date: string): void {
    this.from.set(date);
    void this.router.navigate([], {
      queryParams: { from: date || null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  protected setTo(date: string): void {
    this.to.set(date);
    void this.router.navigate([], {
      queryParams: { to: date || null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  protected print(): void {
    this.document.defaultView?.print();
  }
}
