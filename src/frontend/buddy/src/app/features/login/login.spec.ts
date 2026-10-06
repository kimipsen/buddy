import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../core/auth.service';
import { Login } from './login';

// TranslatePipe/TranslationService are used unstubbed throughout (the same pattern as the other
// component specs in this app), so assertions below check the real English copy from
// core/i18n/translations/en/login.ts rather than raw translation keys.
describe('Login', () => {
  async function setup(queryParams: Record<string, string> = {}) {
    const authStub: Partial<AuthService> = {
      login: vi.fn(async () => undefined),
    };

    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        { provide: AuthService, useValue: authStub },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(Login);

    return { fixture, auth: authStub };
  }

  it('renders the sign-in card', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Welcome back');
    expect(compiled.querySelector('button')?.textContent).toContain('Sign in with Keycloak');
  });

  it('calls AuthService.login exactly once when the sign-in button is clicked', async () => {
    const { fixture, auth } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    compiled.querySelector('button')!.dispatchEvent(new Event('click'));

    expect(auth.login).toHaveBeenCalledOnce();
  });

  it('explains that the session expired when sent here for that reason', async () => {
    const { fixture } = await setup({ reason: 'session-expired' });
    fixture.detectChanges();

    const notice = (fixture.nativeElement as HTMLElement).querySelector('[role="status"]');
    expect(notice?.textContent).toContain('Your session expired. Sign in again to continue.');
  });

  it('shows no session notice on a plain visit or for another reason', async () => {
    for (const queryParams of [{}, { reason: 'something-else' }] as Record<string, string>[]) {
      TestBed.resetTestingModule();
      const { fixture } = await setup(queryParams);
      fixture.detectChanges();

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('[role="status"]')).toBeNull();
      expect(compiled.textContent).not.toContain('Your session expired');
    }
  });
});
