import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { ChildClockService } from '../../../../core/child-clock.service';
import { boardForChild } from '../../../../core/first-then';
import { FirstThenBoard, FirstThenService } from '../../../../core/first-then.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { pollWhileOpen } from '../../../../core/visible-poll';
import { FIRST_THEN_POLL_MS } from '../../first-then/child-first-then';
import { Icon } from '../../../../shared/icon/icon';

/**
 * The large card at the top of the child home while a First – Then board is current
 * (docs/backend/analysis/first-then-board.md, Question 7): both pictures small and the state,
 * opening /child/first-then. Renders nothing without a board, and keeps the last board on screen
 * when a poll fails.
 */
@Component({
  selector: 'app-first-then-card',
  imports: [Icon, RouterLink, TranslatePipe],
  templateUrl: './first-then-card.html',
  host: { class: 'contents' },
})
export class FirstThenCard implements OnInit, OnDestroy {
  private readonly firstThen = inject(FirstThenService);
  private readonly clock = inject(ChildClockService);

  private readonly loaded = signal<FirstThenBoard | null>(null);
  private readonly now = signal(this.clock.now());

  protected readonly board = computed(() =>
    boardForChild(this.loaded(), this.clock.today(this.now()), this.clock.timeZone()),
  );

  private stopPolling: (() => void) | undefined;

  ngOnInit(): void {
    void this.refresh();
    this.stopPolling = pollWhileOpen(() => void this.refresh(), FIRST_THEN_POLL_MS);
  }

  ngOnDestroy(): void {
    this.stopPolling?.();
  }

  private async refresh(): Promise<void> {
    try {
      this.loaded.set(await this.firstThen.getMyBoard());
      this.now.set(this.clock.now());
    } catch {
      // Nothing to show is the safe fallback; the home page has its own error for the rest.
    }
  }
}
