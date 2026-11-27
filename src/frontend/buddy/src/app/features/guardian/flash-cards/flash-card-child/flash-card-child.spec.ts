import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import {
  ChildFlashCards,
  FlashCardSet,
  FlashCardsService,
} from '../../../../core/flash-cards.service';
import { ChildSummary } from '../../../../core/guardians.service';
import { FlashCardChild, tiersValid } from './flash-card-child';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 4; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function child(id: string, givenName: string): ChildSummary {
  return {
    id,
    name: { givenName, familyName: 'Kid' },
    guardianLinkId: `link-${id}`,
    kind: 'Parent',
    language: 'en',
    timeZoneId: 'UTC',
  } as ChildSummary;
}

function set(id: string, name: string, uniqueCardCount: number): FlashCardSet {
  return {
    id,
    ownerId: 'g1',
    name,
    kind: 'Math',
    math: { operation: 'Multiplication', limit: 7 },
    cards: [],
    uniqueCardCount,
  };
}

const PLUS = set('plus', 'Plus up to 20', 231);
const TABLE = set('table', 'Seven table', 10);

function childData(overrides: Partial<ChildFlashCards> = {}): ChildFlashCards {
  return { childId: 'c1', enabledSets: [], games: [], ...overrides };
}

function setup(
  data: ChildFlashCards | ((childId: string) => ChildFlashCards),
  service: Partial<FlashCardsService> = {},
  sets: FlashCardSet[] = [PLUS, TABLE],
) {
  const getChildFlashCards = vi.fn(async (childId: string) =>
    typeof data === 'function' ? data(childId) : data,
  );
  TestBed.configureTestingModule({
    providers: [{ provide: FlashCardsService, useValue: { getChildFlashCards, ...service } }],
  });
  const fixture = TestBed.createComponent(FlashCardChild);
  fixture.componentRef.setInput('children', [child('c1', 'Emil'), child('c2', 'Ida')]);
  fixture.componentRef.setInput('childId', 'c1');
  fixture.componentRef.setInput('sets', sets);
  const el = fixture.nativeElement as HTMLElement;
  return { fixture, el, getChildFlashCards };
}

function button(root: Element, text: string): HTMLButtonElement {
  const found = Array.from(root.querySelectorAll('button')).find((b) =>
    b.textContent?.trim().startsWith(text),
  );
  expect(found, text).toBeDefined();
  return found!;
}

