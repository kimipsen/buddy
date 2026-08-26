import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { Language } from '../../core/i18n/language';
import { TranslationService } from '../../core/i18n/translation.service';

type DatePart = 'day' | 'month' | 'year';

interface DateFormat {
  order: readonly DatePart[];
  separators: readonly [string, string];
}

const DAYS = Array.from({ length: 31 }, (_, day) => day + 1);
const MONTHS = Array.from({ length: 12 }, (_, month) => month + 1);

// Denmark writes dates day-month-year, this app's English locale month-day-year -- native
// <input type="date"> renders whichever order the browser's/OS's own locale prefers, not the
// app's selected language (see TranslationService), so this picker is built from plain <select>s
// instead, the same fix time-select.ts applies to time-of-day input.
const DATE_FORMATS: Record<Language, DateFormat> = {
  da: { order: ['day', 'month', 'year'], separators: ['/', '-'] },
  en: { order: ['month', 'day', 'year'], separators: ['/', '/'] }
};

function pad2(value: number): string {
  return String(value).padStart(2, '0');
}

function daysInMonth(year: number, month: number): number {
  return new Date(year, month, 0).getDate();
}

// `value`/`valueChange` carry a plain "YYYY-MM-DD" string, the same shape the calendar item
// APIs already use. `valueChange` only fires once day, month, and year are all picked, so a
// half-made selection never round-trips back in through `value` as a cleared one.
@Component({
  selector: 'app-date-select',
  imports: [FormsModule],
  templateUrl: './date-select.html'
})
export class DateSelect {
  readonly value = input<string>('');
  readonly valueChange = output<string>();

  private readonly translation = inject(TranslationService);

  protected readonly format = computed(() => DATE_FORMATS[this.translation.language()]);
  protected readonly dayOptions = DAYS;
  protected readonly monthOptions = MONTHS;
  protected readonly pad2 = pad2;

  protected readonly monthNames = computed(() => {
    const formatter = new Intl.DateTimeFormat(this.translation.language(), { month: 'short' });
    return MONTHS.map((month) => formatter.format(new Date(2000, month - 1, 1)));
  });

  protected readonly day = signal<number | null>(null);
  protected readonly month = signal<number | null>(null);
  protected readonly year = signal<number | null>(null);

  constructor() {
    effect(() => this.applyValue(this.value()));
  }

  protected separatorAt(index: number): string {
    return this.format().separators[index] ?? '';
  }

  protected setDay(day: number | null): void {
    this.day.set(day);
    this.emitIfComplete();
  }

  protected setMonth(month: number | null): void {
    this.month.set(month);
    this.emitIfComplete();
  }

  protected setYear(year: number | null): void {
    this.year.set(year);
    this.emitIfComplete();
  }

  private applyValue(value: string): void {
    if (!value) {
      this.day.set(null);
      this.month.set(null);
      this.year.set(null);
      return;
    }

    const [year, month, day] = value.split('-').map(Number);
    this.year.set(year);
    this.month.set(month);
    this.day.set(day);
  }

  private emitIfComplete(): void {
    const day = this.day();
    const month = this.month();
    const year = this.year();

    if (day === null || month === null || year === null) {
      return;
    }

    const clampedDay = Math.min(day, daysInMonth(year, month));
    this.valueChange.emit(`${year}-${pad2(month)}-${pad2(clampedDay)}`);
  }
}
