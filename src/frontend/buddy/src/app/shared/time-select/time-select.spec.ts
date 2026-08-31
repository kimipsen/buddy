import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { TimeSelect } from './time-select';

describe('TimeSelect', () => {
  interface Setup {
    compiled: HTMLElement;
    input: HTMLInputElement;
    onValueChange: ReturnType<typeof vi.fn>;
  }

  async function setup(options: { value?: string } = {}): Promise<Setup> {
    await TestBed.configureTestingModule({ imports: [TimeSelect] }).compileComponents();

    const fixture = TestBed.createComponent(TimeSelect);
    const onValueChange = vi.fn();
    fixture.componentInstance.valueChange.subscribe(onValueChange);

    if (options.value !== undefined) {
      fixture.componentRef.setInput('value', options.value);
    }
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    return { compiled, input: compiled.querySelector('input')!, onValueChange };
  }

  it('renders a native time input', async () => {
    const { input } = await setup();

    expect(input.type).toBe('time');
  });

  it('reflects the value input as the input\'s value', async () => {
    const { input } = await setup({ value: '14:30' });

    expect(input.value).toBe('14:30');
  });

  it('emits valueChange with the "HH:mm" time the user picks', async () => {
    const { input, onValueChange } = await setup();

    input.value = '09:05';
    input.dispatchEvent(new Event('input'));

    expect(onValueChange).toHaveBeenCalledTimes(1);
    expect(onValueChange).toHaveBeenLastCalledWith('09:05');
  });

  it('does not emit valueChange when the value input is set programmatically', async () => {
    const { onValueChange } = await setup({ value: '09:05' });

    expect(onValueChange).not.toHaveBeenCalled();
  });
});
