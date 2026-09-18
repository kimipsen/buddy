import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { Stepper } from './stepper';

describe('Stepper', () => {
  interface Setup {
    compiled: HTMLElement;
    decrementButton: HTMLButtonElement;
    incrementButton: HTMLButtonElement;
    input: HTMLInputElement;
    onValueChange: ReturnType<typeof vi.fn>;
  }

  async function setup(options: { value: number; min?: number; max?: number | null }): Promise<Setup> {
    await TestBed.configureTestingModule({ imports: [Stepper] }).compileComponents();

    const fixture = TestBed.createComponent(Stepper);
    const onValueChange = vi.fn();
    fixture.componentInstance.valueChange.subscribe(onValueChange);

    fixture.componentRef.setInput('value', options.value);
    if (options.min !== undefined) {
      fixture.componentRef.setInput('min', options.min);
    }
    if (options.max !== undefined) {
      fixture.componentRef.setInput('max', options.max);
    }
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const buttons = compiled.querySelectorAll('button');
    return {
      compiled,
      decrementButton: buttons[0],
      incrementButton: buttons[1],
      input: compiled.querySelector('input')!,
      onValueChange
    };
  }

  it('reflects the value input in the numeric field', async () => {
    const { input } = await setup({ value: 5 });

    expect(input.value).toBe('5');
  });

  it('emits valueChange one step up when the increment button is clicked', async () => {
    const { incrementButton, onValueChange } = await setup({ value: 5 });

    incrementButton.click();

    expect(onValueChange).toHaveBeenCalledTimes(1);
    expect(onValueChange).toHaveBeenLastCalledWith(6);
  });

  it('emits valueChange one step down when the decrement button is clicked', async () => {
    const { decrementButton, onValueChange } = await setup({ value: 5 });

    decrementButton.click();

    expect(onValueChange).toHaveBeenCalledTimes(1);
    expect(onValueChange).toHaveBeenLastCalledWith(4);
  });

  it('disables the decrement button at the min bound', async () => {
    const { decrementButton, onValueChange } = await setup({ value: 1, min: 1 });

    expect(decrementButton.disabled).toBe(true);

    decrementButton.click();

    expect(onValueChange).not.toHaveBeenCalled();
  });

  it('disables the increment button at the max bound', async () => {
    const { incrementButton, onValueChange } = await setup({ value: 10, max: 10 });

    expect(incrementButton.disabled).toBe(true);

    incrementButton.click();

    expect(onValueChange).not.toHaveBeenCalled();
  });
});
