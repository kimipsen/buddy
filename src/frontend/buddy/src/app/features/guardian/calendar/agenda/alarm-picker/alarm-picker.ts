import { Component, computed, inject, input, output } from '@angular/core';

import { TranslatePipe } from '../../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../../core/i18n/translation.service';

// A calendar item's alarms for subscribed calendar apps: signed minutes from its start or due time
// (negative = before; an all-day item is anchored at local midnight). See
// docs/backend/analysis/calendar-item-alarms.md, Question 6.
export const MAX_ALARMS = 3;

export interface AlarmPreset {
  offset: number;
  labelKey: string;
}

export const TIMED_ALARM_PRESETS: readonly AlarmPreset[] = [
  { offset: 0, labelKey: 'calendar.agenda.alarms.presets.atStart' },
  { offset: -5, labelKey: 'calendar.agenda.alarms.presets.minutes5' },
  { offset: -15, labelKey: 'calendar.agenda.alarms.presets.minutes15' },
  { offset: -30, labelKey: 'calendar.agenda.alarms.presets.minutes30' },
  { offset: -60, labelKey: 'calendar.agenda.alarms.presets.hour1' },
  { offset: -120, labelKey: 'calendar.agenda.alarms.presets.hours2' },
  { offset: -1440, labelKey: 'calendar.agenda.alarms.presets.day1' },
];

export const ALL_DAY_ALARM_PRESETS: readonly AlarmPreset[] = [
  { offset: 540, labelKey: 'calendar.agenda.alarms.presets.onDayAt9' },
  { offset: -360, labelKey: 'calendar.agenda.alarms.presets.dayBeforeAt18' },
  { offset: -900, labelKey: 'calendar.agenda.alarms.presets.dayBeforeAt9' },
  { offset: -2340, labelKey: 'calendar.agenda.alarms.presets.twoDaysBeforeAt9' },
];

export function alarmPresetsFor(isAllDay: boolean): readonly AlarmPreset[] {
  return isAllDay ? ALL_DAY_ALARM_PRESETS : TIMED_ALARM_PRESETS;
}

// When the all-day toggle flips, any alarm that isn't a preset of the new mode is dropped, so the
// form never sends an offset the guardian can't see.
export function keepAlarmPresets(offsets: readonly number[], isAllDay: boolean): number[] {
  const presets = alarmPresetsFor(isAllDay);
  return offsets.filter((offset) => presets.some((preset) => preset.offset === offset));
}

// Order-insensitive, as the backend stores alarms distinct and sorted.
export function sameAlarms(a: readonly number[], b: readonly number[]): boolean {
  const left = [...new Set(a)].sort((x, y) => x - y);
  const right = [...new Set(b)].sort((x, y) => x - y);
  return left.length === right.length && left.every((offset, index) => offset === right[index]);
}

interface AlarmChip {
  offset: number;
  label: string;
  selected: boolean;
}

// Preset chips, up to MAX_ALARMS selected. An offset outside the presets (only possible through the
// API) shows as one extra chip, so editing never silently drops it.
@Component({
  selector: 'app-alarm-picker',
  imports: [TranslatePipe],
  templateUrl: './alarm-picker.html',
})
export class AlarmPicker {
  private readonly translation = inject(TranslationService);

  readonly isAllDay = input(false);
  readonly value = input<readonly number[]>([]);

  readonly valueChange = output<number[]>();

  protected readonly max = MAX_ALARMS;

  protected readonly isFull = computed(() => this.value().length >= MAX_ALARMS);

  protected readonly chips = computed<AlarmChip[]>(() => {
    const selected = this.value();
    const presets = alarmPresetsFor(this.isAllDay());
    const chips = presets.map((preset) => ({
      offset: preset.offset,
      label: this.translation.translate(preset.labelKey),
      selected: selected.includes(preset.offset),
    }));
    const extras = selected
      .filter((offset) => !presets.some((preset) => preset.offset === offset))
      .map((offset) => ({ offset, label: this.customLabel(offset), selected: true }));

    return [...chips, ...extras];
  });

  protected toggle(offset: number): void {
    const selected = this.value();

    if (selected.includes(offset)) {
      this.valueChange.emit(selected.filter((existing) => existing !== offset));
      return;
    }

    if (selected.length >= MAX_ALARMS) {
      return;
    }

    this.valueChange.emit([...selected, offset]);
  }

  // "3 h 20 min before": whole days, hours and minutes of the offset's size.
  private customLabel(offset: number): string {
    const size = Math.abs(offset);
    const parts: [string, number][] = [
      ['calendar.agenda.alarms.custom.days', Math.floor(size / 1440)],
      ['calendar.agenda.alarms.custom.hours', Math.floor((size % 1440) / 60)],
      ['calendar.agenda.alarms.custom.minutes', size % 60],
    ];
    const duration = parts
      .filter(([, count]) => count > 0)
      .map(([key, count]) => this.translation.translate(key, { count }))
      .join(' ');

    return this.translation.translate(
      offset < 0 ? 'calendar.agenda.alarms.custom.before' : 'calendar.agenda.alarms.custom.after',
      { duration },
    );
  }
}
