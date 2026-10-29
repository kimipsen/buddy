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
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { ChildClockService } from '../../../core/child-clock.service';
import { keepAwakeWhile } from '../../../core/keep-awake';
import {
  DEFAULT_HOMEWORK_CHUNKS,
  ESTIMATE_STEP_MINUTES,
  HomeworkChunks,
  HomeworkPart,
  HomeworkStep,
  MAX_ESTIMATE_MINUTES,
  MAX_PARTS,
  MAX_PART_TITLE_LENGTH,
  MIN_ESTIMATE_MINUTES,
  isValidPart,
  planHomework,
  totalMinutes,
} from '../../../core/homework-plan';
import {
  HomeworkSession,
  addChunk,
  breaksBefore,
  createSession,
  currentStep,
  finishPart,
  homeworkTimerState,
  isFinished,
  next,
  pause,
  resume,
  start,
} from '../../../core/homework-session';
import {
  clearHomeworkSession,
  readHomeworkSession,
  writeHomeworkSession,
} from '../../../core/homework-storage';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { UsersService } from '../../../core/users.service';
import { TimerState } from '../../../core/visual-timer';
import { VisualTimer } from '../../../shared/visual-timer/visual-timer';
import { ChildPage } from '../child-page/child-page';

// The timer component counts the seconds itself; this only moves the phase on.
const NOW_REFRESH_INTERVAL_MS = 60_000;

const FOCUS_COLOR = '#10b981'; // emerald-500
const BREAK_COLOR = '#38bdf8'; // sky-400

// Shown one per break, in turn (docs/frontend/analysis/homework-splitter.md, Question 4).
const BREAK_IDEAS = [
  'homework.breakIdeas.water',
  'homework.breakIdeas.jump',
  'homework.breakIdeas.stretch',
  'homework.breakIdeas.window',
  'homework.breakIdeas.breathe',
  'homework.breakIdeas.walk',
  'homework.breakIdeas.shake',
  'homework.breakIdeas.snack',
];

const ESTIMATE_OPTIONS = Array.from(
  { length: (MAX_ESTIMATE_MINUTES - MIN_ESTIMATE_MINUTES) / ESTIMATE_STEP_MINUTES + 1 },
  (_, i) => MIN_ESTIMATE_MINUTES + i * ESTIMATE_STEP_MINUTES,
);

function newPart(): HomeworkPart {
  return { id: crypto.randomUUID(), title: '', estimateMinutes: 20 };
}

// The homework splitter (`/child/homework`): plan tonight's homework, then work through it in
// timed focus chunks with breaks, then "All done". The session lives in this browser only.
// See docs/frontend/analysis/homework-splitter.md.
@Component({
  selector: 'app-child-homework',
  imports: [TranslatePipe, FormsModule, RouterLink, ChildPage, VisualTimer],
  templateUrl: './child-homework.html',
})
export class ChildHomework implements OnInit, OnDestroy {
  private readonly users = inject(UsersService);
  private readonly clock = inject(ChildClockService);
  private readonly translation = inject(TranslationService);

  private readonly me = resource({ loader: () => this.users.ensureCurrentUser() });

  // The guardian's lengths; the defaults until (or unless) the user loads.
  protected readonly chunks = computed<HomeworkChunks>(() =>
    this.me.hasValue() ? this.me.value().homeworkChunks : DEFAULT_HOMEWORK_CHUNKS,
  );

  // Same switch as the routine timers: with it off, the steps and buttons stay, the disc goes.
  protected readonly showTimer = computed(
    () => !this.me.hasValue() || this.me.value().showVisualTimers !== false,
  );

  protected readonly estimateOptions = ESTIMATE_OPTIONS;
  protected readonly maxParts = MAX_PARTS;
  protected readonly maxTitleLength = MAX_PART_TITLE_LENGTH;

  protected readonly parts = signal<HomeworkPart[]>([newPart()]);
  protected readonly session = signal<HomeworkSession | null>(
    readHomeworkSession(this.clock.today()),
  );

  private readonly now = signal(Date.now());
  private nowIntervalId: ReturnType<typeof setInterval> | undefined;

  protected readonly canStart = computed(
    // Stryker disable next-line ConditionalExpression,EqualityOperator: the plan always keeps at least one part (Remove shows only with two or more, Stop resets to one)
    () => this.parts().length > 0 && this.parts().every(isValidPart),
  );

  protected readonly preview = computed(() => {
    const steps = planHomework(this.parts(), this.chunks());
    return {
      chunks: steps.filter((step) => step.kind === 'focus').length,
      breaks: steps.filter((step) => step.kind === 'break').length,
      total: totalMinutes(steps),
    };
  });

  protected readonly finished = computed(() => {
    const session = this.session();
    return session !== null && isFinished(session);
  });

  protected readonly step = computed(() => {
    const session = this.session();
    return session ? currentStep(session) : null;
  });

  protected readonly nextStep = computed<HomeworkStep | null>(() => {
    const session = this.session();
    return session ? (session.steps[session.currentIndex + 1] ?? null) : null;
  });

  protected readonly state = computed<TimerState | null>(() => {
    const session = this.session();
    return session ? homeworkTimerState(session, this.now()) : null;
  });

