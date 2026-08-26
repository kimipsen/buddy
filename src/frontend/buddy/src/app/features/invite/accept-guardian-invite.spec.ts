import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../core/auth.service';
import { GuardianInvitePreview, GuardiansService } from '../../core/guardians.service';
import { takePendingGuardianInviteToken } from '../../core/pending-guardian-invite-token';
import { AcceptGuardianInvite } from './accept-guardian-invite';

describe('AcceptGuardianInvite', () => {
  const token = 'guardian-invite-token-abc';

  interface Stubs {
    auth?: Partial<AuthService>;
    guardians?: Partial<GuardiansService>;
  }

  // Services are stubbed directly (not through HttpTestingController), so a plain
  // fixture.whenStable() resolves immediately without actually waiting for the mocked promise
  // chains -- see docs/testing.md. A macrotask flush lets already-scheduled microtasks drain.
  async function settle(fixture: { detectChanges: () => void }) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  async function setup(stubs: Stubs = {}) {
    const authStub: Partial<AuthService> = {
      isAuthenticated: signal(true).asReadonly(),
      login: vi.fn(async () => {}),
      ...stubs.auth
    };
    const guardiansStub: Partial<GuardiansService> = {
      previewGuardianInvite: vi.fn(async () => ({ childGivenName: 'Alex', kind: 0 }) as GuardianInvitePreview),
      acceptGuardianInvite: vi.fn(async () => {}),
      ...stubs.guardians
    };

    await TestBed.configureTestingModule({
      imports: [AcceptGuardianInvite],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authStub },
        { provide: GuardiansService, useValue: guardiansStub },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ token }) } }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(AcceptGuardianInvite);
    const router = TestBed.inject(Router);

    return { fixture, auth: authStub, guardians: guardiansStub, router };
  }

  beforeEach(() => {
    sessionStorage.clear();
  });

  it('shows the loading state while the preview is in flight', async () => {
    const { fixture } = await setup({
      guardians: { previewGuardianInvite: vi.fn(() => new Promise<GuardianInvitePreview>(() => {})) }
    });
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Loading invite…');
    expect(compiled.querySelector('button')).toBeNull();
  });

  it('requests the preview using the token from the route', async () => {
    const previewGuardianInvite = vi.fn(async () => ({ childGivenName: 'Alex', kind: 0 }) as GuardianInvitePreview);
    const { fixture } = await setup({ guardians: { previewGuardianInvite } });
    await settle(fixture);

    expect(previewGuardianInvite).toHaveBeenCalledWith(token);
    expect(previewGuardianInvite).toHaveBeenCalledTimes(1);
  });

  it('shows the translated error message when the preview fails to load', async () => {
    const { fixture } = await setup({
      guardians: { previewGuardianInvite: vi.fn(async () => Promise.reject(new Error('not found'))) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('This invite link is invalid or has expired.');
    expect(compiled.querySelector('button')).toBeNull();
  });

  it('renders the child name and kind for a parent invite once the preview loads', async () => {
    const { fixture } = await setup({
      guardians: { previewGuardianInvite: vi.fn(async () => ({ childGivenName: 'Alex', kind: 0 }) as GuardianInvitePreview) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain("You've been invited to help manage Alex's account, as a parent.");
  });

  it('renders the guardian kind label for a guardian invite', async () => {
    const { fixture } = await setup({
      guardians: { previewGuardianInvite: vi.fn(async () => ({ childGivenName: 'Sam', kind: 1 }) as GuardianInvitePreview) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain("You've been invited to help manage Sam's account, as a guardian.");
  });

  it('shows the accept button and no log-in prompt when already authenticated', async () => {
    const { fixture } = await setup({ auth: { isAuthenticated: signal(true).asReadonly() } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Accept invite');
    expect(compiled.textContent).not.toContain('Log in with the account this invite was sent to');
  });

  it('shows the log-in prompt instead of the accept button when not authenticated', async () => {
    const { fixture } = await setup({ auth: { isAuthenticated: signal(false).asReadonly() } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Log in with the account this invite was sent to, to accept it.');
    expect(compiled.textContent).toContain('Log in to accept');
    expect(compiled.textContent).not.toContain('Accept invite');
  });

  it('stores the pending token and starts login when logging in to accept', async () => {
    const login = vi.fn(async () => {});
    const { fixture } = await setup({ auth: { isAuthenticated: signal(false).asReadonly(), login } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const logInButton = Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.trim() === 'Log in to accept')!;
    logInButton.click();
    await settle(fixture);

    // takePendingGuardianInviteToken consumes (removes) the stored value, so reading it back here
    // also verifies the round trip through pending-guardian-invite-token.ts's real storage contract.
    expect(takePendingGuardianInviteToken()).toBe(token);
    expect(login).toHaveBeenCalledTimes(1);
  });

  it('accepts the invite with the exact route token and shows the success state', async () => {
    const acceptGuardianInvite = vi.fn(async () => {});
    const { fixture } = await setup({
      guardians: {
        previewGuardianInvite: vi.fn(async () => ({ childGivenName: 'Alex', kind: 0 }) as GuardianInvitePreview),
        acceptGuardianInvite
      }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const acceptButton = Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.trim() === 'Accept invite')!;
    acceptButton.click();
    await settle(fixture);

    expect(acceptGuardianInvite).toHaveBeenCalledWith(token);
    expect(compiled.textContent).toContain("You're now a guardian for Alex.");
    expect(compiled.textContent).toContain('Go to my children');
    expect(compiled.querySelector('button')?.textContent?.trim()).not.toBe('Accept invite');
  });

  it('disables the accept button while the accept request is in flight', async () => {
    const { fixture } = await setup({
      guardians: { acceptGuardianInvite: vi.fn(() => new Promise<void>(() => {})) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const acceptButton = compiled.querySelector('button') as HTMLButtonElement;
    acceptButton.click();
    await settle(fixture);

    expect(acceptButton.disabled).toBe(true);
    // Still on the preview screen, not the (unreachable) success screen -- confirms the button
    // that's disabled is genuinely the in-flight accept button and not a stray match.
    expect(compiled.textContent).toContain('Accept invite');
  });

  it('shows the generic accept error and re-enables the button when accepting fails', async () => {
    const { fixture } = await setup({
      guardians: { acceptGuardianInvite: vi.fn(async () => Promise.reject(new Error('boom'))) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const acceptButton = compiled.querySelector('button') as HTMLButtonElement;
    acceptButton.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to accept this invite. It may have expired or already been used.');
    expect(acceptButton.disabled).toBe(false);
  });

  it('shows the wrong-account error when accepting is rejected with a 403', async () => {
    const forbidden = new HttpErrorResponse({ status: 403 });
    const { fixture } = await setup({
      guardians: { acceptGuardianInvite: vi.fn(async () => Promise.reject(forbidden)) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    (compiled.querySelector('button') as HTMLButtonElement).click();
    await settle(fixture);

    expect(compiled.textContent).toContain("This invite was sent to a different account than the one you're logged in with.");
  });

  it('does not show the wrong-account error for a non-403 HttpErrorResponse', async () => {
    const serverError = new HttpErrorResponse({ status: 500 });
    const { fixture } = await setup({
      guardians: { acceptGuardianInvite: vi.fn(async () => Promise.reject(serverError)) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    (compiled.querySelector('button') as HTMLButtonElement).click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to accept this invite. It may have expired or already been used.');
    expect(compiled.textContent).not.toContain("This invite was sent to a different account than the one you're logged in with.");
  });

  it('clears a previous accept error on a retried accept attempt', async () => {
    const acceptGuardianInvite = vi.fn(async (): Promise<void> => Promise.reject(new Error('boom')));
    const { fixture } = await setup({ guardians: { acceptGuardianInvite } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const acceptButton = compiled.querySelector('button') as HTMLButtonElement;
    acceptButton.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to accept this invite. It may have expired or already been used.');

    acceptGuardianInvite.mockImplementation(() => new Promise<void>(() => {}));
    acceptButton.click();
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Unable to accept this invite. It may have expired or already been used.');
  });

  it('navigates to the guardian admin page when going to children after accepting', async () => {
    const { fixture, router } = await setup({
      guardians: {
        previewGuardianInvite: vi.fn(async () => ({ childGivenName: 'Alex', kind: 0 }) as GuardianInvitePreview),
        acceptGuardianInvite: vi.fn(async () => {})
      }
    });
    const navigateSpy = vi.spyOn(router, 'navigate');
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    (compiled.querySelector('button') as HTMLButtonElement).click();
    await settle(fixture);

    const goToChildrenButton = Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.trim() === 'Go to my children')!;
    goToChildrenButton.click();

    expect(navigateSpy).toHaveBeenCalledWith(['/guardian/admin']);
  });
});
