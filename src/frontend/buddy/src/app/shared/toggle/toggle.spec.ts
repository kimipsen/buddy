import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { Toggle } from './toggle';

describe('Toggle', () => {
  interface Setup {
    compiled: HTMLElement;
    button: HTMLButtonElement;
    onCheckedChange: ReturnType<typeof vi.fn>;
  }

  async function setup(options: { checked?: boolean; disabled?: boolean } = {}): Promise<Setup> {
    await TestBed.configureTestingModule({ imports: [Toggle] }).compileComponents();

    const fixture = TestBed.createComponent(Toggle);
    const onCheckedChange = vi.fn();
    fixture.componentInstance.checkedChange.subscribe(onCheckedChange);

    if (options.checked !== undefined) {
      fixture.componentRef.setInput('checked', options.checked);
    }
    if (options.disabled !== undefined) {
      fixture.componentRef.setInput('disabled', options.disabled);
    }
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    return { compiled, button: compiled.querySelector('button')!, onCheckedChange };
  }

  it('renders a switch with aria-checked reflecting the checked input', async () => {
    const { button } = await setup({ checked: true });

    expect(button.getAttribute('role')).toBe('switch');
    expect(button.getAttribute('aria-checked')).toBe('true');
  });

  it('emits checkedChange with the flipped value when clicked', async () => {
    const { button, onCheckedChange } = await setup({ checked: false });

    button.click();

    expect(onCheckedChange).toHaveBeenCalledTimes(1);
    expect(onCheckedChange).toHaveBeenLastCalledWith(true);
  });

  it('does not emit when disabled', async () => {
    const { button, onCheckedChange } = await setup({ checked: false, disabled: true });

    button.click();

    expect(onCheckedChange).not.toHaveBeenCalled();
    expect(button.disabled).toBe(true);
  });
});
