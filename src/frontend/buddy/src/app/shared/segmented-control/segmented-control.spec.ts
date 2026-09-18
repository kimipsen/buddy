import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { SegmentedControl } from './segmented-control';

describe('SegmentedControl', () => {
  interface Setup {
    compiled: HTMLElement;
    buttons: HTMLButtonElement[];
    onSelectedChange: ReturnType<typeof vi.fn>;
  }

  async function setup(selected: number): Promise<Setup> {
    await TestBed.configureTestingModule({ imports: [SegmentedControl] }).compileComponents();

    const fixture = TestBed.createComponent(SegmentedControl<number>);
    const onSelectedChange = vi.fn();
    fixture.componentInstance.selectedChange.subscribe(onSelectedChange);

    fixture.componentRef.setInput('options', [
      { value: 0, label: 'Guardian' },
      { value: 1, label: 'Self-escort' },
      { value: 2, label: 'Sibling' }
    ]);
    fixture.componentRef.setInput('selected', selected);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    return { compiled, buttons: Array.from(compiled.querySelectorAll('button')), onSelectedChange };
  }

  it('renders one radio button per option', async () => {
    const { buttons } = await setup(0);

    expect(buttons).toHaveLength(3);
    expect(buttons.map((button) => button.textContent?.trim())).toEqual(['Guardian', 'Self-escort', 'Sibling']);
  });

  it('marks the selected option as checked', async () => {
    const { buttons } = await setup(1);

    expect(buttons.map((button) => button.getAttribute('aria-checked'))).toEqual(['false', 'true', 'false']);
  });

  it('emits selectedChange with the clicked option value', async () => {
    const { buttons, onSelectedChange } = await setup(0);

    buttons[2].click();

    expect(onSelectedChange).toHaveBeenCalledTimes(1);
    expect(onSelectedChange).toHaveBeenLastCalledWith(2);
  });
});
