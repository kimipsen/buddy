import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { AccountRole, AccountService } from './account.service';
import { onboardingEntryGuard } from './onboarding.guard';
import { OnboardingService } from './onboarding.service';

describe('onboardingEntryGuard', () => {
  function setup(options: {
    role?: () => Promise<AccountRole>;
    shouldEnterGuide?: () => Promise<boolean>;
  }) {
    const shouldEnterGuide = vi.fn(options.shouldEnterGuide ?? (async () => false));

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AccountService,
          useValue: { resolveRole: vi.fn(options.role ?? (async () => 'guardian')) },
        },
        { provide: OnboardingService, useValue: { shouldEnterGuide } },
      ],
    });

    const run = () =>
      TestBed.runInInjectionContext(() =>
        onboardingEntryGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
      ) as Promise<boolean | UrlTree>;

    return { run, shouldEnterGuide, router: TestBed.inject(Router) };
  }

  it('sends an eligible guardian to the guide', async () => {
    const { run, router } = setup({ shouldEnterGuide: async () => true });

    const result = await run();

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe('/guardian/onboarding');
  });

  it('lets a guardian who does not need the guide through to the home', async () => {
    const { run } = setup({ shouldEnterGuide: async () => false });

    await expect(run()).resolves.toBe(true);
  });

  it('never sends a child account into the guide', async () => {
    const { run, shouldEnterGuide } = setup({
      role: async () => 'child',
      shouldEnterGuide: async () => true,
    });

    await expect(run()).resolves.toBe(true);
    expect(shouldEnterGuide).not.toHaveBeenCalled();
  });

  it('shows the home instead of guessing when the eligibility lookup fails', async () => {
    const { run } = setup({
      shouldEnterGuide: async () => {
        throw new Error('network');
      },
    });

    await expect(run()).resolves.toBe(true);
  });

  it('shows the home when the role lookup fails', async () => {
    const { run, shouldEnterGuide } = setup({
      role: async () => {
        throw new Error('network');
      },
    });

    await expect(run()).resolves.toBe(true);
    expect(shouldEnterGuide).not.toHaveBeenCalled();
  });
});
