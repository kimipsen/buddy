import { WritableSignal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { Language } from '../../core/i18n/language';
import { TranslationService } from '../../core/i18n/translation.service';
import { TimeSelect } from './time-select';

describe('TimeSelect', () => {
  interface Setup {
    fixture: ReturnType<typeof TestBed.createComponent<TimeSelect>>;
    compiled: HTMLElement;
    languageState: WritableSignal<Language>;
    onValueChange: ReturnType<typeof vi.fn>;
  }

  async function setup(options: { language?: Language; value?: string } = {}): Promise<Setup> {
    const languageState = signal<Language>(options.language ?? 'en');
    const translationStub: Partial<TranslationService> = { language: languageState.asReadonly() };

    await TestBed.configureTestingModule({
      imports: [TimeSelect],
      providers: [{ provide: TranslationService, useValue: translationStub }]
    }).compileComponents();

    const fixture = TestBed.createComponent(TimeSelect);
    const onValueChange = vi.fn();
    fixture.componentInstance.valueChange.subscribe(onValueChange);

    if (options.value !== undefined) {
      fixture.componentRef.setInput('value', options.value);
    }
    fixture.detectChanges();

    return { fixture, compiled: fixture.nativeElement as HTMLElement, languageState, onValueChange };
  }

  function selects(compiled: HTMLElement): HTMLSelectElement[] {
    return Array.from(compiled.querySelectorAll('select'));
  }

  function optionLabels(select: HTMLSelectElement): string[] {
    return Array.from(select.options).map((option) => option.textContent?.trim() ?? '');
  }

  function selectedLabel(select: HTMLSelectElement): string | undefined {
    return select.selectedOptions[0]?.textContent?.trim();
  }

  function selectByLabel(select: HTMLSelectElement, label: string): void {
    const index = Array.from(select.options).findIndex((option) => option.textContent?.trim() === label);
    expect(index, `option "${label}" not found in select`).toBeGreaterThanOrEqual(0);

    select.selectedIndex = index;
    select.dispatchEvent(new Event('change'));
  }

  // The `effect()` that re-parses `value()` into hour/minute/period doesn't settle within a single
  // synchronous detectChanges() after a later setInput -- see docs/testing.md's zoneless-async note.
  // A macrotask flush reliably drains it.
  async function settle(fixture: Setup['fixture']): Promise<void> {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  describe('rendering by language', () => {
    it('renders 12 unpadded hour options, a minute select, and an AM/PM select in English', async () => {
      const { compiled } = await setup({ language: 'en' });

      const [hourSelect, minuteSelect, periodSelect] = selects(compiled);
      expect(optionLabels(hourSelect)).toEqual(['--', '1', '2', '3', '4', '5', '6', '7', '8', '9', '10', '11', '12']);
      expect(optionLabels(minuteSelect)[1]).toBe('00');
      expect(optionLabels(minuteSelect)).toHaveLength(61);
      expect(optionLabels(periodSelect)).toEqual(['AM', 'PM']);
    });

    it('renders 24 zero-padded hour options and no AM/PM select in Danish', async () => {
      const { compiled } = await setup({ language: 'da' });

      const selectElements = selects(compiled);
      expect(selectElements).toHaveLength(2);

      const [hourSelect, minuteSelect] = selectElements;
      expect(optionLabels(hourSelect)).toEqual([
        '--',
        '00',
        '01',
        '02',
        '03',
        '04',
        '05',
        '06',
        '07',
        '08',
        '09',
        '10',
        '11',
        '12',
        '13',
        '14',
        '15',
        '16',
        '17',
        '18',
        '19',
        '20',
        '21',
        '22',
        '23'
      ]);
      expect(optionLabels(minuteSelect)[59]).toBe('58');
    });

    it('switches the option layout live when the language signal changes', async () => {
      const { fixture, compiled, languageState } = await setup({ language: 'en' });
      expect(selects(compiled)).toHaveLength(3);

      languageState.set('da');
      fixture.detectChanges();

      expect(selects(compiled)).toHaveLength(2);
    });
  });

  describe('default (empty) value', () => {
    it('shows the placeholder in every select and does not emit', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en' });

      const [hourSelect, minuteSelect, periodSelect] = selects(compiled);
      expect(selectedLabel(hourSelect)).toBe('--');
      expect(selectedLabel(minuteSelect)).toBe('--');
      expect(selectedLabel(periodSelect)).toBe('AM');
      expect(onValueChange).not.toHaveBeenCalled();
    });
  });

  describe('parsing an initial value', () => {
    it('splits a 24-hour value into a 12-hour hour/minute/period triple in English', async () => {
      const { compiled } = await setup({ language: 'en', value: '14:30' });

      const [hourSelect, minuteSelect, periodSelect] = selects(compiled);
      expect(selectedLabel(hourSelect)).toBe('2');
      expect(selectedLabel(minuteSelect)).toBe('30');
      expect(selectedLabel(periodSelect)).toBe('PM');
    });

    it('renders midnight ("00:00") as 12 AM', async () => {
      const { compiled } = await setup({ language: 'en', value: '00:00' });

      const [hourSelect, , periodSelect] = selects(compiled);
      expect(selectedLabel(hourSelect)).toBe('12');
      expect(selectedLabel(periodSelect)).toBe('AM');
    });

    it('renders noon ("12:00") as 12 PM', async () => {
      const { compiled } = await setup({ language: 'en', value: '12:00' });

      const [hourSelect, , periodSelect] = selects(compiled);
      expect(selectedLabel(hourSelect)).toBe('12');
      expect(selectedLabel(periodSelect)).toBe('PM');
    });

    it('renders the last minute of the day ("23:59") as 11:59 PM', async () => {
      const { compiled } = await setup({ language: 'en', value: '23:59' });

      const [hourSelect, minuteSelect, periodSelect] = selects(compiled);
      expect(selectedLabel(hourSelect)).toBe('11');
      expect(selectedLabel(minuteSelect)).toBe('59');
      expect(selectedLabel(periodSelect)).toBe('PM');
    });

    it('keeps a 24-hour value as-is in Danish, with no AM/PM conversion', async () => {
      const { compiled } = await setup({ language: 'da', value: '14:30' });

      const [hourSelect, minuteSelect] = selects(compiled);
      expect(selectedLabel(hourSelect)).toBe('14');
      expect(selectedLabel(minuteSelect)).toBe('30');
    });

    it('resets every field to the placeholder when the value input is cleared back to empty', async () => {
      const { fixture, compiled } = await setup({ language: 'en', value: '09:05' });

      fixture.componentRef.setInput('value', '');
      await settle(fixture);

      const [hourSelect, minuteSelect, periodSelect] = selects(compiled);
      expect(selectedLabel(hourSelect)).toBe('--');
      expect(selectedLabel(minuteSelect)).toBe('--');
      expect(selectedLabel(periodSelect)).toBe('AM');
    });

    it('re-parses when the value input changes to a new time', async () => {
      const { fixture, compiled } = await setup({ language: 'en', value: '09:05' });

      fixture.componentRef.setInput('value', '18:45');
      await settle(fixture);

      const [hourSelect, minuteSelect, periodSelect] = selects(compiled);
      expect(selectedLabel(hourSelect)).toBe('6');
      expect(selectedLabel(minuteSelect)).toBe('45');
      expect(selectedLabel(periodSelect)).toBe('PM');
    });

    it('does not throw and does not emit for a malformed initial value', async () => {
      const { onValueChange } = await setup({ language: 'en', value: 'not-a-time' });

      expect(onValueChange).not.toHaveBeenCalled();
    });

    it('ignores a trailing seconds component', async () => {
      const { compiled } = await setup({ language: 'da', value: '14:30:00' });

      const [hourSelect, minuteSelect] = selects(compiled);
      expect(selectedLabel(hourSelect)).toBe('14');
      expect(selectedLabel(minuteSelect)).toBe('30');
    });
  });

  describe('emitting on user interaction', () => {
    it('does not emit after only the hour is picked', async () => {
      const { compiled, onValueChange } = await setup({ language: 'da' });

      const [hourSelect] = selects(compiled);
      selectByLabel(hourSelect, '09');

      expect(onValueChange).not.toHaveBeenCalled();
    });

    it('does not emit after only the minute is picked', async () => {
      const { compiled, onValueChange } = await setup({ language: 'da' });

      const [, minuteSelect] = selects(compiled);
      selectByLabel(minuteSelect, '05');

      expect(onValueChange).not.toHaveBeenCalled();
    });

    it('emits a zero-padded "HH:mm" once both hour and minute are picked in Danish (24-hour)', async () => {
      const { compiled, onValueChange } = await setup({ language: 'da' });

      const [hourSelect, minuteSelect] = selects(compiled);
      selectByLabel(hourSelect, '09');
      selectByLabel(minuteSelect, '05');

      expect(onValueChange).toHaveBeenCalledTimes(1);
      expect(onValueChange).toHaveBeenLastCalledWith('09:05');
    });

    it('converts a 12-hour AM selection to 24-hour on emit', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en' });

      const [hourSelect, minuteSelect] = selects(compiled);
      selectByLabel(hourSelect, '9');
      selectByLabel(minuteSelect, '05');

      expect(onValueChange).toHaveBeenLastCalledWith('09:05');
    });

    it('converts a 12-hour PM selection to 24-hour on emit', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en' });

      const [hourSelect, minuteSelect] = selects(compiled);
      selectByLabel(hourSelect, '9');
      selectByLabel(minuteSelect, '05');
      // period defaults to AM, so flip it to PM explicitly
      const [, , periodSelect] = selects(compiled);
      selectByLabel(periodSelect, 'PM');

      expect(onValueChange).toHaveBeenLastCalledWith('21:05');
    });

    it('emits "00:00" for 12 AM (midnight boundary)', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en' });

      const [hourSelect, minuteSelect] = selects(compiled);
      selectByLabel(hourSelect, '12');
      selectByLabel(minuteSelect, '00');

      expect(onValueChange).toHaveBeenLastCalledWith('00:00');
    });

    it('emits "12:00" for 12 PM (noon boundary)', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en' });

      const [hourSelect, minuteSelect, periodSelect] = selects(compiled);
      selectByLabel(hourSelect, '12');
      selectByLabel(minuteSelect, '00');
      selectByLabel(periodSelect, 'PM');

      expect(onValueChange).toHaveBeenLastCalledWith('12:00');
    });

    it('emits "23:59" for 11:59 PM (end-of-day boundary)', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en' });

      const [hourSelect, minuteSelect, periodSelect] = selects(compiled);
      selectByLabel(hourSelect, '11');
      selectByLabel(minuteSelect, '59');
      selectByLabel(periodSelect, 'PM');

      expect(onValueChange).toHaveBeenLastCalledWith('23:59');
    });

    it('re-emits with the updated period when AM/PM is toggled after a complete selection', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en' });

      const [hourSelect, minuteSelect, periodSelect] = selects(compiled);
      selectByLabel(hourSelect, '7');
      selectByLabel(minuteSelect, '15');
      expect(onValueChange).toHaveBeenLastCalledWith('07:15');

      selectByLabel(periodSelect, 'PM');
      expect(onValueChange).toHaveBeenCalledTimes(2);
      expect(onValueChange).toHaveBeenLastCalledWith('19:15');
    });

    it('re-emits with the updated hour when the hour is changed after a complete selection', async () => {
      const { compiled, onValueChange } = await setup({ language: 'da' });

      const [hourSelect, minuteSelect] = selects(compiled);
      selectByLabel(hourSelect, '09');
      selectByLabel(minuteSelect, '05');
      expect(onValueChange).toHaveBeenLastCalledWith('09:05');

      selectByLabel(hourSelect, '10');
      expect(onValueChange).toHaveBeenCalledTimes(2);
      expect(onValueChange).toHaveBeenLastCalledWith('10:05');
    });

    it('ends up on the correct final value once a fresh selection follows a malformed initial value', async () => {
      // A malformed `value` (e.g. `"garbage"`) is now rejected by `parseHhMm` and treated like an
      // empty value (both hour and minute cleared to `null`), so picking just the hour still
      // doesn't emit -- only once the minute is also picked does a real "HH:mm" go out.
      const { compiled, onValueChange } = await setup({ language: 'da', value: 'garbage' });

      const [hourSelect, minuteSelect] = selects(compiled);
      selectByLabel(hourSelect, '08');
      expect(onValueChange).not.toHaveBeenCalled();

      selectByLabel(minuteSelect, '30');
      expect(onValueChange).toHaveBeenLastCalledWith('08:30');
    });
  });
});