function type(root: Element, selector: string, value: string): void {
  const input = root.querySelector<HTMLInputElement>(selector)!;
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

function available(el: HTMLElement, setId: string): HTMLElement {
  return el.querySelector<HTMLElement>(`[data-available-set="${setId}"]`)!;
}

function enabled(el: HTMLElement, setId: string): HTMLElement {
  return el.querySelector<HTMLElement>(`[data-enabled-set="${setId}"]`)!;
}

describe('FlashCardChild', () => {
  it('loads the chosen child and names them', async () => {
    const { fixture, el, getChildFlashCards } = setup(childData());
    await settle(fixture);

    expect(getChildFlashCards).toHaveBeenCalledExactlyOnceWith('c1');
    expect(el.textContent).toContain('Flash cards for Emil');
    expect(el.textContent).toContain('No sets switched on yet.');
    expect(el.textContent).toContain('No games finished yet.');
  });

  it('says so when there is no child', async () => {
    const { fixture, el } = setup(childData());
    fixture.componentRef.setInput('children', []);
    fixture.componentRef.setInput('childId', undefined);
    await settle(fixture);

    expect(el.textContent).toContain('No children are linked to you yet.');
  });

  it('shows an error when the child cannot be loaded', async () => {
    const { fixture, el } = setup(childData());
    TestBed.inject(FlashCardsService).getChildFlashCards = vi
      .fn()
      .mockRejectedValue(new Error('x'));
    fixture.componentRef.setInput('childId', 'c2');
    await settle(fixture);

    expect(el.querySelector('[role="alert"]')?.textContent).toContain(
      'Could not load the flash cards.',
    );
  });

  it('loads the other child when the selector changes', async () => {
    const { fixture, el } = setup(childData());
    const emitted: string[] = [];
    fixture.componentInstance.childIdChange.subscribe((id) => emitted.push(id));
    await settle(fixture);

    const select = el.querySelector<HTMLSelectElement>('#flashCardsChildId')!;
    select.value = select.options[1]!.value;
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(emitted).toEqual(['c2']);
  });

  it('switches a set on with the default game length', async () => {
    const enableSet = vi.fn().mockResolvedValue({});
    const { fixture, el, getChildFlashCards } = setup(childData(), { enableSet });
    await settle(fixture);

    button(available(el, 'plus'), 'Switch on').click();
    await settle(fixture);

    expect(enableSet).toHaveBeenCalledExactlyOnceWith('c1', 'plus', [{ cardCount: 5, stars: 1 }]);
    expect(getChildFlashCards).toHaveBeenCalledTimes(2);
  });

  it('edits, adds and removes game lengths before enabling', async () => {
    const enableSet = vi.fn().mockResolvedValue({});
    const { fixture, el } = setup(childData(), { enableSet });
    await settle(fixture);
    const card = () => available(el, 'plus');

    expect(card().querySelector('button.text-red-600')).toBeNull();
    button(card(), 'Add a game length').click();
    await settle(fixture);
    type(card(), '[data-tier-row="1"] input[id^="tierCards"]', '10');
    type(card(), '[data-tier-row="1"] input[id^="tierStars"]', '2');
    button(card(), 'Add a game length').click();
    await settle(fixture);
    expect(card().querySelectorAll('[data-tier-row]')).toHaveLength(3);

    // The third row, the newest, goes.
    Array.from(card().querySelectorAll('button'))
      .filter((b) => b.textContent?.trim() === 'Remove this game length')
      .at(-1)!
      .click();
    await settle(fixture);
    expect(card().querySelectorAll('[data-tier-row]')).toHaveLength(2);

    button(card(), 'Switch on').click();
    await settle(fixture);

    expect(enableSet).toHaveBeenCalledExactlyOnceWith('c1', 'plus', [
      { cardCount: 5, stars: 1 },
      { cardCount: 10, stars: 2 },
    ]);
  });

  it('stops adding at five game lengths', async () => {
    const { fixture, el } = setup(childData());
    await settle(fixture);
    const card = available(el, 'plus');

    for (let i = 0; i < 4; i++) {
      button(card, 'Add a game length').click();
      await settle(fixture);
    }

    expect(card.querySelectorAll('[data-tier-row]')).toHaveLength(5);
    expect(
      Array.from(card.querySelectorAll('button')).some((b) =>
        b.textContent?.includes('Add a game length'),
      ),
    ).toBe(false);
  });

  it('shows the repeat hint only when a game is longer than the set has different cards', async () => {
    const { fixture, el } = setup(childData());
    await settle(fixture);
    const card = available(el, 'table');

    expect(card.querySelector('[data-testid="repeat-hint"]')).toBeNull();

    type(card, 'input[id^="tierCards"]', '10');
    await settle(fixture);
    expect(card.querySelector('[data-testid="repeat-hint"]')).toBeNull();

    type(card, 'input[id^="tierCards"]', '11');
    await settle(fixture);
    expect(card.querySelector('[data-testid="repeat-hint"]')?.textContent).toContain(
      'only has 10 different cards',
    );

    // A big set never repeats at the same length.
    expect(available(el, 'plus').querySelector('[data-testid="repeat-hint"]')).toBeNull();
  });

  it('refuses game lengths outside the limits without calling the API', async () => {
    const enableSet = vi.fn();
    const { fixture, el } = setup(childData(), { enableSet });
    await settle(fixture);
    const card = available(el, 'plus');

    type(card, 'input[id^="tierCards"]', '2');
    button(card, 'Switch on').click();
    await settle(fixture);
    expect(card.querySelector('[role="alert"]')?.textContent).toContain('3 to 50 cards');

    type(card, 'input[id^="tierCards"]', '5');
    type(card, 'input[id^="tierStars"]', '21');
    button(card, 'Switch on').click();
    await settle(fixture);

    expect(enableSet).not.toHaveBeenCalled();
  });

  it('shows enabled sets with their tiers, saves changes with the tier ids, and says Saved', async () => {
    const enableSet = vi.fn().mockResolvedValue({});
    const { fixture, el } = setup(
      childData({
        enabledSets: [
          {
            setId: 'plus',
            setName: 'Plus up to 20',
            tiers: [
              { id: 't1', cardCount: 5, stars: 1 },
              { id: 't2', cardCount: 10, stars: 3 },
            ],
          },
        ],
      }),
      { enableSet },
    );
    await settle(fixture);

    const card = enabled(el, 'plus');
    expect(available(el, 'plus')).toBeNull();
    expect(card.querySelectorAll('[data-tier-row]')).toHaveLength(2);
    expect(card.querySelector<HTMLInputElement>('input[id^="tierStars"]')?.value).toBe('1');

    type(card, '[data-tier-row="1"] input[id^="tierStars"]', '4');
    button(card, 'Save game lengths').click();
    await settle(fixture);

    expect(enableSet).toHaveBeenCalledExactlyOnceWith('c1', 'plus', [
      { id: 't1', cardCount: 5, stars: 1 },
      { id: 't2', cardCount: 10, stars: 4 },
    ]);
    expect(enabled(el, 'plus').textContent).toContain('Saved.');
  });

  it('switches a set off', async () => {
    const disableSet = vi.fn().mockResolvedValue(undefined);
    const { fixture, el, getChildFlashCards } = setup(
      childData({
        enabledSets: [
          {
            setId: 'plus',
            setName: 'Plus up to 20',
            tiers: [{ id: 't1', cardCount: 5, stars: 1 }],
          },
        ],
      }),
      { disableSet },
    );
    await settle(fixture);

    button(enabled(el, 'plus'), 'Switch off').click();
    await settle(fixture);

    expect(disableSet).toHaveBeenCalledExactlyOnceWith('c1', 'plus');
    expect(getChildFlashCards).toHaveBeenCalledTimes(2);
  });

  it('shows an error when switching on or off fails', async () => {
    const enableSet = vi.fn().mockRejectedValue(new Error('boom'));
    const { fixture, el } = setup(childData(), { enableSet });
    await settle(fixture);

    button(available(el, 'plus'), 'Switch on').click();
    await settle(fixture);

    expect(available(el, 'plus').querySelector('[role="alert"]')?.textContent).toContain(
      'Could not save. Try again.',
    );
    expect(el.textContent).not.toContain('Saved.');
  });

  it('lists finished games newest first', async () => {
    const game = (gameId: string, finishedOn: string, knownCount: number) => ({
      gameId,
      finishedOn,
      setId: 'plus',
      setName: 'Plus up to 20',
      cardCount: 10,
      knownCount,
      stars: 2,
    });
    const { fixture, el } = setup(
      childData({
        games: [
          game('g1', '2026-11-01', 7),
          game('g3', '2026-11-20', 9),
          game('g2', '2026-11-10', 8),
        ],
      }),
    );
    await settle(fixture);

    const rows = Array.from(el.querySelectorAll('[data-testid="history"] tbody tr')).map((row) =>
      Array.from(row.querySelectorAll('td')).map((cell) => cell.textContent?.trim()),
    );
    expect(rows).toEqual([
      ['2026-11-20', 'Plus up to 20', '10', '9', '2'],
      ['2026-11-10', 'Plus up to 20', '10', '8', '2'],
      ['2026-11-01', 'Plus up to 20', '10', '7', '2'],
    ]);
    expect(el.textContent).not.toContain('No games finished yet.');
  });
});

describe('tiersValid', () => {
  const row = (cardCount: string, stars: string) => ({ id: undefined, cardCount, stars });

  it('accepts 3 to 50 cards, 1 to 20 stars and 1 to 5 rows', () => {
    expect(tiersValid([row('3', '1')])).toBe(true);
    expect(tiersValid([row('50', '20')])).toBe(true);
    expect(tiersValid(Array.from({ length: 5 }, () => row('5', '1')))).toBe(true);
  });

  it('rejects the rest', () => {
    expect(tiersValid([])).toBe(false);
    expect(tiersValid(Array.from({ length: 6 }, () => row('5', '1')))).toBe(false);
    expect(tiersValid([row('2', '1')])).toBe(false);
    expect(tiersValid([row('51', '1')])).toBe(false);
    expect(tiersValid([row('5', '0')])).toBe(false);
    expect(tiersValid([row('5', '21')])).toBe(false);
    expect(tiersValid([row('', '1')])).toBe(false);
    expect(tiersValid([row('5.5', '1')])).toBe(false);
    expect(tiersValid([row('5', 'x')])).toBe(false);
  });
});
