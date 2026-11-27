import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { FlashCardSet, FlashCardsService } from '../../../../core/flash-cards.service';
import { FlashCardLibrary } from './flash-card-library';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function mathSet(overrides: Partial<FlashCardSet> = {}): FlashCardSet {
  return {
    id: 'set-math',
    ownerId: 'g1',
    name: 'Plus up to 20',
    kind: 'Math',
    math: { operation: 'Addition', limit: 20 },
    cards: [],
    uniqueCardCount: 231,
    ...overrides,
  };
}

function textSet(overrides: Partial<FlashCardSet> = {}): FlashCardSet {
  return {
    id: 'set-text',
    ownerId: 'g1',
    name: 'Capitals',
    kind: 'Text',
    math: null,
    cards: [],
    uniqueCardCount: 0,
    ...overrides,
  };
}

function setup(sets: FlashCardSet[], service: Partial<FlashCardsService> = {}) {
  TestBed.configureTestingModule({
    providers: [{ provide: FlashCardsService, useValue: service }],
  });
  const fixture = TestBed.createComponent(FlashCardLibrary);
  fixture.componentRef.setInput('sets', sets);
  const saved: FlashCardSet[] = [];
  const deleted: string[] = [];
  fixture.componentInstance.setSaved.subscribe((set) => saved.push(set));
  fixture.componentInstance.setDeleted.subscribe((id) => deleted.push(id));
  const el = fixture.nativeElement as HTMLElement;
  return { fixture, el, saved, deleted };
}

function button(el: HTMLElement, text: string): HTMLButtonElement {
  const found = Array.from(el.querySelectorAll('button')).find((b) =>
    b.textContent?.trim().startsWith(text),
  );
  expect(found, text).toBeDefined();
  return found!;
}

