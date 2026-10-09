import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  ElementRef,
  Injector,
  OnInit,
  afterNextRender,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Router } from '@angular/router';

import { FeaturesService } from '../../../core/features.service';
import { AccountService } from '../../../core/account.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import {
  EMPTY_SETUP,
  ONBOARDING_STATUS,
  OnboardingProgress,
  OnboardingService,
  OnboardingSetup,
  OnboardingStep,
  firstIncompleteStep,
  isStepComplete,
  offeredSteps,
} from '../../../core/onboarding.service';
import { createAction } from '../../../shared/action-state/action-state';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';
import { AdultsStep } from './adults-step/adults-step';
import { CalendarStep } from './calendar-step/calendar-step';
import { ChildrenStep } from './children-step/children-step';
import { GroupStep } from './group-step/group-step';
import { MealStep } from './meal-step/meal-step';
import { SummaryStep } from './summary-step/summary-step';
import { TaskStep } from './task-step/task-step';

const STEP_LABELS: Record<OnboardingStep, string> = {
  group: 'onboarding.steps.group',
  children: 'onboarding.steps.children',
  adults: 'onboarding.steps.adults',
  calendar: 'onboarding.steps.calendar',
  task: 'onboarding.steps.task',
  meal: 'onboarding.steps.meal',
  summary: 'onboarding.steps.summary',
};

const STEP_TITLES: Record<OnboardingStep, string> = {
  group: 'onboarding.group.title',
  children: 'onboarding.children.title',
  adults: 'onboarding.adults.title',
  calendar: 'onboarding.calendar.title',
  task: 'onboarding.task.title',
  meal: 'onboarding.meal.title',
  summary: 'onboarding.summary.title',
};

