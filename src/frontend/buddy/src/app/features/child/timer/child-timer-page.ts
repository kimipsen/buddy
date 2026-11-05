import { Component, computed, effect, inject, signal } from '@angular/core';

import { ChildClockService } from '../../../core/child-clock.service';
import { chimeWhenTimeRunsOut } from '../../../core/chime-when-time-runs-out';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { keepAwakeWhile } from '../../../core/keep-awake';
import {
  OWN_TIMER_MINUTES,
  OwnTimer,
  ownTimerState,
  pauseOwnTimer,
  resumeOwnTimer,
  startOwnTimer,
} from '../../../core/own-timer';
import { clearOwnTimer, readOwnTimer, writeOwnTimer } from '../../../core/own-timer-storage';
import {
  CHIME_SOUNDS,
  CHIME_SOUND_LABELS,
  ChimeSound,
  TIMER_COLORS,
  TIMER_COLOR_LABELS,
  TIMER_SHAPES,
  TIMER_SHAPE_LABELS,
  TIMER_SIZES,
  TIMER_SIZE_LABELS,
  TimerColor,
  TimerPreferences,
  timerPreferencesOf,
} from '../../../core/timer-preferences';
import { TimeUpChimeService } from '../../../core/time-up-chime.service';
import { UsersService } from '../../../core/users.service';
import { TimerState } from '../../../core/visual-timer';
import { SegmentedControl } from '../../../shared/segmented-control/segmented-control';
import { Toggle } from '../../../shared/toggle/toggle';
import { VisualTimer } from '../../../shared/visual-timer/visual-timer';
import { ChildPage } from '../child-page/child-page';

// The disc the page draws when no timer runs: an own timer is always green unless the child
// picked a colour, so "Same as the routine" shows as that green here.
const OWN_TIMER_COLOR = '#10b981';
// The preview is a 5-minute timer with most of its time left, so the colour shows as a big wedge.
const PREVIEW_BUDGET_MS = 5 * 60_000;
const PREVIEW_REMAINING_MS = 3.5 * 60_000;

// "My timer" (docs/frontend/analysis/child-timer-preferences.md, Question 4): a timer the child
// starts from a few preset lengths, kept on this device, and the settings for how every timer
// looks and sounds -- on one page, so each choice shows on a real timer straight away.
@Component({
  selector: 'app-child-timer-page',
  imports: [ChildPage, SegmentedControl, Toggle, TranslatePipe, VisualTimer],
  templateUrl: './child-timer-page.html',
  // Any tap lets the page make sound later (browsers block audio until the user interacts).
  host: { '(pointerdown)': 'unlockChime()' },
})
export class ChildTimerPage {
  private readonly clock = inject(ChildClockService);
  private readonly users = inject(UsersService);
  private readonly chime = inject(TimeUpChimeService);
  private readonly translation = inject(TranslationService);

  protected readonly presets = OWN_TIMER_MINUTES;
  protected readonly ownTimerColor = OWN_TIMER_COLOR;

  private readonly now = signal(Date.now());
  protected readonly timer = signal<OwnTimer | null>(readOwnTimer(this.clock.today()));

  protected readonly state = computed<TimerState | null>(() => {
    const timer = this.timer();
    return timer ? ownTimerState(timer, this.now()) : null;
  });

  protected readonly preview: TimerState = {
    current: null,
    next: null,
    remainingMs: PREVIEW_REMAINING_MS,
    budgetMs: PREVIEW_BUDGET_MS,
    runRemainingMs: PREVIEW_REMAINING_MS,
    runBudgetMs: PREVIEW_BUDGET_MS,
    deadlineMs: null,
    startsAtMs: null,
    phase: 'upcoming',
    // Stryker disable next-line BooleanLiteral: only the timer's announcements read it, and the preview passes announce false
    childStarted: false,
  };

  // What the child picked, shown straight away. Saves go one after another, each carrying every
  // choice made so far, so a second tap before the first save returns doesn't undo the first.
  private readonly draft = signal<TimerPreferences | null>(null);
  private saving: Promise<unknown> = Promise.resolve();
  private pendingSaves = 0;

