import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { ColorSwatchPicker, DEFAULT_COLOR_SWATCHES } from './color-swatch-picker';

describe('ColorSwatchPicker', () => {
  interface Setup {
    compiled: HTMLElement;
    buttons: HTMLButtonElement[];
    onValueChange: ReturnType<typeof vi.fn>;
  }

  async function setup(value = ''): Promise<Setup> {
    await TestBed.configureTestingModule({ imports: [ColorSwatchPicker] }).compileComponents();

    const fixture = TestBed.createComponent(ColorSwatchPicker);
    const onValueChange = vi.fn();
    fixture.componentInstance.valueChange.subscribe(onValueChange);

    fixture.componentRef.setInput('value', value);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    return { compiled, buttons: Array.from(compiled.querySelectorAll('button')), onValueChange };
  }

  it('renders one swatch per preset color', async () => {
    const { buttons } = await setup();

    expect(buttons).toHaveLength(DEFAULT_COLOR_SWATCHES.length);
  });

  it('marks the matching preset swatch as checked', async () => {
    const { buttons } = await setup(DEFAULT_COLOR_SWATCHES[2]);

    expect(buttons[2].getAttribute('aria-checked')).toBe('true');
    expect(buttons[0].getAttribute('aria-checked')).toBe('false');
  });

  it('appends the current value as an extra swatch when it is outside the preset list', async () => {
    const { buttons } = await setup('#123456');

    expect(buttons).toHaveLength(DEFAULT_COLOR_SWATCHES.length + 1);
    expect(buttons.at(-1)?.getAttribute('aria-checked')).toBe('true');
  });

  it('emits valueChange with the clicked swatch color', async () => {
    const { buttons, onValueChange } = await setup();

    buttons[1].click();

    expect(onValueChange).toHaveBeenCalledTimes(1);
    expect(onValueChange).toHaveBeenLastCalledWith(DEFAULT_COLOR_SWATCHES[1]);
  });
});
