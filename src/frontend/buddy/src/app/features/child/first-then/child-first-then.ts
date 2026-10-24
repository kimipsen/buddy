import {
  Component,
  OnDestroy,
  OnInit,
  computed,
  effect,
  inject,
  resource,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';

import { ChildClockService } from '../../../core/child-clock.service';
import {
  boardForChild,
  isBoardChanged,
  isThenTimeUp,
  thenTimerState,
} from '../../../core/first-then';
import {
  FirstThenAction,
  FirstThenBoard,
  FirstThenService,
} from '../../../core/first-then.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { UsersService } from '../../../core/users.service';
import { pollWhileOpen } from '../../../core/visible-poll';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';
import { VisualTimer } from '../../../shared/visual-timer/visual-timer';
import { ChildPage } from '../child-page/child-page';
import { Icon } from '../../../shared/icon/icon';

export const FIRST_THEN_POLL_MS = 60_000;
// Often enough for "Time's up" to appear on time when no disc is shown (visual timers off).
export const THEN_TICK_MS = 15_000;

/**
 * The child's First – Then board (docs/backend/analysis/first-then-board.md, Question 7): two big
 * cards, Done on the First, which unlocks the Then straight away, Not done yet for a mis-tap and
 * All done to end it. Polls once a minute and on visibilitychange for a guardian's new board. A
 * board from before today (the child's clock) is shown as no board.
 */
@Component({
  selector: 'app-child-first-then',
  imports: [Icon, ChildPage, LoadingSpinner, RouterLink, TranslatePipe, VisualTimer],
  templateUrl: './child-first-then.html',
})
export class ChildFirstThen implements OnInit, OnDestroy {
  private readonly firstThen = inject(FirstThenService);
  private readonly clock = inject(ChildClockService);
  private readonly users = inject(UsersService);
  private readonly translation = inject(TranslationService);

  private readonly me = resource({ loader: () => this.users.ensureCurrentUser() });
  private readonly loaded = signal<FirstThenBoard | null>(null);
  protected readonly loading = signal(true);
  // Stryker disable next-line BooleanLiteral: only read once loading is over, and every load sets it first
  protected readonly loadError = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly changed = signal(false);
  // Set for the moment the Then unlocks on this device: one scale-in, under motion-safe only.
  protected readonly justUnlocked = signal(false);
  protected readonly announcement = signal('');
  protected readonly now = signal(this.clock.now());

  protected readonly board = computed(() =>
    boardForChild(this.loaded(), this.clock.today(this.now()), this.clock.timeZone()),
  );
  protected readonly showTimers = computed(
    () => this.me.hasValue() && this.me.value().showVisualTimers !== false,
  );
  protected readonly timer = computed(() => {
    const board = this.board();
    return board && this.showTimers() ? thenTimerState(board, this.now()) : null;
  });
  protected readonly timeUp = computed(() => {
    const board = this.board();
    return !!board && isThenTimeUp(board, this.now());
  });

  private stopPolling: (() => void) | undefined;

  constructor() {
    // The tick runs only while an unlocked Then is counting down.
    effect((onCleanup) => {
      const board = this.board();

      if (!board || board.state !== 'Unlocked' || !board.thenMinutes) {
        return;
      }

      const id = setInterval(() => this.now.set(this.clock.now()), THEN_TICK_MS);
      onCleanup(() => clearInterval(id));
    });
  }

  ngOnInit(): void {
    void this.refresh();
    this.stopPolling = pollWhileOpen(() => void this.refresh(), FIRST_THEN_POLL_MS);
  }

  ngOnDestroy(): void {
    this.stopPolling?.();
  }

  protected done(): Promise<void> {
    return this.act('first/complete');
  }

  protected undo(): Promise<void> {
    return this.act('first/reopen');
  }

  protected allDone(): Promise<void> {
    return this.act('end');
  }

  // Bumped when a write starts and when it ends, so a poll that overlapped a write can't put back
  // the board the write replaced. The reload after a 409 runs inside the write, so it counts.
  private writes = 0;

  private async refresh(): Promise<void> {
    const writes = this.writes;

    try {
      const board = await this.firstThen.getMyBoard();

      if (writes !== this.writes) {
        return;
      }

      this.loaded.set(board);
      this.now.set(this.clock.now());
      this.loadError.set(false);
    } catch {
      // A failed poll keeps the last board on screen (the device may be offline).
      this.loadError.set(this.loaded() === null);
    } finally {
      this.loading.set(false);
    }
  }

  private async act(action: FirstThenAction): Promise<void> {
    const board = this.board();

    if (!board || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.writes++;
    this.error.set(null);
    this.changed.set(false);
    // Cleared while the request is out, so the same words are announced again next time.
    this.announcement.set('');

    try {
      const updated = await this.firstThen.actOnMyBoard(action, board.revision);
      this.loaded.set(updated);
      this.now.set(this.clock.now());

      // Laid out so each Stryker comment below covers only its own condition.
      // prettier-ignore
      if (
        // Stryker disable next-line ConditionalExpression: only first/complete comes back Unlocked (a reopen is Waiting, an end None)
        action === 'first/complete' &&
        // Stryker disable next-line ConditionalExpression: a first/complete that succeeds always comes back Unlocked
        updated.state === 'Unlocked' &&
        updated.then
      ) {
        this.justUnlocked.set(true);
        this.announcement.set(
          this.translation.translate('firstThen.child.unlocked', { then: updated.then.title }),
        );
      } else {
        this.justUnlocked.set(false);
      }
    } catch (error) {
      if (isBoardChanged(error)) {
        // A guardian set a new board since this one loaded: show it rather than tick it.
        await this.refresh();
        this.changed.set(true);
        this.announcement.set(this.translation.translate('firstThen.child.changed'));
      } else {
        this.error.set('firstThen.child.actionError');
      }
    } finally {
      this.writes++;
      this.busy.set(false);
    }
  }
}
