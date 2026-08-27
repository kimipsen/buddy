import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../core/auth.service';
import { CurrentUser, UsersService } from '../../core/users.service';
import { VerifyEmail } from './verify-email';

const STORAGE_KEY = 'buddy_pending_verify_email_token';

describe('VerifyEmail', () => {
  function fakeCurrentUser(overrides: Partial<CurrentUser> = {}): CurrentUser {
    return {
      id: 'user-1',
      email: { value: 'user@buddy.test', isVerified: true },
      userName: 'user',
      name: { givenName: 'Uma', familyName: 'User' },
      timeZoneId: 'UTC',
      language: 'en',
      ...overrides
    };
  }

  interface Stubs {
    auth?: Partial<AuthService>;
    users?: Partial<UsersService>;
    token?: string;
    // Real routing always supplies a :token path segment (see app.routes.ts: 'verify-email/:token'),
    // but the component reads it with `?? ''`, so exercise that defensive fallback directly rather
    // than only through a route config that can never actually omit it.
    omitTokenParam?: boolean;
  }

  async function setup(stubs: Stubs = {}) {
    const authStub: Partial<AuthService> = {
      isAuthenticated: signal(true).asReadonly(),
      login: vi.fn(async () => {}),
      ...stubs.auth
    };
    const usersStub: Partial<UsersService> = {
      verifyEmail: vi.fn(async () => fakeCurrentUser()),
      ...stubs.users
    };
    const token = stubs.token ?? 'verify-token-123';
    const paramMap = stubs.omitTokenParam ? convertToParamMap({}) : convertToParamMap({ token });

    await TestBed.configureTestingModule({
      imports: [VerifyEmail],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authStub },
        { provide: UsersService, useValue: usersStub },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(VerifyEmail);
    const router = TestBed.inject(Router);

    return { fixture, auth: authStub, users: usersStub, router, token };
  }

  // Zoneless: fixture.whenStable() only tracks HttpClient-style pending tasks, so it resolves
  // immediately for a plain Promise returned by a stubbed service. A setTimeout macrotask flush
  // reliably drains any depth of chained awaits instead -- see docs/testing.md.
  async function settle(fixture: ComponentFixture<unknown>) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  function text(fixture: ComponentFixture<unknown>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function findButton(fixture: ComponentFixture<unknown>, label: string): HTMLButtonElement | undefined {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === label
    );
  }

  describe('unauthenticated', () => {
    it('shows the log-in prompt and no verify button', async () => {
      const { fixture } = await setup({ auth: { isAuthenticated: signal(false).asReadonly() } });
      await settle(fixture);

      expect(text(fixture)).toContain('Verify your email address');
      expect(text(fixture)).toContain('Log in with the account this link was sent to, to verify it.');
      expect(findButton(fixture, 'Verify email')).toBeUndefined();
      expect(findButton(fixture, 'Log in to verify')).toBeTruthy();
    });

    it('stores the exact route token and starts login when logging in to verify', async () => {
      const { fixture, auth, token } = await setup({ auth: { isAuthenticated: signal(false).asReadonly() }, token: 'a-specific-token' });
      await settle(fixture);

      findButton(fixture, 'Log in to verify')!.click();

      expect(sessionStorage.getItem(STORAGE_KEY)).toBe(token);
      expect(auth.login).toHaveBeenCalledTimes(1);
    });

    it('falls back to storing an empty-string token when the route param is missing entirely', async () => {
      const { fixture } = await setup({ auth: { isAuthenticated: signal(false).asReadonly() }, omitTokenParam: true });
      await settle(fixture);

      findButton(fixture, 'Log in to verify')!.click();

      expect(sessionStorage.getItem(STORAGE_KEY)).toBe('');
    });
  });

  describe('authenticated', () => {
    it('shows the verify button and no log-in prompt', async () => {
      const { fixture } = await setup();
      await settle(fixture);

      expect(text(fixture)).toContain('Verify your email address');
      expect(findButton(fixture, 'Verify email')).toBeTruthy();
      expect(findButton(fixture, 'Log in to verify')).toBeUndefined();
      expect(text(fixture)).not.toContain('Log in with the account this link was sent to');
    });

    it('does not call verifyEmail automatically on init -- verification only starts on click', async () => {
      const { fixture, users } = await setup();
      await settle(fixture);

      expect(users.verifyEmail).not.toHaveBeenCalled();
    });

    it('verifies using the exact token from the route, exactly once per click', async () => {
      const { fixture, users, token } = await setup({ token: 'a-specific-token' });
      await settle(fixture);

      findButton(fixture, 'Verify email')!.click();
      await settle(fixture);

      expect(users.verifyEmail).toHaveBeenCalledWith('a-specific-token');
      expect(users.verifyEmail).toHaveBeenCalledTimes(1);
      expect(token).toBe('a-specific-token');
    });

    it('calls verifyEmail with an empty string when the route param is missing entirely', async () => {
      const { fixture, users } = await setup({ omitTokenParam: true });
      await settle(fixture);

      findButton(fixture, 'Verify email')!.click();
      await settle(fixture);

      expect(users.verifyEmail).toHaveBeenCalledWith('');
    });

    it('disables the verify button while verification is in flight, and hides it once verification succeeds', async () => {
      let resolveVerify!: (value: CurrentUser) => void;
      const { fixture } = await setup({
        users: { verifyEmail: vi.fn(() => new Promise<CurrentUser>((resolve) => (resolveVerify = resolve))) }
      });
      await settle(fixture);

      const button = findButton(fixture, 'Verify email')!;
      button.click();
      fixture.detectChanges();

      expect(button.disabled).toBe(true);

      resolveVerify(fakeCurrentUser());
      await settle(fixture);

      expect(findButton(fixture, 'Verify email')).toBeUndefined();
    });

    it('shows the success screen and a Continue button once verification succeeds, replacing the verify UI', async () => {
      const { fixture } = await setup();
      await settle(fixture);

      findButton(fixture, 'Verify email')!.click();
      await settle(fixture);

      expect(text(fixture)).toContain('Your email address is verified.');
      expect(findButton(fixture, 'Continue')).toBeTruthy();
      expect(findButton(fixture, 'Verify email')).toBeUndefined();
      expect(text(fixture)).not.toContain('Verify your email address');
    });

    it('navigates to the app root when clicking Continue on the success screen', async () => {
      const { fixture, router } = await setup();
      const navigateSpy = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
      await settle(fixture);

      findButton(fixture, 'Verify email')!.click();
      await settle(fixture);

      findButton(fixture, 'Continue')!.click();

      expect(navigateSpy).toHaveBeenCalledWith('/');
    });

    it('re-enables the verify button after a failed attempt so the user can retry', async () => {
      const { fixture } = await setup({ users: { verifyEmail: vi.fn(async () => Promise.reject(new Error('boom'))) } });
      await settle(fixture);

      findButton(fixture, 'Verify email')!.click();
      await settle(fixture);

      expect(findButton(fixture, 'Verify email')?.disabled).toBe(false);
    });

    it('shows the translated generic error message when verifyEmail rejects with a plain Error', async () => {
      const { fixture } = await setup({ users: { verifyEmail: vi.fn(async () => Promise.reject(new Error('boom'))) } });
      await settle(fixture);

      findButton(fixture, 'Verify email')!.click();
      await settle(fixture);

      expect(text(fixture)).toContain('Unable to verify this email. The link may have expired or already been used.');
      expect(text(fixture)).not.toContain('Your email address is verified.');
    });

    it('shows the backend validation message verbatim for an HttpErrorResponse with a structured error envelope body', async () => {
      const serverError = new HttpErrorResponse({
        error: { code: 'validation_error', message: 'This verification link has already been used.', details: {}, requestId: 'abc' },
        status: 400
      });
      const { fixture } = await setup({ users: { verifyEmail: vi.fn(async () => Promise.reject(serverError)) } });
      await settle(fixture);

      findButton(fixture, 'Verify email')!.click();
      await settle(fixture);

      expect(text(fixture)).toContain('This verification link has already been used.');
      expect(text(fixture)).not.toContain('Unable to verify this email. The link may have expired or already been used.');
    });

    it('falls back to the generic error message for an HttpErrorResponse with a plain string body', async () => {
      const serverError = new HttpErrorResponse({ error: 'This verification link has already been used.', status: 400 });
      const { fixture } = await setup({ users: { verifyEmail: vi.fn(async () => Promise.reject(serverError)) } });
      await settle(fixture);

      findButton(fixture, 'Verify email')!.click();
      await settle(fixture);

      expect(text(fixture)).toContain('Unable to verify this email. The link may have expired or already been used.');
    });

    it('falls back to the generic error message when the HttpErrorResponse body is an object with no message', async () => {
      const serverError = new HttpErrorResponse({ error: { code: 'TOKEN_EXPIRED' }, status: 400 });
      const { fixture } = await setup({ users: { verifyEmail: vi.fn(async () => Promise.reject(serverError)) } });
      await settle(fixture);

      findButton(fixture, 'Verify email')!.click();
      await settle(fixture);

      expect(text(fixture)).toContain('Unable to verify this email. The link may have expired or already been used.');
    });

    it('clears a previous error once a retry succeeds', async () => {
      const verifyEmail = vi.fn().mockRejectedValueOnce(new Error('boom')).mockResolvedValueOnce(fakeCurrentUser());
      const { fixture } = await setup({ users: { verifyEmail } });
      await settle(fixture);

      findButton(fixture, 'Verify email')!.click();
      await settle(fixture);
      expect(text(fixture)).toContain('Unable to verify this email.');

      findButton(fixture, 'Verify email')!.click();
      await settle(fixture);

      expect(text(fixture)).not.toContain('Unable to verify this email.');
      expect(text(fixture)).toContain('Your email address is verified.');
    });
  });
});
