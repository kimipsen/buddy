import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { SegmentedControl } from './segmented-control';

describe('SegmentedControl', () => {
  interface Setup {
    compiled: HTMLElement;
    buttons: HTMLButtonElement[];
    onSelectedChange: ReturnType<typeof vi.fn>;
  }

  async function setup(selected: number, wrap?: boolean): Promise<Setup> {
    await TestBed.configureTestingModule({ imports: [SegmentedControl] }).compileComponents();

    const fixture = TestBed.createComponent(SegmentedControl<number>);
    const onSelectedChange = vi.fn();
    fixture.componentInstance.selectedChange.subscribe(onSelectedChange);

    fixture.componentRef.setInput('options', [
      { value: 0, label: 'Guardian' },
      { value: 1, label: 'Self-escort' },
      { value: 2, label: 'Sibling' },
    ]);
    fixture.componentRef.setInput('selected', selected);
    if (wrap !== undefined) {
      fixture.componentRef.setInput('wrap', wrap);
    }
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    return { compiled, buttons: Array.from(compiled.querySelectorAll('button')), onSelectedChange };
  }

  it('renders one joined, clipped bar by default', async () => {
    const { compiled, buttons } = await setup(0);
    const group = compiled.querySelector('[role="radiogroup"]')!;

    expect(group.classList).toContain('overflow-hidden');
    expect(group.classList).not.toContain('flex-wrap');
    expect(buttons[0].classList).toContain('flex-1');
  });

  it('lets the options wrap onto more lines when wrap is set, so none is clipped', async () => {
    const { compiled, buttons } = await setup(1, true);
    const group = compiled.querySelector('[role="radiogroup"]')!;

    expect(group.classList).toContain('flex-wrap');
    expect(group.classList).not.toContain('overflow-hidden');
    expect(buttons[0].classList).toContain('rounded-md');
    expect(buttons[0].classList).toContain('border-slate-300');
    expect(buttons[1].classList).toContain('border-emerald-500');
  });

  // jsdom has no pointer media, so this checks the classes: compact with a mouse, 44px on touch.
  it.each([false, true])(
    'raises every option to a 44px touch target on a touch screen (wrap: %s)',
    async (wrap) => {
      const { buttons } = await setup(0, wrap);

      for (const button of buttons) {
        expect(button.classList).toContain('py-1.5');
        expect(button.classList).toContain('pointer-coarse:min-h-11');
      }
    },
  );

  it('renders one radio button per option', async () => {
    const { buttons } = await setup(0);

    expect(buttons).toHaveLength(3);
    expect(buttons.map((button) => button.textContent?.trim())).toEqual([
      'Guardian',
      'Self-escort',
      'Sibling',
    ]);
  });

  it('marks the selected option as checked', async () => {
    const { buttons } = await setup(1);

    expect(buttons.map((button) => button.getAttribute('aria-checked'))).toEqual([
      'false',
      'true',
      'false',
    ]);
  });

  it('emits selectedChange with the clicked option value', async () => {
    const { buttons, onSelectedChange } = await setup(0);

    buttons[2].click();

    expect(onSelectedChange).toHaveBeenCalledTimes(1);
    expect(onSelectedChange).toHaveBeenLastCalledWith(2);
  });
});
