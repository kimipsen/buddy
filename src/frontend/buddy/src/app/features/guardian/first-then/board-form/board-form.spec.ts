import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { FirstThenCard } from '../../../../core/first-then.service';
import { BoardDraft, FirstThenBoardForm } from './board-form';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

interface Inputs {
  recentFirsts?: FirstThenCard[];
  recentThens?: FirstThenCard[];
  todaysTasks?: FirstThenCard[];
  hasCurrentBoard?: boolean;
  busy?: boolean;
}

describe('FirstThenBoardForm', () => {
  async function setup(inputs: Inputs = {}) {
    await TestBed.configureTestingModule({ imports: [FirstThenBoardForm] }).compileComponents();

    const fixture = TestBed.createComponent(FirstThenBoardForm);
    fixture.componentRef.setInput('childName', 'Emil');
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    const submitted: BoardDraft[] = [];
    fixture.componentInstance.submitted.subscribe((draft) => submitted.push(draft));
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, submitted };
  }

  function button(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return [...compiled.querySelectorAll('button')].find((b) => b.textContent?.trim() === text);
  }

  // The suggestion chips inside one picker (by its idPrefix's fieldset).
  function chip(compiled: HTMLElement, side: 0 | 1, title: string): HTMLButtonElement | undefined {
    const fieldset = compiled.querySelectorAll('fieldset')[side];
    return [...fieldset.querySelectorAll<HTMLButtonElement>('button[aria-pressed]')].find((b) =>
      b.textContent?.includes(title),
    );
  }

  function type(compiled: HTMLElement, id: string, value: string): void {
    const input = compiled.querySelector<HTMLInputElement>(`#${id}`)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function groupLabels(compiled: HTMLElement, side: 0 | 1): string[] {
    const fieldset = compiled.querySelectorAll('fieldset')[side];
    return [...fieldset.querySelectorAll('p')].map((p) => p.textContent!.trim());
  }

  it('offers recent, today and starter cards for First, and recent and starters for Then', async () => {
    const { compiled } = await setup({
      recentFirsts: [{ title: 'Piano', icon: '🎹' }],
      recentThens: [{ title: 'Bike ride', icon: '🚲' }],
      todaysTasks: [{ title: 'Maths p. 12', icon: '✏️' }],
    });

    expect(groupLabels(compiled, 0)).toEqual(['Used before', 'From today', 'Suggestions']);
    expect(groupLabels(compiled, 1)).toEqual(['Used before', 'Suggestions']);
    expect(chip(compiled, 0, 'Piano')).toBeDefined();
    expect(chip(compiled, 0, 'Maths p. 12')).toBeDefined();
    expect(chip(compiled, 0, 'Homework')).toBeDefined();
    expect(chip(compiled, 1, 'Bike ride')).toBeDefined();
    expect(chip(compiled, 1, 'iPad')).toBeDefined();
    expect(chip(compiled, 1, 'Homework')).toBeUndefined();
  });

  it('hides empty suggestion groups', async () => {
    const { compiled } = await setup();

    expect(groupLabels(compiled, 0)).toEqual(['Suggestions']);
  });

  it('prefills a card from a chip, with the starter pictogram, and marks it pressed', async () => {
    const { fixture, compiled } = await setup();

    chip(compiled, 0, 'Homework')!.click();
    await settle(fixture);

    expect(compiled.querySelector<HTMLInputElement>('#firstThenFirstTitle')!.value).toBe(
      'Homework',
    );
    // A pictogram replaces the emoji input with its picture.
    expect(compiled.querySelector('#firstThenFirstIcon')).toBeNull();
    expect(compiled.querySelector('img[src="/pictograms/homework.svg"]')).not.toBeNull();
    expect(chip(compiled, 0, 'Homework')!.getAttribute('aria-pressed')).toBe('true');
    expect(chip(compiled, 0, 'Shower')!.getAttribute('aria-pressed')).toBe('false');
  });

  it('keeps Show disabled until both cards have a title and an emoji', async () => {
    const { fixture, compiled } = await setup();
    const show = () => button(compiled, 'Show to Emil')!;

    expect(show().disabled).toBe(true);

    chip(compiled, 0, 'Homework')!.click();
    type(compiled, 'firstThenThenTitle', 'iPad');
    await settle(fixture);
    expect(show().disabled).toBe(true);

    type(compiled, 'firstThenThenIcon', '📱');
    await settle(fixture);
    expect(show().disabled).toBe(false);

    type(compiled, 'firstThenThenTitle', '   ');
    await settle(fixture);
    expect(show().disabled).toBe(true);
  });

  it('emits trimmed cards and the chosen length', async () => {
    const { fixture, compiled, submitted } = await setup();

    type(compiled, 'firstThenFirstTitle', '  Homework ');
    type(compiled, 'firstThenFirstIcon', ' 📚 ');
    chip(compiled, 1, 'iPad')!.click();
    const length = compiled.querySelector<HTMLSelectElement>('#firstThenLength')!;
    length.value = '20';
    length.dispatchEvent(new Event('change'));
    await settle(fixture);

    button(compiled, 'Show to Emil')!.click();
    await settle(fixture);

    expect(submitted).toEqual([
      {
        first: { title: 'Homework', icon: '📚' },
        then: { title: 'iPad', icon: 'pictogram:screen-time' },
        thenMinutes: 20,
      },
    ]);
  });

  it('lists the lengths, no time limit first', async () => {
    const { compiled } = await setup();

    const options = [...compiled.querySelectorAll('#firstThenLength option')].map((o) =>
      o.textContent!.trim(),
    );
    expect(options[0]).toBe('No time limit');
    expect(options.slice(1)).toEqual([
      '5 min',
      '10 min',
      '15 min',
      '20 min',
      '30 min',
      '45 min',
      '60 min',
    ]);
  });

  it('asks before replacing a current board, and cancel goes back', async () => {
    const { fixture, compiled, submitted } = await setup({ hasCurrentBoard: true });

    chip(compiled, 0, 'Homework')!.click();
    chip(compiled, 1, 'iPad')!.click();
    await settle(fixture);
    button(compiled, 'Show to Emil')!.click();
    await settle(fixture);

    expect(submitted).toEqual([]);
    expect(compiled.textContent).toContain('Replace the current board?');

    button(compiled, 'Cancel')!.click();
    await settle(fixture);
    expect(compiled.textContent).not.toContain('Replace the current board?');

    button(compiled, 'Show to Emil')!.click();
    await settle(fixture);
    button(compiled, 'Replace')!.click();
    await settle(fixture);

    expect(submitted).toHaveLength(1);
    expect(compiled.textContent).not.toContain('Replace the current board?');
  });

  it('does not submit while busy', async () => {
    const { fixture, compiled, submitted } = await setup({ busy: true });

    chip(compiled, 0, 'Homework')!.click();
    chip(compiled, 1, 'iPad')!.click();
    await settle(fixture);

    expect(button(compiled, 'Show to Emil')!.disabled).toBe(true);
    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);
    expect(submitted).toEqual([]);
  });

  it('reset empties the form', async () => {
    const { fixture, compiled } = await setup();

    chip(compiled, 0, 'Homework')!.click();
    await settle(fixture);
    fixture.componentInstance.reset();
    await settle(fixture);

    expect(compiled.querySelector<HTMLInputElement>('#firstThenFirstTitle')!.value).toBe('');
    expect(compiled.querySelector<HTMLInputElement>('#firstThenFirstIcon')!.value).toBe('');
  });
});
