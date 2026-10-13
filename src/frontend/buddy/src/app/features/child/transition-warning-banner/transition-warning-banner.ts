import { Component, OnDestroy, OnInit, computed, effect, inject, signal } from '@angular/core';

import { ChildWarningFeed } from '../../../core/child-warning-feed.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import {
  TransitionWarning,
  minutesUntil,
  remainingFraction,
} from '../../../core/transition-warnings';

// Fine enough for a 1-minute stage; the home page's 60-second tick isn't.
export const WARNING_TICK_MS = 15_000;
// How often the banner asks the feed whether its data is stale (the feed refetches only every
// WARNING_REFRESH_MS). Runs whether or not warnings are on, so switching them on reaches an open iPad.
export const REFRESH_CHECK_MS = 60_000;

const PICKUP_LABELS = {
  DropOff: 'transitionWarnings.banner.dropOff',
  PickUp: 'transitionWarnings.banner.pickUp',
} as const;

/**
 * The heads-up before the child's next transition ("5 minutes until Football"), shown in the
 * ChildPage frame so it appears on every child page (docs/backend/analysis/transition-warnings.md,
 * Question 6). Renders nothing, and runs no tick, unless a guardian turned warnings on.
 */
@Component({
  selector: 'app-transition-warning-banner',
  imports: [TranslatePipe],
  templateUrl: './transition-warning-banner.html',
  host: { class: 'block' },
})
export class TransitionWarningBanner implements OnInit, OnDestroy {
  private readonly feed = inject(ChildWarningFeed);

  protected readonly active = this.feed.active;
  protected readonly now = signal(Date.now());
  protected readonly showAll = signal(false);
  protected readonly pickupLabels = PICKUP_LABELS;

  protected readonly warnings = computed(() => this.feed.warningsAt(this.now()));
  protected readonly current = computed<TransitionWarning | null>(() => this.warnings()[0] ?? null);
  protected readonly others = computed(() => this.warnings().slice(1));

  private refreshIntervalId: ReturnType<typeof setInterval> | undefined;
  private readonly onVisibilityChange = (): void => {
    if (document.visibilityState === 'visible') {
      this.now.set(Date.now());
      void this.feed.refresh(true);
    }
  };

  constructor() {
    // The countdown tick runs only while this child gets warnings at all.
    effect((onCleanup) => {
      if (!this.feed.active()) {
        return;
      }

      // The clock stood still while warnings were off: catch up before the first stage shows.
      this.now.set(Date.now());
      const id = setInterval(() => this.now.set(Date.now()), WARNING_TICK_MS);
      onCleanup(() => clearInterval(id));
    });
  }

  ngOnInit(): void {
    void this.feed.refresh();
    // Picks up a guardian's change (an item moved, warnings switched on) on an iPad left open.
    this.refreshIntervalId = setInterval(() => void this.feed.refresh(), REFRESH_CHECK_MS);
    document.addEventListener('visibilitychange', this.onVisibilityChange);
  }

  ngOnDestroy(): void {
    clearInterval(this.refreshIntervalId);
    document.removeEventListener('visibilitychange', this.onVisibilityChange);
  }

  protected minutes(warning: TransitionWarning): number {
    return minutesUntil(warning, this.now());
  }

  protected barPercent(warning: TransitionWarning): number {
    return remainingFraction(warning, this.now()) * 100;
  }

  protected dismiss(warning: TransitionWarning): void {
    this.feed.dismiss(warning.key);
    this.showAll.set(false);
  }

  protected toggleAll(): void {
    this.showAll.update((open) => !open);
  }
}
