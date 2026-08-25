import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslationService } from '../../core/i18n/translation.service';

type Period = 'AM' | 'PM';

const HOURS_24 = Array.from({ length: 24 }, (_, hour) => hour);
const HOURS_12 = Array.from({ length: 12 }, (_, hour) => hour + 1);
const MINUTES = Array.from({ length: 60 }, (_, minute) => minute);

function to24Hour(hour12: number, period: Period): number {
  const base = hour12 % 12;
  return period === 'PM' ? base + 12 : base;
}

function from12Hour(hour24: number): { hour: number; period: Period } {
  return { hour: hour24 % 12 === 0 ? 12 : hour24 % 12, period: hour24 < 12 ? 'AM' : 'PM' };
}

function pad2(value: number): string {
  return String(value).padStart(2, '0');
}

// Danish uses a 24-hour clock, English a 12-hour one with AM/PM -- native <input type="time">
// renders that split based on the browser's/OS's own locale, not the app's selected language (see
// TranslationService), so this picker is built from plain <select>s instead to guarantee it always
// matches the language chosen in the app, regardless of the browser or OS.
//
// `value`/`valueChange` carry a plain "HH:mm" string (24-hour, no seconds), the same shape the
// pickups and medicine-schedule APIs already use. `valueChange` only fires once both an hour and a
// minute are picked, so a half-made selection (e.g. hour chosen, minute not yet) never round-trips
// back in through `value` as a cleared one.
@Component({
  selector: 'app-time-select',
  imports: [FormsModule],
  templateUrl: './time-select.html'
})
export class TimeSelect {
  readonly value = input<string>('');
  readonly valueChange = output<string>();

  private readonly translation = inject(TranslationService);

  protected readonly is24Hour = computed(() => this.translation.language() === 'da');
  protected readonly hourOptions = computed(() => (this.is24Hour() ? HOURS_24 : HOURS_12));
  protected readonly minuteOptions = MINUTES;
  protected readonly pad2 = pad2;

  protected readonly hour = signal<number | null>(null);
  protected readonly minute = signal<number | null>(null);
  protected readonly period = signal<Period>('AM');

  constructor() {
    effect(() => this.applyValue(this.value()));
  }

  protected setHour(hour: number | null): void {
    this.hour.set(hour);
    this.emitIfComplete();
  }

  protected setMinute(minute: number | null): void {
    this.minute.set(minute);
    this.emitIfComplete();
  }

  protected setPeriod(period: Period): void {
    this.period.set(period);
    this.emitIfComplete();
  }

  private applyValue(value: string): void {
    if (!value) {
      this.hour.set(null);
      this.minute.set(null);
      this.period.set('AM');
      return;
    }

    const [hour24, minute] = value.split(':').map(Number);
    const { hour, period } = this.is24Hour() ? { hour: hour24, period: this.period() } : from12Hour(hour24);

    this.hour.set(hour);
    this.minute.set(minute);
    this.period.set(period);
  }

  private emitIfComplete(): void {
    const hour = this.hour();
    const minute = this.minute();

    if (hour === null || minute === null) {
      return;
    }

    const hour24 = this.is24Hour() ? hour : to24Hour(hour, this.period());
    this.valueChange.emit(`${pad2(hour24)}:${pad2(minute)}`);
  }
}
