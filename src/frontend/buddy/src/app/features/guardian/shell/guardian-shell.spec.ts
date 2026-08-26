import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../core/auth.service';
import { GuardianShell } from './guardian-shell';

// GuardianShell is a pure composition shell -- a static header (brand, title, and an already
// covered app-profile-menu) plus a router-outlet, with no logic of its own. This spec only checks
// that it constructs and renders without throwing, and that its pieces are present, mirroring the
// other shell specs in this phase.
describe('GuardianShell', () => {
  async function setup() {
    const authStub: Partial<AuthService> = { logout: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [GuardianShell],
      providers: [provideRouter([]), { provide: AuthService, useValue: authStub }]
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianShell);

    return { fixture };
  }

  it('renders the brand header, the profile menu, and a router outlet without throwing', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Buddy');
    expect(compiled.textContent).toContain('Guardian dashboard');
    expect(compiled.querySelector('a[href="/guardian"]')).toBeTruthy();
    expect(compiled.querySelector('app-profile-menu')).toBeTruthy();
    expect(compiled.querySelector('router-outlet')).toBeTruthy();
  });
});