  protected readonly preferences = computed(
    () => this.draft() ?? timerPreferencesOf(this.users.currentUser()),
  );
  protected readonly chimeAllowed = computed(() => this.users.currentUser()?.allowChime !== false);

  protected readonly saveError = signal<string | null>(null);

  protected readonly shapeOptions = computed(() =>
    TIMER_SHAPES.map((value) => ({ value, label: this.t(TIMER_SHAPE_LABELS[value]) })),
  );
  protected readonly sizeOptions = computed(() =>
    TIMER_SIZES.map((value) => ({ value, label: this.t(TIMER_SIZE_LABELS[value]) })),
  );
  protected readonly soundOptions = computed(() =>
    CHIME_SOUNDS.map((value) => ({ value, label: this.t(CHIME_SOUND_LABELS[value]) })),
  );
  protected readonly colorOptions: { value: TimerColor; label: string; hex: string | null }[] = [
    { value: 'Routine', label: TIMER_COLOR_LABELS.Routine, hex: null },
    ...(Object.entries(TIMER_COLORS) as [Exclude<TimerColor, 'Routine'>, string][]).map(
      ([value, hex]) => ({ value, label: TIMER_COLOR_LABELS[value], hex }),
    ),
  ];

  constructor() {
    keepAwakeWhile(computed(() => this.state()?.phase === 'running'));
    chimeWhenTimeRunsOut(
      computed(() => {
        const state = this.state();
        // Stryker disable next-line StringLiteral: any constant key works, there is only one own timer
        return state ? { key: 'own', phase: state.phase } : null;
      }),
      'own',
    );

    // Recalculate the moment time runs out, so the buttons and the chime follow the disc.
    effect((onCleanup) => {
      const deadlineMs = this.state()?.deadlineMs;
      if (deadlineMs === null || deadlineMs === undefined) {
        return;
      }

      const id = setTimeout(() => this.now.set(Date.now()), Math.max(0, deadlineMs - Date.now()));
      // Stryker disable next-line ArrowFunction,CallExpression: a leftover timeout only refreshes `now` to the real time
      onCleanup(() => clearTimeout(id));
    });
  }

  protected unlockChime(): void {
    this.chime.unlock();
  }

  protected start(minutes: number): void {
    this.save(startOwnTimer(minutes, this.tick()));
  }

  protected pause(timer: OwnTimer): void {
    this.save(pauseOwnTimer(timer, this.tick()));
  }

  protected resume(timer: OwnTimer): void {
    this.save(resumeOwnTimer(timer, this.tick()));
  }

  protected stop(): void {
    this.timer.set(null);
    clearOwnTimer();
  }

  protected async listen(sound: ChimeSound): Promise<void> {
    // A suspended context only plays once resume() has finished.
    await this.chime.unlock();
    this.chime.play(sound);
  }

  protected async change(patch: Partial<TimerPreferences>): Promise<void> {
    const next = { ...this.preferences(), ...patch };
    this.draft.set(next);
    this.saveError.set(null);
    this.pendingSaves++;

    const save = this.saving.then(() => this.users.updateTimerPreferences(next));
    this.saving = save.catch(() => undefined);
    try {
      await save;
    } catch {
      this.saveError.set('childTimer.saveError');
    } finally {
      // Once nothing is left to save, show what the server holds: the last save's answer, or the
      // old value after a failure.
      this.pendingSaves--;
      if (this.pendingSaves === 0) {
        this.draft.set(null);
      }
    }
  }

  private tick(): number {
    const nowMs = Date.now();
    // Stryker disable next-line CallExpression: the drawn timer ticks on its own clock; the page's state only needs the right phase, which a stale `now` doesn't change
    this.now.set(nowMs);
    return nowMs;
  }

  private save(timer: OwnTimer): void {
    this.timer.set(timer);
    writeOwnTimer(this.clock.today(), timer);
  }

  private t(key: string): string {
    return this.translation.translate(key);
  }
}
