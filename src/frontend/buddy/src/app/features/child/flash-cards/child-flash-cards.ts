import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, resource, signal } from '@angular/core';

import {
  FlashCardGame,
  answer,
  createGame,
  current,
  flip,
  isFinished,
  toFinishRequest,
} from '../../../core/flash-card-game';
import {
  FinishFlashCardGameResponse,
  FlashCardsService,
  GAME_NO_LONGER_AVAILABLE,
  IDEMPOTENCY_KEY_IN_PROGRESS,
  MyFlashCardSet,
  isConflict,
} from '../../../core/flash-cards.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ChildPage } from '../child-page/child-page';

function errorCode(error: unknown): string | undefined {
  return error instanceof HttpErrorResponse
    ? (error.error as { code?: string } | null)?.code
    : undefined;
}

type FinishProblem = 'error' | 'unavailable' | 'busy';

// The child's flash-card game (`/child/homework/flash-cards`): pick a set and a game length, flip
// each card, answer "I knew it" or "Not yet", then see the stars earned. No timer. The game lives
// in this component only; its GameId is made once when the game starts and reused by every retry
// of saving it.
@Component({
  selector: 'app-child-flash-cards',
  imports: [TranslatePipe, ChildPage],
  templateUrl: './child-flash-cards.html',
})
export class ChildFlashCards {
  private readonly service = inject(FlashCardsService);

  protected readonly sets = resource({ loader: () => this.service.listMySets() });

  protected readonly selectedSetId = signal<string | null>(null);
  protected readonly selectedTierId = signal<string | null>(null);
  protected readonly starting = signal(false);
  protected readonly deckError = signal(false);

  protected readonly game = signal<FlashCardGame | null>(null);
  protected readonly finishing = signal(false);
  protected readonly finishProblem = signal<FinishProblem | null>(null);
  protected readonly result = signal<FinishFlashCardGameResponse | null>(null);

  protected readonly playable = computed(() =>
    this.sets.hasValue() ? this.sets.value().filter((set) => set.tiers.length > 0) : [],
  );

  // With one set there is nothing to choose between.
  protected readonly selectedSet = computed<MyFlashCardSet | null>(() => {
    const sets = this.playable();
    const id = this.selectedSetId();

    return sets.find((set) => set.setId === id) ?? (sets.length === 1 ? (sets[0] ?? null) : null);
  });

  protected readonly selectedTier = computed(() => {
    const set = this.selectedSet();
    const id = this.selectedTierId();

    return set?.tiers.find((tier) => tier.id === id) ?? null;
  });

  protected readonly now = computed(() => {
    const game = this.game();

    return game === null ? null : current(game);
  });

  protected readonly progress = computed(() => {
    const game = this.game();

    return game === null || this.now() === null
      ? null
      : { position: game.step + 1, total: this.totalTurns(game) };
  });

  protected selectSet(setId: string): void {
    this.selectedSetId.set(setId);
    this.selectedTierId.set(null);
    this.deckError.set(false);
  }

  protected selectTier(tierId: string): void {
    this.selectedTierId.set(tierId);
    this.deckError.set(false);
  }

  protected async start(): Promise<void> {
    const set = this.selectedSet();
    const tier = this.selectedTier();

    if (set === null || tier === null || this.starting()) {
      return;
    }

    this.starting.set(true);
    this.deckError.set(false);

    try {
      const deck = await this.service.getDeck(set.setId, tier.id);
      this.game.set(createGame(crypto.randomUUID(), set.setId, tier.id, deck.cards));
    } catch {
      this.deckError.set(true);
    } finally {
      this.starting.set(false);
    }
  }

  protected flip(): void {
    this.game.update((game) => (game === null ? game : flip(game)));
  }

  protected async answer(knew: boolean): Promise<void> {
    const game = this.game();

    if (game === null) {
      return;
    }

    const next = answer(game, knew);
    this.game.set(next);

    if (isFinished(next)) {
      await this.finish();
    }
  }

  // Sends the finished game. A retry sends the same request, so the same GameId.
  protected async finish(): Promise<void> {
    const game = this.game();
    const request = game === null ? null : toFinishRequest(game);

    if (request === null || this.finishing()) {
      return;
    }

    this.finishing.set(true);
    this.finishProblem.set(null);

    try {
      this.result.set(await this.service.finishGame(request));
    } catch (error) {
      const code = errorCode(error);

      this.finishProblem.set(
        code === GAME_NO_LONGER_AVAILABLE
          ? 'unavailable'
          : code === IDEMPOTENCY_KEY_IN_PROGRESS || isConflict(error, IDEMPOTENCY_KEY_IN_PROGRESS)
            ? 'busy'
            : 'error',
      );
    } finally {
      this.finishing.set(false);
    }
  }

  protected playAgain(): void {
    this.game.set(null);
    this.result.set(null);
    this.finishProblem.set(null);
    this.selectedTierId.set(null);
    void this.sets.reload();
  }

  private totalTurns(game: FlashCardGame): number {
    // The repeats are only known once the first pass is over.
    return game.step < game.cards.length
      ? game.cards.length
      : game.cards.length + game.firstPass.filter((knew) => knew === false).length;
  }
}