  protected readonly timerColor = computed(() =>
    this.step()?.kind === 'break' ? BREAK_COLOR : FOCUS_COLOR,
  );

  protected readonly breakIdea = computed<string>(() => {
    const session = this.session();
    const index = session ? breaksBefore(session) % BREAK_IDEAS.length : 0;
    // Stryker disable next-line StringLiteral: the modulo keeps the index in range; the fallback only satisfies noUncheckedIndexedAccess
    return BREAK_IDEAS[index] ?? 'homework.breakIdeas.water';
  });

  // "Done with Maths" while a chunk runs, so a child who finished early can move on. On the last
  // step "I'm done" covers it, and once the time is up on a part's last chunk it would only
  // repeat "Take a break".
  protected readonly canFinishPart = computed(() => {
    const step = this.step();
    if (step?.kind !== 'focus' || !this.nextStep()) {
      return false;
    }
    return this.state()?.phase !== 'timesUp' || step.index < step.of;
  });

  protected readonly confirmingStop = signal(false);
  protected readonly announcement = signal('');

  constructor() {
    // A running chunk or break keeps the screen on (docs/frontend/analysis/keep-screen-awake.md).
    keepAwakeWhile(computed(() => this.state()?.phase === 'running'));

    // Recalculate the moment the current step's time runs out, so the heading and buttons follow
    // the disc.
    effect((onCleanup) => {
      const deadlineMs = this.state()?.deadlineMs;
      if (deadlineMs === null || deadlineMs === undefined) {
        return;
      }

      const id = setTimeout(() => this.now.set(Date.now()), Math.max(0, deadlineMs - Date.now()));
      // Stryker disable next-line ArrowFunction,CallExpression: a stale timer would only set `now` to the current time again
      onCleanup(() => clearTimeout(id));
    });
  }

  ngOnInit(): void {
    this.nowIntervalId = setInterval(() => this.now.set(Date.now()), NOW_REFRESH_INTERVAL_MS);
  }

  ngOnDestroy(): void {
    clearInterval(this.nowIntervalId);
  }

  protected titleOf(partId: string): string {
    // Stryker disable next-line StringLiteral: every step's partId names one of the session's parts
    return this.session()?.parts.find((part) => part.id === partId)?.title ?? '';
  }

  protected chunkTitle(step: HomeworkStep): string {
    return step.kind === 'focus'
      ? this.translation.translate('homework.run.chunk', {
          title: this.titleOf(step.partId),
          index: step.index,
          of: step.of,
        })
      : this.translation.translate('homework.run.break');
  }

  protected updateTitle(id: string, title: string): void {
    this.parts.update((parts) => parts.map((part) => (part.id === id ? { ...part, title } : part)));
  }

  protected updateEstimate(id: string, estimateMinutes: number): void {
    this.parts.update((parts) =>
      parts.map((part) =>
        part.id === id ? { ...part, estimateMinutes: Number(estimateMinutes) } : part,
      ),
    );
  }

  protected addPart(): void {
    this.parts.update((parts) => [...parts, newPart()]);
  }

  protected removePart(id: string): void {
    this.parts.update((parts) => parts.filter((part) => part.id !== id));
  }

  protected startSession(): void {
    if (!this.canStart()) {
      return;
    }

    const parts = this.parts().map((part) => ({ ...part, title: part.title.trim() }));
    const nowMs = Date.now();
    this.save(start(createSession(parts, this.chunks(), this.clock.today()), nowMs), nowMs);
  }

  protected pause(): void {
    this.apply((session, nowMs) => pause(session, nowMs));
  }

  protected resume(): void {
    this.apply((session, nowMs) => resume(session, nowMs));
  }

  protected next(): void {
    this.apply((session, nowMs) => next(session, nowMs));
    this.announceStep();
  }

  protected finishPart(): void {
    this.apply((session, nowMs) => finishPart(session, nowMs));
    this.announceStep();
  }

  protected moreTime(): void {
    this.apply((session, nowMs) => addChunk(session, nowMs));
  }

  protected stop(): void {
    clearHomeworkSession();
    this.session.set(null);
    this.confirmingStop.set(false);
    this.parts.set([newPart()]);
  }

  protected planAgain(): void {
    this.stop();
    this.announcement.set('');
  }

  private apply(transition: (session: HomeworkSession, nowMs: number) => HomeworkSession): void {
    const session = this.session();
    if (session) {
      const nowMs = Date.now();
      this.save(transition(session, nowMs), nowMs);
    }
  }

  private save(session: HomeworkSession, nowMs: number): void {
    if (isFinished(session)) {
      clearHomeworkSession();
    } else {
      writeHomeworkSession(session);
    }
    this.session.set(session);
    // Stryker disable next-line CallExpression: every transition leaves the deadline at or after nowMs, and the disc reads its own clock while running, so a stale `now` shows the same
    this.now.set(nowMs);
  }

  private announceStep(): void {
    const step = this.step();
    const t = (key: string, params?: Record<string, string | number>) =>
      this.translation.translate(key, params);

    this.announcement.set(
      step === null
        ? t('homework.run.announce.allDone')
        : step.kind === 'break'
          ? t('homework.run.announce.breakStarted')
          : t('homework.run.announce.backToWork', { title: this.chunkTitle(step) }),
    );
  }
}
