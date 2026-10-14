import { Component, computed, inject, resource } from '@angular/core';
import { RouterLink } from '@angular/router';

import { toIsoDateInTimeZone } from '../../../core/date-utils';
import { boardForChild } from '../../../core/first-then';
import { FirstThenBoard, FirstThenService } from '../../../core/first-then.service';
import { GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import {
  PER_ITEM_REQUEST_CONCURRENCY,
  mapWithConcurrency,
} from '../../../core/map-with-concurrency';
import { Card } from '../../../shared/card/card';

export interface ChildBoard {
  childId: string;
  name: string;
  board: FirstThenBoard;
}

/**
 * The dashboard's First – Then card (docs/backend/analysis/first-then-board.md, Question 7): each
 * child with a current board, its two cards and state, linking to /guardian/first-then. Hidden
 * entirely when no child has a board, and when the boards can't be loaded.
 */
@Component({
  selector: 'app-first-then-today',
  imports: [Card, RouterLink, TranslatePipe],
  templateUrl: './first-then-today.html',
  host: { class: 'contents' },
})
export class FirstThenToday {
  private readonly guardians = inject(GuardiansService);
  private readonly firstThen = inject(FirstThenService);

  private readonly loaded = resource({ loader: () => this.load() });

  protected readonly boards = computed((): ChildBoard[] =>
    this.loaded.hasValue() ? this.loaded.value() : [],
  );

  private async load(): Promise<ChildBoard[]> {
    const children = await this.guardians.listMyChildren();

    const now = new Date();
    const perChild = await mapWithConcurrency(
      children,
      PER_ITEM_REQUEST_CONCURRENCY,
      async (child) => ({
        childId: child.id,
        name: child.name.givenName,
        // A board left over from yesterday (the child's clock) isn't current: the child no longer
        // sees it, so neither does the dashboard. The First – Then page still offers to end it.
        board: boardForChild(
          (await this.firstThen.getBoard(child.id)).board,
          toIsoDateInTimeZone(now, child.timeZoneId),
          child.timeZoneId,
        ),
      }),
    );

    return perChild.filter((child): child is ChildBoard => child.board !== null);
  }
}
