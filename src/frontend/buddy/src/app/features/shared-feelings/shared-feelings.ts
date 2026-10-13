import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

import { addDaysIso, buildDateRangeIso, parseIsoDate } from '../../core/date-utils';
import { TAG_LABEL_KEYS, zoneInfo } from '../../core/feelings';
import { FeelingCheckIn, FeelingsService } from '../../core/feelings.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';
import { FeelingsTrend } from '../../shared/feelings-trend/feelings-trend';

const DAYS_SHOWN = 14;

interface Range {
  from?: string;
  to?: string;
}

interface Row {
  date: string;
  label: string;
  checkIns: FeelingCheckIn[];
}

// The page a doctor opens from a share link: no login and no app chrome, the 14-day grid (without
// doses or sleep, which this link doesn't share) and a printable list of every check-in with its
// words and note. The token in the URL is the only credential (GetSharedFeelings on the backend).
@Component({
  selector: 'app-shared-feelings',
  imports: [DatePipe, FeelingsTrend, TranslatePipe],
  templateUrl: './shared-feelings.html',
})
export class SharedFeelings {
  private readonly route = inject(ActivatedRoute);
  private readonly feelings = inject(FeelingsService);
  private readonly translation = inject(TranslationService);

  private readonly token = this.route.snapshot.paramMap.get('token') ?? '';

  // Empty until the reader pages: the server then picks the last 14 days on the child's clock.
  private readonly range = signal<Range>({});

  protected readonly shared = resource({
    params: () => this.range(),
    loader: ({ params }) => this.feelings.getShared(this.token, params.from, params.to),
  });

  // The default window's last day (today on the child's clock): paging forward stops there.
  private readonly newestTo = linkedSignal<string | null, string | null>({
    source: () => {
      const range = this.range();
      return this.shared.hasValue() && range.from === undefined && range.to === undefined
        ? this.shared.value().to
        : null;
    },
    computation: (to, previous) => to ?? previous?.value ?? null,
  });
  protected readonly canGoForward = computed(() => {
    const newest = this.newestTo();
    return this.shared.hasValue() && newest !== null && this.shared.value().to < newest;
  });

  protected readonly notFound = computed(() => {
    const error = this.shared.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });

  protected readonly zoneInfo = zoneInfo;

  protected readonly days = computed((): string[] => {
    if (!this.shared.hasValue()) {
      return [];
    }

    const { from, to } = this.shared.value();
    const count =
      Math.round((parseIsoDate(to).getTime() - parseIsoDate(from).getTime()) / 86_400_000) + 1;
    return buildDateRangeIso(from, count);
  });

  protected readonly rows = computed((): Row[] => {
    const locale = this.translation.language();
    const checkIns = this.shared.hasValue() ? this.shared.value().checkIns : [];

    return this.days().map((date) => ({
      date,
      label: parseIsoDate(date).toLocaleDateString(locale, {
        weekday: 'short',
        day: 'numeric',
        month: 'short',
      }),
      checkIns: checkIns.filter((c) => c.date === date),
    }));
  });

  protected previous(): void {
    if (this.shared.hasValue()) {
      const from = this.shared.value().from;
      this.range.set({ from: addDaysIso(from, -DAYS_SHOWN), to: addDaysIso(from, -1) });
    }
  }

  protected next(): void {
    if (this.canGoForward() && this.shared.hasValue()) {
      const to = this.shared.value().to;
      this.range.set({ from: addDaysIso(to, 1), to: addDaysIso(to, DAYS_SHOWN) });
    }
  }

  protected tagList(checkIn: FeelingCheckIn): string {
    return checkIn.tags.map((tag) => this.translation.translate(TAG_LABEL_KEYS[tag])).join(', ');
  }

  protected print(): void {
    window.print();
  }
}
