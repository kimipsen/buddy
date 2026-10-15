import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import {
  ALL_DAY_ALARM_PRESETS,
  AlarmPicker,
  TIMED_ALARM_PRESETS,
  alarmPresetsFor,
  keepAlarmPresets,
  sameAlarms,
} from './alarm-picker';

describe('alarm helpers', () => {
  it('uses the doc presets for each mode', () => {
    expect(TIMED_ALARM_PRESETS.map((preset) => preset.offset)).toEqual([
      0, -5, -15, -30, -60, -120, -1440,
    ]);
    expect(ALL_DAY_ALARM_PRESETS.map((preset) => preset.offset)).toEqual([540, -360, -900, -2340]);
    expect(alarmPresetsFor(true)).toBe(ALL_DAY_ALARM_PRESETS);
    expect(alarmPresetsFor(false)).toBe(TIMED_ALARM_PRESETS);
  });

  it('keeps only the presets of the new mode', () => {
    expect(keepAlarmPresets([-120, 540, -200], false)).toEqual([-120]);
    expect(keepAlarmPresets([-120, 540, -200], true)).toEqual([540]);
  });

  it('compares alarm selections regardless of order and repeats', () => {
    expect(sameAlarms([-120, -1440], [-1440, -120])).toBe(true);
    expect(sameAlarms([-120, -120], [-120])).toBe(true);
    expect(sameAlarms([-120], [-120, -60])).toBe(false);
    expect(sameAlarms([-120], [-60])).toBe(false);
    expect(sameAlarms([], [])).toBe(true);
  });
});

describe('AlarmPicker', () => {
  async function render(value: number[], isAllDay = false) {
    await TestBed.configureTestingModule({ imports: [AlarmPicker] }).compileComponents();
    const fixture = TestBed.createComponent(AlarmPicker);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('isAllDay', isAllDay);
    const emitted: number[][] = [];
    fixture.componentInstance.valueChange.subscribe((next) => emitted.push(next));
    fixture.detectChanges();

    const chips = () =>
      Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).map(
        (button) => ({
          button,
          label: button.textContent!.trim(),
          pressed: button.getAttribute('aria-pressed'),
        }),
      );

    return { fixture, emitted, chips };
  }

  it('labels the group and adds the chosen preset', async () => {
    const { fixture, emitted, chips } = await render([-5]);

    expect(
      (fixture.nativeElement as HTMLElement)
        .querySelector('[role="group"]')!
        .getAttribute('aria-label'),
    ).toBe('Alarms');
    chips()
      .find((chip) => chip.label === '1 day before')!
      .button.click();

    expect(emitted).toEqual([[-5, -1440]]);
  });

  it('removes a selected alarm when its chip is pressed again', async () => {
    const { emitted, chips } = await render([-5, -60]);

    chips()
      .find((chip) => chip.label === '5 minutes before')!
      .button.click();

    expect(emitted).toEqual([[-60]]);
  });

  it('adds nothing beyond three alarms', async () => {
    const { emitted, chips } = await render([0, -5, -15]);

    const fourth = chips().find((chip) => chip.label === '30 minutes before')!;
    expect(fourth.button.disabled).toBe(true);
    fourth.button.click();

    expect(emitted).toEqual([]);
  });

  it('labels non-preset offsets by their size and direction', async () => {
    const { chips } = await render([-200, 90, -2900], true);

    expect(chips().map((chip) => chip.label)).toEqual([
      'On the day at 09:00',
      'The day before at 18:00',
      'The day before at 09:00',
      '2 days before at 09:00',
      '3 h 20 min before',
      '1 h 30 min after',
      '2 d 20 min before',
    ]);
    expect(
      chips()
        .slice(-3)
        .every((chip) => chip.pressed === 'true'),
    ).toBe(true);
  });
});
