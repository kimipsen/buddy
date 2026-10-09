import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { AccountService } from '../../../core/account.service';
import { CalendarsService } from '../../../core/calendars.service';
import { GroupsService } from '../../../core/groups.service';
import { GuardiansService } from '../../../core/guardians.service';
import { MealplansService } from '../../../core/mealplans.service';
import {
  EMPTY_SETUP,
  OnboardingProgress,
  OnboardingService,
  OnboardingSetup,
} from '../../../core/onboarding.service';
import { TaskLibraryService } from '../../../core/task-library.service';
import {
  buttonByText,
  calendarDetail,
  child,
  progress,
  settle,
  setupWith,
} from '../../../../testing/onboarding-fixture';
import { GuardianOnboarding } from './onboarding';

const COMPLETE: OnboardingSetup = setupWith({
  children: [child()],
  pendingInvites: [
    { id: 'i1', email: 'aunt@buddy.test', role: 'Admin', invitedAt: '', expiresAt: '' },
  ],
  calendars: [calendarDetail()],
  hasScheduledRoutine: true,
  hasMealAssignment: true,
});

describe('GuardianOnboarding', () => {
  interface Options {
    role?: 'guardian' | 'child';
    progress?: OnboardingProgress;
    setup?: OnboardingSetup;
    onboarding?: Partial<OnboardingService>;
  }

  async function setup(options: Options = {}) {
    const onboardingStub: Partial<OnboardingService> = {
      getProgress: vi.fn(async () => options.progress ?? progress()),
      saveProgress: vi.fn(async (saved: OnboardingProgress) => ({
        ...saved,
        version: saved.version + 1,
      })),
      loadSetup: vi.fn(async () => options.setup ?? EMPTY_SETUP),
      ...options.onboarding,
    };

    await TestBed.configureTestingModule({
      imports: [GuardianOnboarding],
      providers: [
        provideRouter([]),
        {
          provide: AccountService,
          useValue: { resolveRole: vi.fn(async () => options.role ?? 'guardian') },
        },
        { provide: OnboardingService, useValue: onboardingStub },
        // The step components inject these; every call the page itself makes is stubbed above.
        { provide: GroupsService, useValue: { listMyGroups: vi.fn(async () => []) } },
        { provide: GuardiansService, useValue: {} },
        { provide: CalendarsService, useValue: {} },
        { provide: TaskLibraryService, useValue: {} },
        {
          provide: MealplansService,
          useValue: {
            listMeals: vi.fn(async () => []),
            getSharedGroup: vi.fn(async () => null),
          },
        },
      ],
    }).compileComponents();

    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(GuardianOnboarding);
    await settle(fixture);

    return {
      fixture,
      compiled: fixture.nativeElement as HTMLElement,
      onboarding: onboardingStub,
      navigate,
    };
  }

  function heading(compiled: HTMLElement): string {
    return compiled.querySelector('h2')?.textContent?.trim() ?? '';
  }

  it('opens a fresh guide at the group step without writing any progress', async () => {
    const { compiled, onboarding } = await setup({
      progress: { status: 'NotStarted', setupGroupId: null, invitationsSkipped: false, version: 0 },
    });

    expect(onboarding.saveProgress).not.toHaveBeenCalled();
    expect(heading(compiled)).toBe('Create your group');
    expect(compiled.textContent).toContain('Step 1 of 7');
  });

  it('leaves a resumed deferred guide deferred', async () => {
    const { onboarding } = await setup({
      progress: progress({ status: 'Deferred', version: 3 }),
      setup: { ...COMPLETE, calendars: [] },
    });

    expect(onboarding.saveProgress).not.toHaveBeenCalled();
  });

  it('resumes at the first incomplete step without rewriting an active guide', async () => {
    const { compiled, onboarding } = await setup({
      setup: { ...COMPLETE, calendars: [], hasScheduledRoutine: false, hasMealAssignment: false },
    });

    expect(onboarding.saveProgress).not.toHaveBeenCalled();
    expect(heading(compiled)).toBe('Create a shared calendar');
    expect(compiled.textContent).toContain('Step 4 of 7');
  });

  it('marks finished steps with text, not colour alone', async () => {
    const { compiled } = await setup({ setup: { ...COMPLETE, hasMealAssignment: false } });

    const items = Array.from(compiled.querySelectorAll('nav li'));
    expect(items[0]?.textContent).toContain('(done)');
    expect(items[5]?.textContent).not.toContain('(done)');
    expect(items[5]?.getAttribute('aria-current')).toBe('step');
  });

  it('sends a completed guide back to the dashboard', async () => {
    const { navigate } = await setup({ progress: progress({ status: 'Completed' }) });

    expect(navigate).toHaveBeenCalledWith(['/guardian']);
  });

  it('sends a child account to the child home', async () => {
    const { navigate, onboarding } = await setup({ role: 'child' });

    expect(navigate).toHaveBeenCalledWith(['/child']);
    expect(onboarding.getProgress).not.toHaveBeenCalled();
  });

  it('shows a retryable error when the setup cannot be loaded', async () => {
    let fail = true;
    const { compiled, fixture } = await setup({
      onboarding: {
        loadSetup: vi.fn(async () => {
          if (fail) {
            throw new HttpErrorResponse({ status: 500 });
          }
          return EMPTY_SETUP;
        }),
      },
    });

    expect(compiled.textContent).toContain('Unable to load your setup.');

    fail = false;
    buttonByText(compiled, 'Try again')!.click();
    await settle(fixture);

    expect(heading(compiled)).toBe('Create your group');
  });

  it('only continues once the current step is done, and goes back', async () => {
    const { compiled, fixture } = await setup({ setup: { ...COMPLETE, hasMealAssignment: false } });

    expect(heading(compiled)).toBe('Plan a first meal');
    expect(buttonByText(compiled, 'Continue')!.disabled).toBe(true);

    buttonByText(compiled, 'Back')!.click();
    await settle(fixture);
    expect(heading(compiled)).toBe('Schedule a first routine');

    buttonByText(compiled, 'Continue')!.click();
    await settle(fixture);
    expect(heading(compiled)).toBe('Plan a first meal');
  });

  it('defers the guide and returns to the dashboard on finish later', async () => {
    const { compiled, fixture, onboarding, navigate } = await setup({ setup: COMPLETE });

    buttonByText(compiled, 'Finish later')!.click();
    await settle(fixture);

    expect(onboarding.saveProgress).toHaveBeenCalledWith(progress({ status: 'Deferred' }));
    expect(navigate).toHaveBeenCalledWith(['/guardian']);
  });

  it('completes the guide after confirming every step against current data', async () => {
    const { compiled, fixture, onboarding, navigate } = await setup({ setup: COMPLETE });

    expect(heading(compiled)).toBe('All set?');
    buttonByText(compiled, 'Finish setup')!.click();
    await settle(fixture);

    expect(onboarding.loadSetup).toHaveBeenCalledTimes(2);
    expect(onboarding.saveProgress).toHaveBeenCalledWith(progress({ status: 'Completed' }));
    expect(navigate).toHaveBeenCalledWith(['/guardian']);
  });

  it('refuses to finish when a step was undone elsewhere, and goes to it', async () => {
    const loadSetup = vi
      .fn<OnboardingService['loadSetup']>()
      .mockResolvedValueOnce(COMPLETE)
      .mockResolvedValue({ ...COMPLETE, calendars: [] });
    const { compiled, fixture, onboarding } = await setup({ onboarding: { loadSetup } });

    buttonByText(compiled, 'Finish setup')!.click();
    await settle(fixture);

    expect(onboarding.saveProgress).not.toHaveBeenCalled();
    expect(heading(compiled)).toBe('Create a shared calendar');
    expect(compiled.textContent).toContain('Some steps aren’t finished yet.');
  });

  it('does not complete the guide when the final check cannot load current data', async () => {
    const loadSetup = vi
      .fn<OnboardingService['loadSetup']>()
      .mockResolvedValueOnce(COMPLETE)
      .mockRejectedValue(new HttpErrorResponse({ status: 500 }));
    const { compiled, fixture, onboarding, navigate } = await setup({ onboarding: { loadSetup } });

    buttonByText(compiled, 'Finish setup')!.click();
    await settle(fixture);

    expect(onboarding.saveProgress).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
    expect(compiled.textContent).toContain('Unable to load your setup.');
  });

  it('reloads instead of overwriting when another window saved first', async () => {
    const getProgress = vi
      .fn<OnboardingService['getProgress']>()
      .mockResolvedValueOnce(progress())
      .mockResolvedValue(progress({ version: 5, invitationsSkipped: true }));
    const saveProgress = vi.fn(async () => {
      throw new HttpErrorResponse({ status: 409 });
    });
    const { compiled, fixture, navigate } = await setup({
      setup: COMPLETE,
      onboarding: { getProgress, saveProgress },
    });

    buttonByText(compiled, 'Finish later')!.click();
    await settle(fixture);

    expect(getProgress).toHaveBeenCalledTimes(2);
    expect(navigate).not.toHaveBeenCalled();
    expect(compiled.textContent).toContain('changed in another window');
    expect(compiled.textContent).not.toContain('Unable to save your progress.');
  });

  it('keeps the guardian on the step when saving progress fails', async () => {
    const { compiled, fixture, navigate } = await setup({
      setup: COMPLETE,
      onboarding: {
        saveProgress: vi.fn(async () => {
          throw new HttpErrorResponse({ status: 500 });
        }),
      },
    });

    buttonByText(compiled, 'Finish later')!.click();
    await settle(fixture);

    expect(navigate).not.toHaveBeenCalled();
    expect(compiled.textContent).toContain('Unable to save your progress. Try again.');
  });

  it('starts the guide when a fresh guardian creates the first group', async () => {
    const loadSetup = vi
      .fn<OnboardingService['loadSetup']>()
      .mockResolvedValueOnce(EMPTY_SETUP)
      .mockResolvedValue(setupWith());
    const { compiled, fixture, onboarding } = await setup({
      progress: { status: 'NotStarted', setupGroupId: null, invitationsSkipped: false, version: 0 },
      onboarding: { loadSetup },
    });
    const groups = TestBed.inject(GroupsService) as unknown as Record<string, unknown>;
    groups['createGroup'] = vi.fn(async () => ({ id: 'group-9', name: 'Home', role: 'Owner' }));

    const input = compiled.querySelector<HTMLInputElement>('#onboardingGroupName')!;
    input.value = 'Home';
    input.dispatchEvent(new Event('input'));
    await settle(fixture);
    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(onboarding.saveProgress).toHaveBeenCalledWith({
      status: 'Active',
      setupGroupId: 'group-9',
      invitationsSkipped: false,
      version: 0,
    });
  });

  it('stores the chosen group, then moves on to the children', async () => {
    const loadSetup = vi
      .fn<OnboardingService['loadSetup']>()
      .mockResolvedValueOnce(EMPTY_SETUP)
      .mockResolvedValue(setupWith());
    const { compiled, fixture, onboarding } = await setup({
      progress: progress({ setupGroupId: null }),
      onboarding: { loadSetup },
    });
    const groups = TestBed.inject(GroupsService) as unknown as Record<string, unknown>;
    groups['createGroup'] = vi.fn(async () => ({ id: 'group-9', name: 'Home', role: 'Owner' }));

    const input = compiled.querySelector<HTMLInputElement>('#onboardingGroupName')!;
    input.value = 'Home';
    input.dispatchEvent(new Event('input'));
    await settle(fixture);
    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(onboarding.saveProgress).toHaveBeenCalledWith(progress({ setupGroupId: 'group-9' }));
    expect(heading(compiled)).toBe('Add your children');
  });

  it('records an explicit skip of the invitations', async () => {
    const { compiled, fixture, onboarding } = await setup({
      setup: { ...COMPLETE, pendingInvites: [], calendars: [] },
    });

    expect(heading(compiled)).toContain('Invite other adults');
    expect(heading(compiled)).toContain('Optional');

    buttonByText(compiled, 'Skip for now')!.click();
    await settle(fixture);

    expect(onboarding.saveProgress).toHaveBeenCalledWith(progress({ invitationsSkipped: true }));
    expect(heading(compiled)).toBe('Create a shared calendar');
  });
});
