import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  FinishFlashCardGameResponse,
  FlashCardsService,
  MyFlashCardSet,
} from '../../../core/flash-cards.service';
import { ChildFlashCards } from './child-flash-cards';

describe('ChildFlashCards', () => {
  const sums: MyFlashCardSet = {
    setId: 'set-1',
    setName: 'Sums',
    kind: 'Math',
    uniqueCardCount: 20,
    tiers: [
      { id: 'tier-3', cardCount: 3, stars: 1 },
      { id: 'tier-5', cardCount: 5, stars: 2 },
    ],
  };
  const words: MyFlashCardSet = { ...sums, setId: 'set-2', setName: 'Words', kind: 'Text' };

  const cards = [
    { key: 'a', question: '1 + 1', answer: '2', math: null },
    { key: 'b', question: '2 + 2', answer: '4', math: null },
  ];

  const finished = (stars: number, known = 1): FinishFlashCardGameResponse => ({
    stars,
    totalStars: 7,
    game: {
      gameId: 'g',
      finishedOn: '2026-11-27',
      setId: 'set-1',
      setName: 'Sums',
      cardCount: 2,
      knownCount: known,
      stars,
    },
  });

  let service: {
    listMySets: ReturnType<typeof vi.fn>;
    getDeck: ReturnType<typeof vi.fn>;
    finishGame: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    service = {
      listMySets: vi.fn(async () => [sums]),
      getDeck: vi.fn(async () => ({ cards })),
      finishGame: vi.fn(async () => finished(1)),
    };
  });

  afterEach(() => vi.restoreAllMocks());

  async function settle(fixture: ComponentFixture<unknown>) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  async function setup() {
    await TestBed.configureTestingModule({
      imports: [ChildFlashCards],
      providers: [provideRouter([]), { provide: FlashCardsService, useValue: service }],
    }).compileComponents();

    const fixture = TestBed.createComponent(ChildFlashCards);
    await settle(fixture);
    return fixture;
  }

  const root = (f: ComponentFixture<unknown>) => f.nativeElement as HTMLElement;
  const testId = (f: ComponentFixture<unknown>, id: string) =>
    root(f).querySelector<HTMLElement>(`[data-testid="${id}"]`);
  const button = (f: ComponentFixture<unknown>, label: string) =>
    Array.from(root(f).querySelectorAll<HTMLButtonElement>('button')).find((b) =>
      b.textContent?.includes(label),
    );

  async function click(f: ComponentFixture<unknown>, label: string) {
    const target = button(f, label);
    expect(target, `button "${label}"`).toBeDefined();
    target!.click();
    await settle(f);
  }

  async function startGame(f: ComponentFixture<unknown>) {
    await click(f, '3 cards');
    await click(f, 'Start');
  }

  it('shows the loading error when the sets cannot be loaded', async () => {
    service.listMySets.mockRejectedValue(new Error('offline'));
    const fixture = await setup();

    expect(root(fixture).textContent).toContain('Could not load your cards.');
  });

  it('says so when no set can be played', async () => {
    service.listMySets.mockResolvedValue([{ ...sums, tiers: [] }]);
    const fixture = await setup();

    expect(testId(fixture, 'flash-cards-none')?.textContent).toContain('No flash cards');
  });

  it('shows card count and stars per tier, and a set picker only with several sets', async () => {
    const fixture = await setup();

    expect(root(fixture).textContent).toContain('3 cards · 1 stars');
    expect(root(fixture).textContent).toContain('5 cards · 2 stars');
    expect(root(fixture).textContent).not.toContain('Pick a set');

    service.listMySets.mockResolvedValue([sums, words]);
    const several = await (async () => {
      TestBed.resetTestingModule();
      return setup();
    })();

    expect(root(several).textContent).toContain('Pick a set');
    expect(root(several).textContent).not.toContain('3 cards');
    await click(several, 'Words');
    expect(root(several).textContent).toContain('3 cards · 1 stars');
  });

  it('keeps Start disabled until a tier is picked, then fetches that deck', async () => {
    const fixture = await setup();

    expect(button(fixture, 'Start')?.disabled).toBe(true);
    await click(fixture, '5 cards');
    expect(button(fixture, 'Start')?.disabled).toBe(false);
    await click(fixture, 'Start');

    expect(service.getDeck).toHaveBeenCalledWith('set-1', 'tier-5');
    expect(testId(fixture, 'flash-cards-question')?.textContent?.trim()).toBe('1 + 1');
  });

  it('shows an error when the deck cannot be fetched', async () => {
    service.getDeck.mockRejectedValue(new Error('offline'));
    const fixture = await setup();
    await startGame(fixture);

    expect(root(fixture).textContent).toContain('Could not get the cards.');
    expect(testId(fixture, 'flash-cards-question')).toBeNull();
  });

  it('hides the answer and the answer buttons until the card is flipped', async () => {
    const fixture = await setup();
    await startGame(fixture);

    expect(testId(fixture, 'flash-cards-answer')).toBeNull();
    expect(button(fixture, 'I knew it')).toBeUndefined();
    expect(testId(fixture, 'flash-cards-progress')?.textContent).toContain('Card 1 of 2');

    await click(fixture, 'Show the answer');

    expect(testId(fixture, 'flash-cards-answer')?.textContent?.trim()).toBe('2');
    expect(button(fixture, 'I knew it')).toBeDefined();
    expect(button(fixture, 'Not yet')).toBeDefined();
    expect(button(fixture, 'Show the answer')).toBeUndefined();
  });

  it('moves to the next card with I knew it and sends the finished game', async () => {
    const fixture = await setup();
    await startGame(fixture);

    await click(fixture, 'Show the answer');
    await click(fixture, 'I knew it');
    expect(testId(fixture, 'flash-cards-question')?.textContent?.trim()).toBe('2 + 2');
    expect(testId(fixture, 'flash-cards-progress')?.textContent).toContain('Card 2 of 2');

    await click(fixture, 'Show the answer');
    await click(fixture, 'I knew it');

    expect(service.finishGame).toHaveBeenCalledTimes(1);
    expect(service.finishGame.mock.calls[0]![0]).toMatchObject({
      setId: 'set-1',
      tierId: 'tier-3',
      results: [
        { slot: 0, cardKey: 'a', knewFirstTime: true },
        { slot: 1, cardKey: 'b', knewFirstTime: true },
      ],
    });
    expect(testId(fixture, 'flash-cards-stars')?.textContent).toContain('You earned 1 stars');
    expect(testId(fixture, 'flash-cards-total')?.textContent).toContain('7 stars in all');
    expect(testId(fixture, 'flash-cards-known')?.textContent).toContain('1 of 2 first time');
  });

  it('repeats a Not yet card once at the end, with a badge', async () => {
    const fixture = await setup();
    await startGame(fixture);

    await click(fixture, 'Show the answer');
    await click(fixture, 'Not yet');
    await click(fixture, 'Show the answer');
    await click(fixture, 'I knew it');

    expect(testId(fixture, 'flash-cards-question')?.textContent?.trim()).toBe('1 + 1');
    expect(testId(fixture, 'flash-cards-repeat')).not.toBeNull();
    expect(testId(fixture, 'flash-cards-progress')?.textContent).toContain('Card 3 of 3');
    expect(service.finishGame).not.toHaveBeenCalled();

    await click(fixture, 'Show the answer');
    await click(fixture, 'Not yet');

    expect(service.finishGame.mock.calls[0]![0].results[0]).toEqual({
      slot: 0,
      cardKey: 'a',
      knewFirstTime: false,
      repeatedAnswer: false,
    });
  });

  it('shows a kind message instead of stars when none were earned', async () => {
    service.finishGame.mockResolvedValue(finished(0, 0));
    const fixture = await setup();
    await startGame(fixture);

    for (let turn = 0; turn < 2; turn++) {
      await click(fixture, 'Show the answer');
      await click(fixture, 'Not yet');
    }
    for (let turn = 0; turn < 2; turn++) {
      await click(fixture, 'Show the answer');
      await click(fixture, 'Not yet');
    }

    expect(testId(fixture, 'flash-cards-no-stars')?.textContent).toContain('No stars this time');
    expect(testId(fixture, 'flash-cards-stars')).toBeNull();
    expect(testId(fixture, 'flash-cards-total')?.textContent).toContain('7 stars in all');
  });

  async function playToFinish(fixture: ComponentFixture<unknown>) {
    await startGame(fixture);
    for (let turn = 0; turn < 2; turn++) {
      await click(fixture, 'Show the answer');
      await click(fixture, 'I knew it');
    }
  }

  it('retries saving with the same GameId', async () => {
    service.finishGame.mockRejectedValueOnce(new Error('offline'));
    const randomUUID = vi.spyOn(crypto, 'randomUUID');
    const fixture = await setup();
    await playToFinish(fixture);

    expect(testId(fixture, 'flash-cards-finish-problem')?.textContent).toContain(
      'Could not save your game.',
    );

    await click(fixture, 'Try again');

    expect(service.finishGame).toHaveBeenCalledTimes(2);
    expect(service.finishGame.mock.calls[1]![0].gameId).toBe(
      service.finishGame.mock.calls[0]![0].gameId,
    );
    expect(randomUUID).toHaveBeenCalledTimes(1);
    expect(testId(fixture, 'flash-cards-result')).not.toBeNull();
  });

  it('makes a new GameId for the next game', async () => {
    const fixture = await setup();
    await playToFinish(fixture);
    await click(fixture, 'Play again');
    await playToFinish(fixture);

    expect(service.finishGame).toHaveBeenCalledTimes(2);
    expect(service.finishGame.mock.calls[1]![0].gameId).not.toBe(
      service.finishGame.mock.calls[0]![0].gameId,
    );
  });

  it('says the game can no longer be saved when the set was switched off', async () => {
    service.finishGame.mockRejectedValue(
      new HttpErrorResponse({ status: 422, error: { code: 'game_no_longer_available' } }),
    );
    const fixture = await setup();
    await playToFinish(fixture);

    expect(testId(fixture, 'flash-cards-finish-problem')?.textContent).toContain(
      'can’t be saved any more',
    );
    expect(button(fixture, 'Try again')).toBeUndefined();
    expect(button(fixture, 'Play again')).toBeDefined();
  });

  it('asks to wait while a first attempt is still saving', async () => {
    service.finishGame.mockRejectedValue(
      new HttpErrorResponse({ status: 409, error: { code: 'idempotency_key_in_progress' } }),
    );
    const fixture = await setup();
    await playToFinish(fixture);

    expect(testId(fixture, 'flash-cards-finish-problem')?.textContent).toContain('Still saving');
    expect(button(fixture, 'Try again')).toBeDefined();
  });
});