function type(el: HTMLElement, selector: string, value: string): void {
  const input = el.querySelector<HTMLInputElement>(selector)!;
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

describe('FlashCardLibrary', () => {
  it('shows an empty message with no sets', async () => {
    const { fixture, el } = setup([]);
    await settle(fixture);

    expect(el.textContent).toContain('No card sets yet.');
  });

  it('lists the sets with what each one is', async () => {
    const { fixture, el } = setup([
      mathSet(),
      mathSet({ id: 'm2', name: 'Sevens', math: { operation: 'Multiplication', limit: 7 } }),
      mathSet({ id: 'm3', name: 'Shares', math: { operation: 'Division', limit: 4 } }),
      textSet({
        cards: [{ id: 'c1', question: 'France', answer: 'Paris' }],
        uniqueCardCount: 1,
      }),
    ]);
    await settle(fixture);

    const text = el.textContent ?? '';
    expect(text).toContain('Addition up to 20');
    expect(text).toContain('Multiplication: the 7 table');
    expect(text).toContain('Division by 4');
    expect(text).toContain('1 cards');
  });

  it('creates a maths set from the rule, with a live example card', async () => {
    const created = mathSet({
      id: 'new',
      name: 'Take away',
      math: { operation: 'Subtraction', limit: 10 },
    });
    const createSet = vi.fn().mockResolvedValue(created);
    const { fixture, el, saved } = setup([], { createSet });
    await settle(fixture);

    button(el, 'New set').click();
    await settle(fixture);
    type(el, '#flashSetName', ' Take away ');
    expect(el.querySelector('[data-testid="math-example"]')?.textContent).toContain('10 + 10 = 20');

    el.querySelectorAll<HTMLButtonElement>('button[role="radio"]').forEach((radio) => {
      if (radio.textContent?.trim() === 'Subtraction') {
        radio.click();
      }
    });
    await settle(fixture);
    type(el, '#flashSetLimit', '10');
    await settle(fixture);
    expect(el.querySelector('[data-testid="math-example"]')?.textContent).toContain('10 − 5 = 5');

    button(el, 'Save set').click();
    await settle(fixture);

    expect(createSet).toHaveBeenCalledExactlyOnceWith({
      name: 'Take away',
      kind: 'Math',
      math: { operation: 'Subtraction', limit: 10 },
    });
    expect(saved).toEqual([created]);
    expect(el.querySelector('#flashSetName')).toBeNull();
  });

  it('labels the number for a times table and a divisor', async () => {
    const { fixture, el } = setup([]);
    await settle(fixture);
    button(el, 'New set').click();
    await settle(fixture);
    expect(el.textContent).toContain('Highest number');

    button(el, 'Multiplication').click();
    await settle(fixture);
    expect(el.textContent).toContain('Times table');
    expect(el.querySelector<HTMLInputElement>('#flashSetLimit')?.value).toBe('7');

    button(el, 'Division').click();
    await settle(fixture);
    expect(el.textContent).toContain('Divide by');
  });

  it('creates a text set, then lets cards be added to it', async () => {
    const created = textSet({ id: 'new-text', name: 'Animals' });
    const withCard = {
      ...created,
      cards: [{ id: 'c1', question: 'Cat', answer: 'Kat' }],
      uniqueCardCount: 1,
    };
    const createSet = vi.fn().mockResolvedValue(created);
    const addCard = vi.fn().mockResolvedValue(withCard);
    const sets: FlashCardSet[] = [];
    const { fixture, el, saved } = setup(sets, { createSet, addCard });
    await settle(fixture);

    button(el, 'New set').click();
    await settle(fixture);
    button(el, 'Text').click();
    await settle(fixture);
    expect(el.querySelector('#flashSetLimit')).toBeNull();
    type(el, '#flashSetName', 'Animals');
    button(el, 'Save set').click();
    await settle(fixture);

    expect(createSet).toHaveBeenCalledExactlyOnceWith({ name: 'Animals', kind: 'Text' });
    expect(saved).toEqual([created]);

    // The page puts the new set in the list; the form stays open for its cards.
    fixture.componentRef.setInput('sets', [created]);
    await settle(fixture);
    expect(el.textContent).toContain('No cards in this set yet.');

    button(el, 'Add card').click();
    await settle(fixture);
    type(el, '#flashCardQuestion', ' Cat ');
    type(el, '#flashCardAnswer', 'Kat');
    button(el, 'Save card').click();
    await settle(fixture);

    expect(addCard).toHaveBeenCalledExactlyOnceWith('new-text', { question: 'Cat', answer: 'Kat' });
    expect(saved.at(-1)).toEqual(withCard);

    fixture.componentRef.setInput('sets', [withCard]);
    await settle(fixture);
    expect(el.textContent).toContain('Cat → Kat');
  });

  it('edits and removes a card of a text set', async () => {
    const card = { id: 'c1', question: 'Cat', answer: 'Kat' };
    const set = textSet({ cards: [card], uniqueCardCount: 1 });
    const updated = { ...set, cards: [{ ...card, answer: 'Kat!' }] };
    const updateCard = vi.fn().mockResolvedValue(updated);
    const removeCard = vi.fn().mockResolvedValue(undefined);
    const { fixture, el, saved } = setup([set], { updateCard, removeCard });
    await settle(fixture);

    button(el, 'Edit Capitals').click();
    await settle(fixture);
    button(el, 'Edit card').click();
    await settle(fixture);
    expect(el.querySelector<HTMLInputElement>('#flashCardQuestion')?.value).toBe('Cat');
    type(el, '#flashCardAnswer', 'Kat!');
    button(el, 'Save card').click();
    await settle(fixture);
    expect(updateCard).toHaveBeenCalledExactlyOnceWith('set-text', 'c1', {
      question: 'Cat',
      answer: 'Kat!',
    });
    expect(saved).toEqual([updated]);

    button(el, 'Remove card').click();
    await settle(fixture);
    expect(removeCard).toHaveBeenCalledExactlyOnceWith('set-text', 'c1');
    expect(saved.at(-1)).toEqual({ ...set, cards: [], uniqueCardCount: 0 });
  });

  it('saves an edited maths set under its id and keeps the kind fixed', async () => {
    const set = mathSet();
    const updated = { ...set, name: 'Plus', math: { operation: 'Addition' as const, limit: 30 } };
    const updateSet = vi.fn().mockResolvedValue(updated);
    const { fixture, el, saved } = setup([set], { updateSet });
    await settle(fixture);

    button(el, 'Edit Plus up to 20').click();
    await settle(fixture);
    expect(el.textContent).not.toContain('Kind of set');
    type(el, '#flashSetName', 'Plus');
    type(el, '#flashSetLimit', '30');
    button(el, 'Save set').click();
    await settle(fixture);

    expect(updateSet).toHaveBeenCalledExactlyOnceWith('set-math', {
      name: 'Plus',
      math: { operation: 'Addition', limit: 30 },
    });
    expect(saved).toEqual([updated]);
    expect(el.querySelector('#flashSetName')).toBeNull();
  });

  it('rejects a blank name and an out-of-range number without calling the API', async () => {
    const createSet = vi.fn();
    const { fixture, el } = setup([], { createSet });
    await settle(fixture);

    button(el, 'New set').click();
    await settle(fixture);
    button(el, 'Save set').click();
    await settle(fixture);
    expect(el.querySelector('[role="alert"]')?.textContent).toContain('Could not save');

    type(el, '#flashSetName', 'Big');
    type(el, '#flashSetLimit', '101');
    button(el, 'Save set').click();
    await settle(fixture);

    expect(createSet).not.toHaveBeenCalled();
    expect(el.querySelector('[role="alert"]')).not.toBeNull();
  });

  it('shows an error and keeps the form when saving fails', async () => {
    const createSet = vi.fn().mockRejectedValue(new Error('boom'));
    const { fixture, el, saved } = setup([], { createSet });
    await settle(fixture);

    button(el, 'New set').click();
    await settle(fixture);
    type(el, '#flashSetName', 'X');
    button(el, 'Save set').click();
    await settle(fixture);

    expect(saved).toEqual([]);
    expect(el.querySelector('#flashSetName')).not.toBeNull();
    expect(el.querySelector('[role="alert"]')?.textContent).toContain('Could not save. Try again.');
  });

  it('asks before deleting, and cancel deletes nothing', async () => {
    const deleteSet = vi.fn().mockResolvedValue(undefined);
    const { fixture, el, deleted } = setup([mathSet()], { deleteSet });
    await settle(fixture);

    button(el, 'Delete Plus up to 20').click();
    await settle(fixture);
    expect(el.textContent).toContain('Delete the set “Plus up to 20”?');

    button(el, 'Cancel').click();
    await settle(fixture);
    expect(el.textContent).not.toContain('Delete the set');
    expect(deleteSet).not.toHaveBeenCalled();

    button(el, 'Delete Plus up to 20').click();
    await settle(fixture);
    const confirm = Array.from(el.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === 'Delete',
    )!;
    confirm.click();
    await settle(fixture);

    expect(deleteSet).toHaveBeenCalledExactlyOnceWith('set-math');
    expect(deleted).toEqual(['set-math']);
  });

  it('keeps the set when deleting fails', async () => {
    const deleteSet = vi.fn().mockRejectedValue(new Error('boom'));
    const { fixture, el, deleted } = setup([mathSet()], { deleteSet });
    await settle(fixture);

    button(el, 'Delete Plus up to 20').click();
    await settle(fixture);
    Array.from(el.querySelectorAll('button'))
      .find((b) => b.textContent?.trim() === 'Delete')!
      .click();
    await settle(fixture);

    expect(deleted).toEqual([]);
    expect(el.textContent).toContain('Could not save. Try again.');
  });
});
