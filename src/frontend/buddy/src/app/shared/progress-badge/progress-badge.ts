import { Component, computed, effect, input, signal } from '@angular/core';

import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'app-progress-badge',
  imports: [TranslatePipe],
  templateUrl: './progress-badge.html'
})
export class ProgressBadge {
  readonly totalStars = input.required<number>();
  // Resolved server-side from the child's guardian-configured goal posts (see
  // docs/backend/analysis/configurable-goal-posts.md) -- including extrapolated posts past
  // whatever the guardian configured, so the badge never plateaus. currentIcon is null before
  // the child has reached their first goal post.
  readonly currentIcon = input<string | null>(null);
  readonly nextGoalThreshold = input<number>(0);
  readonly nextGoalIcon = input<string>('🌱');

  protected readonly stage = computed(() => this.currentIcon() ?? this.nextGoalIcon());
  protected readonly hasNextGoal = computed(() => this.nextGoalThreshold() > this.totalStars());

  // A short pulse whenever the count goes up -- immediate feedback at the moment of completion
  // matters more here than a delayed summary, so this reacts to the input signal directly rather
  // than waiting for the parent to pass down a separate "just changed" flag.
  protected readonly celebrating = signal(false);
  private previousTotal: number | null = null;
  private celebrationTimeout: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    effect(() => {
      const current = this.totalStars();

      if (this.previousTotal !== null && current > this.previousTotal) {
        this.celebrating.set(true);

        if (this.celebrationTimeout !== null) {
          clearTimeout(this.celebrationTimeout);
        }

        this.celebrationTimeout = setTimeout(() => this.celebrating.set(false), 700);
      }

      this.previousTotal = current;
    });
  }
}
