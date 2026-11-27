import { describe, expect, it } from 'vitest';

import {
  DeckCard,
  FlashCardGame,
  answer,
  createGame,
  current,
  flip,
  isFinished,
  toFinishRequest,
  toResults,
} from './flash-card-game';

const cards: DeckCard[] = [
  { key: 'a', question: '1 + 1', answer: '2', math: null },
  { key: 'b', question: '2 + 2', answer: '4', math: null },
  { key: 'c', question: '3 + 3', answer: '6', math: null },
];

const start = () => createGame('game-1', 'set-1', 'tier-1', cards);

// Flips and answers the current card.
const play = (game: FlashCardGame, knew: boolean) => answer(flip(game), knew);

describe('flash card game', () => {
  it('starts on slot 0 of the first pass, answer hidden, not finished', () => {
    const game = start();

    expect(current(game)).toEqual({ slot: 0, card: cards[0], pass: 'first', revealed: false });
    expect(isFinished(game)).toBe(false);
    expect(toResults(game)).toBeNull();
    expect(toFinishRequest(game)).toBeNull();
  });

  it('flip shows the answer and flipping again changes nothing', () => {
    const flipped = flip(start());

    expect(current(flipped)?.revealed).toBe(true);
    expect(flip(flipped)).toBe(flipped);
  });

  it('ignores an answer before the card is flipped', () => {
    const game = start();

    expect(answer(game, true)).toBe(game);
  });

  it('visits every slot once in order, hiding the answer again after each', () => {
    let game = play(start(), true);
    expect(current(game)).toEqual({ slot: 1, card: cards[1], pass: 'first', revealed: false });

    game = play(game, true);
    expect(current(game)?.slot).toBe(2);
  });

  it('finishes after the first pass when every card was known', () => {
    let game = start();
    for (let i = 0; i < 3; i += 1) {
      game = play(game, true);
    }

    expect(isFinished(game)).toBe(true);
    expect(current(game)).toBeNull();
    expect(toResults(game)).toEqual([
      { slot: 0, cardKey: 'a', knewFirstTime: true },
      { slot: 1, cardKey: 'b', knewFirstTime: true },
      { slot: 2, cardKey: 'c', knewFirstTime: true },
    ]);
  });

  it('repeats the Not yet slots once, in slot order, after the first pass', () => {
    let game = play(start(), false); // slot 0 not yet
    game = play(game, true); // slot 1 knew
    game = play(game, false); // slot 2 not yet

    expect(isFinished(game)).toBe(false);
    expect(current(game)).toEqual({ slot: 0, card: cards[0], pass: 'repeat', revealed: false });

    game = play(game, true);
    expect(current(game)).toEqual({ slot: 2, card: cards[2], pass: 'repeat', revealed: false });

    game = play(game, true);
    expect(isFinished(game)).toBe(true);
  });

  it('does not repeat a slot a second time, whatever the repeat answer', () => {
    let game = play(start(), false);
    game = play(game, true);
    game = play(game, true);

    game = play(game, false); // repeat of slot 0 answered Not yet again

    expect(isFinished(game)).toBe(true);
    expect(current(game)).toBeNull();
  });

  it('keeps the first-pass result and reports the repeat answer separately', () => {
    let game = play(start(), false);
    game = play(game, true);
    game = play(game, true);
    game = play(game, true);

    expect(toResults(game)).toEqual([
      { slot: 0, cardKey: 'a', knewFirstTime: false, repeatedAnswer: true },
      { slot: 1, cardKey: 'b', knewFirstTime: true },
      { slot: 2, cardKey: 'c', knewFirstTime: true },
    ]);
  });

  it('is not finished until the repeats are played', () => {
    let game = play(start(), true);
    game = play(game, false);
    game = play(game, true);

    expect(isFinished(game)).toBe(false);
    expect(toResults(game)).toBeNull();
  });

  it('ignores answers and flips once the game is over', () => {
    let game = start();
    for (let i = 0; i < 3; i += 1) {
      game = play(game, true);
    }

    expect(answer(game, false)).toBe(game);
    expect(flip(game)).toBe(game);
  });

  it('does not change the game it was given', () => {
    const game = start();
    play(game, false);

    expect(game.step).toBe(0);
    expect(game.firstPass).toEqual([null, null, null]);
  });

  it('builds the finish request with the ids the game was created with', () => {
    let game = start();
    for (let i = 0; i < 3; i += 1) {
      game = play(game, true);
    }

    expect(toFinishRequest(game)).toEqual({
      gameId: 'game-1',
      setId: 'set-1',
      tierId: 'tier-1',
      results: toResults(game),
    });
  });

  it('plays a single-card deck to the end', () => {
    let game = createGame('g', 's', 't', [cards[0]]);
    game = play(game, false);
    expect(current(game)?.pass).toBe('repeat');

    game = play(game, false);
    expect(isFinished(game)).toBe(true);
    expect(toResults(game)).toEqual([
      { slot: 0, cardKey: 'a', knewFirstTime: false, repeatedAnswer: false },
    ]);
  });
});
