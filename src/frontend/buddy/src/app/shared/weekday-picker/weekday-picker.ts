import { Component, input, output } from '@angular/core';

import { DayOfWeek } from '../../core/date-utils';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

// Monday first, with the same day labels the agenda's recurrence toggles use.
export const WEEKDAY_CHIPS: readonly { day: DayOfWeek; shortKey: string; nameKey: string }[] = [
  {
    day: 'Monday',
    shortKey: 'calendar.agenda.form.repeat.weekdays.monShort',
    nameKey: 'calendar.agenda.form.repeat.weekdays.mon',
  },
  {
    day: 'Tuesday',
    shortKey: 'calendar.agenda.form.repeat.weekdays.tueShort',
    nameKey: 'calendar.agenda.form.repeat.weekdays.tue',
  },
  {
    day: 'Wednesday',
    shortKey: 'calendar.agenda.form.repeat.weekdays.wedShort',
    nameKey: 'calendar.agenda.form.repeat.weekdays.wed',
  },
  {
    day: 'Thursday',
    shortKey: 'calendar.agenda.form.repeat.weekdays.thuShort',
    nameKey: 'calendar.agenda.form.repeat.weekdays.thu',
  },
  {
    day: 'Friday',
    shortKey: 'calendar.agenda.form.repeat.weekdays.friShort',
    nameKey: 'calendar.agenda.form.repeat.weekdays.fri',
  },
  {
    day: 'Saturday',
    shortKey: 'calendar.agenda.form.repeat.weekdays.satShort',
    nameKey: 'calendar.agenda.form.repeat.weekdays.sat',
  },
  {
    day: 'Sunday',
    shortKey: 'calendar.agenda.form.repeat.weekdays.sunShort',
    nameKey: 'calendar.agenda.form.repeat.weekdays.sun',
  },
];

// The short day label key ("Tue") for showing an item's days as text.
export function weekdayShortKey(day: DayOfWeek): string {
  return WEEKDAY_CHIPS.find((chip) => chip.day === day)?.shortKey ?? day;
}

// Seven pressable day chips for "which days does this apply on". No day pressed means every day
// (a packing-list item that applies whenever the list occurs), so unlike the agenda's recurrence
// toggles an empty selection is valid. The value is kept Monday-first.
@Component({
  selector: 'app-weekday-picker',
  imports: [TranslatePipe],
  templateUrl: './weekday-picker.html',
})
export class WeekdayPicker {
  readonly value = input<DayOfWeek[]>([]);
  readonly ariaLabel = input('');
  // 'lg' for the child's touch targets.
  readonly size = input<'sm' | 'lg'>('sm');
  readonly valueChange = output<DayOfWeek[]>();

  protected readonly days = WEEKDAY_CHIPS;

  protected isOn(day: DayOfWeek): boolean {
    return this.value().includes(day);
  }

  protected toggle(day: DayOfWeek): void {
    const selected = this.isOn(day)
      ? this.value().filter((d) => d !== day)
      : [...this.value(), day];
    this.valueChange.emit(WEEKDAY_CHIPS.map((d) => d.day).filter((d) => selected.includes(d)));
  }
}