// The guided first-login setup (docs/frontend/analysis/guardian-onboarding.md). It owns the stored
// progress and the setup derived from current data; each step component writes through the
// existing domain services and asks for a reload when it changed something.
@Component({
  selector: 'app-guardian-onboarding',
  imports: [
    TranslatePipe,
    LoadingSpinner,
    GroupStep,
    ChildrenStep,
    AdultsStep,
    CalendarStep,
    TaskStep,
    MealStep,
    SummaryStep,
  ],
  templateUrl: './onboarding.html',
})
export class GuardianOnboarding implements OnInit {
  private readonly onboarding = inject(OnboardingService);
  private readonly account = inject(AccountService);
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);

  private readonly stepHeading = viewChild<ElementRef<HTMLElement>>('stepHeading');

  private readonly features = inject(FeaturesService);
  // The flags are loaded before the app starts, so the offered steps are fixed for the page's life.
  protected readonly steps = offeredSteps((feature) => this.features.enabled(feature));
  // Every offered step but the summary must be done before the guide can be completed.
  private readonly requiredSteps = this.steps.filter((step) => step !== 'summary');
  protected readonly stepLabels = STEP_LABELS;
  protected readonly stepTitles = STEP_TITLES;

  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly progress = signal<OnboardingProgress | null>(null);
  protected readonly setup = signal<OnboardingSetup>(EMPTY_SETUP);
  protected readonly step = signal<OnboardingStep>('group');
  // Announced in the live region: a reload after a conflict, or why Finish didn't go through.
  protected readonly notice = signal<string | null>(null);
  protected readonly saving = createAction();

  protected readonly stepIndex = computed(() => this.steps.indexOf(this.step()));
  protected readonly stepComplete = computed(() => {
    const progress = this.progress();
    return progress !== null && isStepComplete(this.step(), progress, this.setup());
  });

  ngOnInit(): void {
    void this.load();
  }

  protected isDone(step: OnboardingStep): boolean {
    const progress = this.progress();
    return progress !== null && isStepComplete(step, progress, this.setup());
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);

    try {
      if ((await this.account.resolveRole()) !== 'guardian') {
        await this.router.navigate(['/child']);
        return;
      }

      // Opening the page writes nothing: a guardian who just looks at it (or resumes a deferred
      // guide from the dashboard) isn't redirected into it afterwards. The guide becomes active
      // when the first group is chosen, which is when the no-groups eligibility stops holding.
      const progress = await this.onboarding.getProgress();

      if (progress.status === ONBOARDING_STATUS.completed) {
        await this.router.navigate(['/guardian']);
        return;
      }

      const setup = await this.onboarding.loadSetup(progress);
      this.progress.set(progress);
      this.setup.set(setup);
      this.goTo(firstIncompleteStep(progress, setup, this.steps));
    } catch {
      this.loadError.set('onboarding.loadError');
    } finally {
      this.loading.set(false);
    }
  }

  // After a step wrote domain data: re-derive the setup, staying on the current step. Resolves to
  // whether the setup is current.
  protected async refresh(): Promise<boolean> {
    const progress = this.progress();

    if (progress === null) {
      return false;
    }

    try {
      this.setup.set(await this.onboarding.loadSetup(progress));
      return true;
    } catch {
      this.notice.set('onboarding.loadError');
      return false;
    }
  }

  protected back(): void {
    const index = this.stepIndex();

    const previous = this.steps[index - 1];

    if (previous !== undefined) {
      this.goTo(previous);
    }
  }

  protected next(): void {
    const index = this.stepIndex();

    const following = this.steps[index + 1];

    if (this.stepComplete() && following !== undefined) {
      this.goTo(following);
    }
  }

  protected async chooseGroup(groupId: string): Promise<void> {
    const status = this.progress()?.status;
    const changes: Partial<OnboardingProgress> =
      status === ONBOARDING_STATUS.notStarted
        ? { setupGroupId: groupId, status: ONBOARDING_STATUS.active }
        : { setupGroupId: groupId };

    if (await this.updateProgress(changes)) {
      await this.refresh();
      this.next();
    }
  }

  protected async skipInvitations(): Promise<void> {
    if (await this.updateProgress({ invitationsSkipped: true })) {
      this.next();
    }
  }

  protected async finishLater(): Promise<void> {
    if (await this.updateProgress({ status: ONBOARDING_STATUS.deferred })) {
      await this.router.navigate(['/guardian']);
    }
  }

  // Completion is confirmed against current data, not against what this page last saw.
  protected async finish(): Promise<void> {
    const progress = this.progress();

    if (progress === null) {
      return;
    }

    // Completed is final, so never decide it on data that may be stale.
    if (!(await this.refresh())) {
      return;
    }

    const setup = this.setup();
    const missing = this.requiredSteps.find((step) => !isStepComplete(step, progress, setup));

    if (missing !== undefined) {
      this.notice.set('onboarding.incomplete');
      this.goTo(missing);
      return;
    }

    if (await this.updateProgress({ status: ONBOARDING_STATUS.completed })) {
      await this.router.navigate(['/guardian']);
    }
  }

  // Writes against the version this page read. On a conflict (another tab moved on), reloads
  // instead of overwriting the newer progress.
  private async updateProgress(changes: Partial<OnboardingProgress>): Promise<boolean> {
    const progress = this.progress();

    if (progress === null) {
      return false;
    }

    this.notice.set(null);
    const outcome = { conflict: false };
    const saved = await this.saving.run(
      true,
      async () => {
        try {
          this.progress.set(await this.onboarding.saveProgress({ ...progress, ...changes }));
        } catch (error: unknown) {
          outcome.conflict = error instanceof HttpErrorResponse && error.status === 409;
          throw error;
        }
      },
      'onboarding.saveError',
    );

    if (outcome.conflict) {
      this.saving.reset();
      await this.load();
      this.notice.set('onboarding.conflict');
    }

    return saved;
  }

  // Moves focus to the new step's heading so keyboard and screen-reader users land on it.
  private goTo(step: OnboardingStep): void {
    this.step.set(step);
    afterNextRender(() => this.stepHeading()?.nativeElement.focus(), { injector: this.injector });
  }
}
