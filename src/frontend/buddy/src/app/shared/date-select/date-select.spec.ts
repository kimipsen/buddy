import { WritableSignal, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { Language } from '../../core/i18n/language';
import { TranslationService } from '../../core/i18n/translation.service';
import { DateSelect } from './date-select';

describe('DateSelect', () => {
  interface Setup {
    fixture: ReturnType<typeof TestBed.createComponent<DateSelect>>;
    compiled: HTMLElement;
    languageState: WritableSignal<Language>;
    onValueChange: ReturnType<typeof vi.fn>;
  }

  // DateSelect applies its `value` input inside a constructor `effect()`, and this app runs
  // zoneless (no zone.js) -- see docs/testing.md. A plain fixture.detectChanges() right after
  // fixture.componentRef.setInput() schedules that effect but does not necessarily run it before
  // the assertion, so a macrotask flush is needed to reliably observe the parsed day/month/year in
  // the DOM after a `value` change. Interactions driven directly by a DOM event listener (picking a
  // day/month/year, which synchronously calls setDay/setMonth/setYear and emits) don't need this --
  // only assertions that follow a `value` input change do.
  async function settle(fixture: ComponentFixture<unknown>) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  async function setup(options: { language?: Language; value?: string } = {}): Promise<Setup> {
    const languageState = signal<Language>(options.language ?? 'en');
    const translationStub: Partial<TranslationService> = { language: languageState.asReadonly() };

    await TestBed.configureTestingModule({
      imports: [DateSelect],
      providers: [{ provide: TranslationService, useValue: translationStub }]
    }).compileComponents();

    const fixture = TestBed.createComponent(DateSelect);
    const onValueChange = vi.fn();
    fixture.componentInstance.valueChange.subscribe(onValueChange);

    if (options.value !== undefined) {
      fixture.componentRef.setInput('value', options.value);
    }
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, languageState, onValueChange };
  }

  // The day select always has 32 options (a disabled placeholder plus 31 days) and the month
  // select always has 13 (a placeholder plus 12 months), regardless of which order the language
  // renders them in -- so these are identified by option count rather than DOM position.
  function daySelect(compiled: HTMLElement): HTMLSelectElement {
    return Array.from(compiled.querySelectorAll('select')).find((select) => select.options.length === 32)!;
  }

  function monthSelect(compiled: HTMLElement): HTMLSelectElement {
    return Array.from(compiled.querySelectorAll('select')).find((select) => select.options.length === 13)!;
  }

  function yearInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector('input[type="number"]')!;
  }

  function optionLabels(select: HTMLSelectElement): string[] {
    return Array.from(select.options).map((option) => option.textContent?.trim() ?? '');
  }

  function selectedLabel(select: HTMLSelectElement): string | undefined {
    return select.selectedOptions[0]?.textContent?.trim();
  }

  // Index 0 in both the day and month selects is the disabled "--" placeholder, and every option
  // after it is in ascending order (day 1..31, month 1..12), so the day/month value itself is
  // always the option's index.
  function chooseDay(compiled: HTMLElement, day: number): void {
    const select = daySelect(compiled);
    select.selectedIndex = day;
    select.dispatchEvent(new Event('change'));
  }

  function chooseMonth(compiled: HTMLElement, month: number): void {
    const select = monthSelect(compiled);
    select.selectedIndex = month;
    select.dispatchEvent(new Event('change'));
  }

  function typeYear(compiled: HTMLElement, year: number | ''): void {
    const input = yearInput(compiled);
    input.value = String(year);
    input.dispatchEvent(new Event('input'));
  }

  function monthAbbreviations(language: Language): string[] {
    const formatter = new Intl.DateTimeFormat(language, { month: 'short' });
    return Array.from({ length: 12 }, (_, month) => formatter.format(new Date(2000, month, 1)));
  }

  describe('rendering by language', () => {
    it('renders 31 zero-padded day options and 12 localized month options', async () => {
      const { compiled } = await setup({ language: 'en' });

      expect(optionLabels(daySelect(compiled))).toEqual(['--', ...Array.from({ length: 31 }, (_, i) => String(i + 1).padStart(2, '0'))]);
      expect(optionLabels(monthSelect(compiled))).toEqual(['--', ...monthAbbreviations('en')]);
    });

    it('renders day, month, year in that order with "/" and "-" separators in Danish', async () => {
      const { compiled } = await setup({ language: 'da' });

      const children = Array.from(compiled.querySelector('div')!.children);
      expect(children.map((child) => child.tagName)).toEqual(['SELECT', 'SPAN', 'SELECT', 'SPAN', 'INPUT']);
      expect((children[0] as HTMLSelectElement).options).toHaveLength(32); // day
      expect((children[2] as HTMLSelectElement).options).toHaveLength(13); // month
      expect(children[1].textContent).toBe('/');
      expect(children[3].textContent).toBe('-');
    });

    it('renders month, day, year in that order with "/" and "/" separators in English', async () => {
      const { compiled } = await setup({ language: 'en' });

      const children = Array.from(compiled.querySelector('div')!.children);
      expect(children.map((child) => child.tagName)).toEqual(['SELECT', 'SPAN', 'SELECT', 'SPAN', 'INPUT']);
      expect((children[0] as HTMLSelectElement).options).toHaveLength(13); // month
      expect((children[2] as HTMLSelectElement).options).toHaveLength(32); // day
      expect(children[1].textContent).toBe('/');
      expect(children[3].textContent).toBe('/');
    });

    it('switches the field order and month names live when the language signal changes', async () => {
      const { fixture, compiled, languageState } = await setup({ language: 'en', value: '2024-03-05' });

      expect(selectedLabel(monthSelect(compiled))).toBe(monthAbbreviations('en')[2]);

      languageState.set('da');
      fixture.detectChanges();

      const children = Array.from(compiled.querySelector('div')!.children);
      expect((children[0] as HTMLSelectElement).options).toHaveLength(32); // day now comes first
      expect(selectedLabel(monthSelect(compiled))).toBe(monthAbbreviations('da')[2]);
    });
  });

  describe('default (empty) value', () => {
    it('shows the placeholder in the day and month selects and an empty year input, and does not emit', async () => {
      const { compiled, onValueChange } = await setup();

      expect(selectedLabel(daySelect(compiled))).toBe('--');
      expect(selectedLabel(monthSelect(compiled))).toBe('--');
      expect(yearInput(compiled).value).toBe('');
      expect(onValueChange).not.toHaveBeenCalled();
    });
  });

  describe('parsing an initial value', () => {
    it('splits an ISO "YYYY-MM-DD" value into day, month, and year', async () => {
      const { compiled } = await setup({ language: 'en', value: '2024-03-05' });

      expect(selectedLabel(daySelect(compiled))).toBe('05');
      expect(selectedLabel(monthSelect(compiled))).toBe(monthAbbreviations('en')[2]);
      expect(yearInput(compiled).value).toBe('2024');
    });

    it('resets every field to the placeholder when the value input is cleared back to empty', async () => {
      const { fixture, compiled } = await setup({ language: 'en', value: '2024-03-05' });

      fixture.componentRef.setInput('value', '');
      await settle(fixture);

      expect(selectedLabel(daySelect(compiled))).toBe('--');
      expect(selectedLabel(monthSelect(compiled))).toBe('--');
      expect(yearInput(compiled).value).toBe('');
    });

    it('re-parses when the value input changes to a new date', async () => {
      const { fixture, compiled } = await setup({ language: 'en', value: '2024-03-05' });

      fixture.componentRef.setInput('value', '2020-12-25');
      await settle(fixture);

      expect(selectedLabel(daySelect(compiled))).toBe('25');
      expect(selectedLabel(monthSelect(compiled))).toBe(monthAbbreviations('en')[11]);
      expect(yearInput(compiled).value).toBe('2020');
    });

    it('does not emit valueChange when the value input is set programmatically', async () => {
      const { onValueChange } = await setup({ language: 'en', value: '2024-03-05' });

      expect(onValueChange).not.toHaveBeenCalled();
    });

    it('renders a leap-day initial value (Feb 29 in a leap year) correctly', async () => {
      const { compiled } = await setup({ language: 'en', value: '2024-02-29' });

      expect(selectedLabel(daySelect(compiled))).toBe('29');
      expect(selectedLabel(monthSelect(compiled))).toBe(monthAbbreviations('en')[1]);
      expect(yearInput(compiled).value).toBe('2024');
    });

    it('passes an impossible calendar day (e.g. Feb 31) through to the selects unclamped, since applying an external value does not validate it', async () => {
      const { compiled } = await setup({ language: 'en', value: '2023-02-31' });

      expect(selectedLabel(daySelect(compiled))).toBe('31');
      expect(selectedLabel(monthSelect(compiled))).toBe(monthAbbreviations('en')[1]);
      expect(yearInput(compiled).value).toBe('2023');
    });

    it('does not throw and does not emit for a malformed initial value', async () => {
      const { onValueChange } = await setup({ language: 'en', value: 'not-a-date' });

      expect(onValueChange).not.toHaveBeenCalled();
    });

    it('lets a fresh, valid selection recover after a malformed initial value', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en', value: 'garbage' });

      chooseDay(compiled, 8);
      chooseMonth(compiled, 6);
      typeYear(compiled, 2022);

      expect(onValueChange).toHaveBeenLastCalledWith('2022-06-08');
    });
  });

  describe('emitting on user interaction', () => {
    it('does not emit after only the day is picked', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en' });

      chooseDay(compiled, 15);

      expect(onValueChange).not.toHaveBeenCalled();
    });

    it('does not emit after only the month is picked', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en' });

      chooseMonth(compiled, 6);

      expect(onValueChange).not.toHaveBeenCalled();
    });

    it('does not emit after only the year is typed', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en' });

      typeYear(compiled, 2023);

      expect(onValueChange).not.toHaveBeenCalled();
    });

    it('emits a zero-padded ISO date, in day/month/year selection order, once all three parts are picked', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en' });

      chooseDay(compiled, 5);
      expect(onValueChange).not.toHaveBeenCalled();

      chooseMonth(compiled, 3);
      expect(onValueChange).not.toHaveBeenCalled();

      typeYear(compiled, 2023);
      expect(onValueChange).toHaveBeenCalledTimes(1);
      expect(onValueChange).toHaveBeenLastCalledWith('2023-03-05');
    });

    it('emits regardless of which of the three fields is completed last', async () => {
      const { compiled, onValueChange } = await setup({ language: 'da' });

      typeYear(compiled, 2023);
      chooseMonth(compiled, 3);
      chooseDay(compiled, 5);

      expect(onValueChange).toHaveBeenCalledTimes(1);
      expect(onValueChange).toHaveBeenLastCalledWith('2023-03-05');
    });

    it('re-emits with the updated day when the day is changed after a complete selection', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en' });

      chooseDay(compiled, 5);
      chooseMonth(compiled, 3);
      typeYear(compiled, 2023);
      expect(onValueChange).toHaveBeenLastCalledWith('2023-03-05');

      chooseDay(compiled, 20);
      expect(onValueChange).toHaveBeenCalledTimes(2);
      expect(onValueChange).toHaveBeenLastCalledWith('2023-03-20');
    });

    it('re-emits with the updated month when the month is changed after a complete selection', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en' });

      chooseDay(compiled, 5);
      chooseMonth(compiled, 3);
      typeYear(compiled, 2023);
      expect(onValueChange).toHaveBeenLastCalledWith('2023-03-05');

      chooseMonth(compiled, 7);
      expect(onValueChange).toHaveBeenCalledTimes(2);
      expect(onValueChange).toHaveBeenLastCalledWith('2023-07-05');
    });

    it('re-emits with the updated year when the year is changed after a complete selection', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en' });

      chooseDay(compiled, 5);
      chooseMonth(compiled, 3);
      typeYear(compiled, 2023);
      expect(onValueChange).toHaveBeenLastCalledWith('2023-03-05');

      typeYear(compiled, 2030);
      expect(onValueChange).toHaveBeenCalledTimes(2);
      expect(onValueChange).toHaveBeenLastCalledWith('2030-03-05');
    });

    it('clamps the emitted day down when switching to a shorter month (31-day Jan -> 30-day Apr)', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en' });

      chooseDay(compiled, 31);
      chooseMonth(compiled, 1);
      typeYear(compiled, 2023);
      expect(onValueChange).toHaveBeenLastCalledWith('2023-01-31');

      chooseMonth(compiled, 4);
      expect(onValueChange).toHaveBeenLastCalledWith('2023-04-30');
    });

    it('clamps day 31 down to 28 when switching to February in a non-leap year', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en' });

      chooseDay(compiled, 31);
      chooseMonth(compiled, 1);
      typeYear(compiled, 2023);

      chooseMonth(compiled, 2);
      expect(onValueChange).toHaveBeenLastCalledWith('2023-02-28');
    });

    it('clamps day 31 down to 29 (not 28) when switching to February in a leap year', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en' });

      chooseDay(compiled, 31);
      chooseMonth(compiled, 1);
      typeYear(compiled, 2024);

      chooseMonth(compiled, 2);
      expect(onValueChange).toHaveBeenLastCalledWith('2024-02-29');
    });

    it('re-clamps against the new year when the year changes onto/off a leap year for a Feb 29 selection', async () => {
      const { compiled, onValueChange } = await setup({ language: 'en' });

      chooseDay(compiled, 29);
      chooseMonth(compiled, 2);
      typeYear(compiled, 2024);
      expect(onValueChange).toHaveBeenLastCalledWith('2024-02-29');

      typeYear(compiled, 2023);
      expect(onValueChange).toHaveBeenLastCalledWith('2023-02-28');
    });
  });
});
