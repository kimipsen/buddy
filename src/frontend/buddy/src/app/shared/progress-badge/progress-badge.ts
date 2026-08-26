import { Component, computed, effect, input, signal } from '@angular/core';

import { TranslatePipe } from '../../core/i18n/translate.pipe';

// Deliberately not the ★ glyph -- that's already the meal-rating icon on the child dashboard
// (see child.mealplan star rating), so reusing it here would read as "rate something" rather
// than "you earned something." A growing plant reinforces the never-resets-to-zero design in
// docs/backend/analysis/gamified-progress.md: it only ever grows, it doesn't wilt on a missed day.
const GROWTH_STAGES = ['🌱', '🌿', '🪴', '🌳'];

@Component({
  selector: 'app-progress-badge',
  imports: [TranslatePipe],
  templateUrl: './progress-badge.html'
})
export class ProgressBadge {
  readonly totalStars = input.required<number>();
  readonly unlockedMilestones = input<number[]>([]);

  protected readonly stage = computed(() => {
    const stageIndex = Math.min(this.unlockedMilestones().length, GROWTH_STAGES.length - 1);

    return GROWTH_STAGES[stageIndex];
  });

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
