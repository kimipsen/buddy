import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { DayOfWeek } from '../../core/date-utils';
import { WeekdayPicker } from './weekday-picker';

describe('WeekdayPicker', () => {
  async function setup(value: DayOfWeek[] = []) {
    await TestBed.configureTestingModule({ imports: [WeekdayPicker] }).compileComponents();

    const fixture = TestBed.createComponent(WeekdayPicker);
    const onChange = vi.fn();
    fixture.componentInstance.valueChange.subscribe(onChange);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('ariaLabel', 'Days');
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const buttons = Array.from(compiled.querySelectorAll<HTMLButtonElement>('button'));
    return { compiled, buttons, onChange };
  }

  it('renders seven day chips, Monday first, inside a labelled group', async () => {
    const { compiled, buttons } = await setup();

    expect(compiled.querySelector('[role="group"]')?.getAttribute('aria-label')).toBe('Days');
    expect(buttons).toHaveLength(7);
    expect(buttons[0]?.getAttribute('aria-label')).toBe('Monday');
    expect(buttons[6]?.getAttribute('aria-label')).toBe('Sunday');
  });

  it('marks the selected days as pressed', async () => {
    const { buttons } = await setup(['Tuesday']);

    expect(buttons.map((b) => b.getAttribute('aria-pressed'))).toEqual([
      'false',
      'true',
      'false',
      'false',
      'false',
      'false',
      'false',
    ]);
  });

  it('adds a day in week order when pressed', async () => {
    const { buttons, onChange } = await setup(['Friday']);

    buttons[0]?.click();

    expect(onChange).toHaveBeenCalledWith(['Monday', 'Friday']);
  });

  it('removes a pressed day, down to none (every day)', async () => {
    const { buttons, onChange } = await setup(['Tuesday']);

    buttons[1]?.click();

    expect(onChange).toHaveBeenCalledWith([]);
  });
});
