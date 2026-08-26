import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../core/auth.service';
import { Login } from './login';

// TranslatePipe/TranslationService are used unstubbed throughout (the same pattern as the other
// component specs in this app), so assertions below check the real English copy from
// core/i18n/translations/en/login.ts rather than raw translation keys.
describe('Login', () => {
  async function setup() {
    const authStub: Partial<AuthService> = {
      login: vi.fn(async () => undefined)
    };

    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [{ provide: AuthService, useValue: authStub }]
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
});
