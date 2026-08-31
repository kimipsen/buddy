import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { DateSelect } from './date-select';

describe('DateSelect', () => {
  interface Setup {
    compiled: HTMLElement;
    input: HTMLInputElement;
    onValueChange: ReturnType<typeof vi.fn>;
  }

  async function setup(options: { value?: string } = {}): Promise<Setup> {
    await TestBed.configureTestingModule({ imports: [DateSelect] }).compileComponents();

    const fixture = TestBed.createComponent(DateSelect);
    const onValueChange = vi.fn();
    fixture.componentInstance.valueChange.subscribe(onValueChange);

    if (options.value !== undefined) {
      fixture.componentRef.setInput('value', options.value);
    }
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    return { compiled, input: compiled.querySelector('input')!, onValueChange };
  }

  it('renders a native date input', async () => {
    const { input } = await setup();

    expect(input.type).toBe('date');
  });

  it('reflects the value input as the input\'s value', async () => {
    const { input } = await setup({ value: '2024-03-05' });

    expect(input.value).toBe('2024-03-05');
  });

  it('emits valueChange with the ISO date the user picks', async () => {
    const { input, onValueChange } = await setup();

    input.value = '2023-06-08';
    input.dispatchEvent(new Event('input'));

    expect(onValueChange).toHaveBeenCalledTimes(1);
    expect(onValueChange).toHaveBeenLastCalledWith('2023-06-08');
  });

  it('does not emit valueChange when the value input is set programmatically', async () => {
    const { onValueChange } = await setup({ value: '2024-03-05' });

    expect(onValueChange).not.toHaveBeenCalled();
  });
});
