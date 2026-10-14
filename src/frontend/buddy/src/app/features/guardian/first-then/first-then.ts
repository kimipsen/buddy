import {
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  linkedSignal,
  resource,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';

import { CalendarsService } from '../../../core/calendars.service';
import { toIsoDateInTimeZone } from '../../../core/date-utils';
import {
  hasBoard,
  isBoardChanged,
  isConcurrencyConflict,
  isStale,
  todaysTaskCards,
} from '../../../core/first-then';
import {
  FirstThenAction,
  FirstThenService,
  GuardianFirstThen as GuardianFirstThenView,
} from '../../../core/first-then.service';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { pollWhileOpen } from '../../../core/visible-poll';
import { Card } from '../../../shared/card/card';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';
import { Page } from '../../../shared/page/page';
import { BoardDraft, FirstThenBoardForm } from './board-form/board-form';

export const GUARDIAN_FIRST_THEN_POLL_MS = 60_000;

/**
 * The guardian's First – Then page (docs/backend/analysis/first-then-board.md, Question 7): the
 * child selector, the current board as the child sees it with Mark done / Undo / End, and the
 * "New board" form. Polls while open, so a child's tick shows within a minute.
 */
@Component({
  selector: 'app-guardian-first-then',
  imports: [Card, FirstThenBoardForm, FormsModule, LoadingSpinner, Page, TranslatePipe],
  templateUrl: './first-then.html',
})
export class GuardianFirstThen implements OnInit, OnDestroy {
  private readonly guardians = inject(GuardiansService);
  private readonly firstThen = inject(FirstThenService);
  private readonly calendars = inject(CalendarsService);

  private readonly form = viewChild(FirstThenBoardForm);

  protected readonly children = resource({ loader: () => this.guardians.listMyChildren() });
  protected readonly childList = computed((): ChildSummary[] =>
    this.children.hasValue() ? this.children.value() : [],
  );
  protected readonly selectedChildId = linkedSignal(() => this.childList()[0]?.id);
  private readonly selectedChild = computed(() =>
    this.childList().find((c) => c.id === this.selectedChildId()),
  );
  protected readonly selectedChildName = computed(() => this.selectedChild()?.name.givenName ?? '');

  protected readonly view = resource({
    params: () => this.selectedChildId(),
    loader: ({ params }) => this.firstThen.getBoard(params),
  });
  // The last board loaded for the selected child. A failed poll keeps it on screen (and keeps its
  // revision for the next write) instead of dropping to an error; switching child clears it.
  protected readonly shown = linkedSignal<
    { childId: string | undefined; value: GuardianFirstThenView | undefined },
    GuardianFirstThenView | undefined
  >({
    source: () => ({
      childId: this.selectedChildId(),
      value: this.view.hasValue() ? this.view.value() : undefined,
    }),
    computation: (source, previous) =>
      source.value ??
      (previous && previous.source.childId === source.childId ? previous.value : undefined),
  });
  protected readonly board = computed(() => {
    const board = this.shown()?.board;
    return hasBoard(board) ? board : null;
  });
  protected readonly revision = computed(() => this.shown()?.board.revision ?? 0);
  // "From yesterday" by the child's clock, as the child's pages decide it.
  protected readonly isFromYesterday = computed(() => {
    const board = this.board();
    const timeZone = this.selectedChild()?.timeZoneId;
    return (
      !!board && !!timeZone && isStale(board, toIsoDateInTimeZone(new Date(), timeZone), timeZone)
    );
  });

  // Today's open tasks for the child, as First suggestions. A failure only loses the suggestions.
  protected readonly todaysTasks = resource({
    params: () => this.selectedChildId(),
    loader: async ({ params }) => {
      try {
        return todaysTaskCards(await this.calendars.listTodayOccurrences(), params);
      } catch {
        return [];
      }
    },
  });

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly changed = signal(false);

  private stopPolling: (() => void) | undefined;

  ngOnInit(): void {
    this.stopPolling = pollWhileOpen(() => this.view.reload(), GUARDIAN_FIRST_THEN_POLL_MS);
  }

  ngOnDestroy(): void {
    this.stopPolling?.();
  }

  protected selectChild(childId: string): void {
    this.selectedChildId.set(childId);
    this.error.set(null);
    this.changed.set(false);
  }

  protected markDone(): Promise<void> {
    return this.act('first/complete');
  }

  protected undo(): Promise<void> {
    return this.act('first/reopen');
  }

  protected end(): Promise<void> {
    return this.act('end');
  }

  protected async setBoard(draft: BoardDraft): Promise<void> {
    const childId = this.selectedChildId();

    if (!childId) {
      return;
    }

    await this.run('firstThen.guardian.saveError', async () => {
      await this.firstThen.setBoard(childId, { revision: this.revision(), ...draft });
      this.form()?.reset();
    });
  }

  private async act(action: FirstThenAction): Promise<void> {
    const childId = this.selectedChildId();
    const board = this.board();

    if (!childId || !board) {
      return;
    }

    await this.run('firstThen.guardian.actionError', () =>
      this.firstThen.act(childId, action, board.revision).then(() => undefined),
    );
  }

  // Every write reloads the board afterwards, so the page always shows the server's version and
  // who changed it. A stale revision shows the current board with a note instead of an error.
  private async run(errorKey: string, work: () => Promise<void>): Promise<void> {
    if (this.busy()) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    this.changed.set(false);

    try {
      await work();
    } catch (error) {
      if (isBoardChanged(error) || isConcurrencyConflict(error)) {
        this.changed.set(true);
      } else {
        this.error.set(errorKey);
      }
    } finally {
      this.busy.set(false);
      this.view.reload();
    }
  }
}
