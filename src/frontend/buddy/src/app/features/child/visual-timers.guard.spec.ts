import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { CurrentUser, UsersService } from '../../core/users.service';
import { visualTimersGuard } from './visual-timers.guard';

describe('visualTimersGuard', () => {
  const child: CurrentUser = {
    id: 'child-1',
    email: { value: 'kid@buddy.test', isVerified: true },
    userName: 'kid',
    name: { givenName: 'Ida', familyName: 'Kid' },
    callingName: '',
    timeZoneId: 'UTC',
    language: 'en',
    showVisualTimers: true,
    homeworkChunks: { focusMinutes: 15, breakMinutes: 5 },
  };

  async function run(ensureCurrentUser: () => Promise<CurrentUser>) {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: UsersService, useValue: { ensureCurrentUser } }],
    });

    return TestBed.runInInjectionContext(() =>
      visualTimersGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );
  }

  it('lets the child in while their guardian keeps timers on', async () => {
    await expect(run(vi.fn(async () => child))).resolves.toBe(true);
  });

  it('sends the child home when their guardian turned timers off', async () => {
    const result = await run(vi.fn(async () => ({ ...child, showVisualTimers: false })));

    expect(result).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/child');
  });

  it('sends the child home when they cannot be resolved', async () => {
    const result = await run(vi.fn(async () => Promise.reject(new Error('offline'))));

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/child');
  });
});
