import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { UsersService } from '../../core/users.service';
import { ResendVerification } from './resend-verification';

describe('ResendVerification', () => {
  async function setup(resendEmailVerification: () => Promise<void>) {
    const users = { resendEmailVerification: vi.fn(resendEmailVerification) };

    await TestBed.configureTestingModule({
      imports: [ResendVerification],
      providers: [{ provide: UsersService, useValue: users }],
    }).compileComponents();

    const fixture = TestBed.createComponent(ResendVerification);
    await settle(fixture);

    return { fixture, users, compiled: fixture.nativeElement as HTMLElement };
  }

  async function settle(fixture: ComponentFixture<ResendVerification>) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
  }

  function button(compiled: HTMLElement): HTMLButtonElement {
    return compiled.querySelector('button') as HTMLButtonElement;
  }

  it('sends the verification email again and confirms it', async () => {
    const { fixture, users, compiled } = await setup(async () => {});

    expect(button(compiled).textContent).toContain('Send verification email again');
    expect(compiled.textContent).not.toContain('Verification email sent.');

    button(compiled).click();
    await settle(fixture);

    expect(users.resendEmailVerification).toHaveBeenCalledTimes(1);
    expect(compiled.textContent).toContain('Verification email sent. Check your inbox.');
  });

  it('disables the button while sending', async () => {
    let finish!: () => void;
    const { fixture, compiled } = await setup(
      () => new Promise<void>((resolve) => (finish = resolve)),
    );

    button(compiled).click();
    fixture.detectChanges();
    expect(button(compiled).disabled).toBe(true);

    finish();
    await settle(fixture);
    expect(button(compiled).disabled).toBe(false);
  });

  it('explains the cooldown on a 409', async () => {
    const { fixture, compiled } = await setup(() =>
      Promise.reject(new HttpErrorResponse({ status: 409 })),
    );

    button(compiled).click();
    await settle(fixture);

    expect(compiled.textContent).toContain(
      'A verification email was sent less than a minute ago. Try again shortly.',
    );
    expect(compiled.textContent).not.toContain('Verification email sent.');
  });

  it('shows a generic error for other failures', async () => {
    const { fixture, compiled } = await setup(() =>
      Promise.reject(new HttpErrorResponse({ status: 500 })),
    );

    button(compiled).click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to send the verification email.');
  });

  it('clears an earlier confirmation when sending again fails', async () => {
    const { fixture, users, compiled } = await setup(async () => {});

    button(compiled).click();
    await settle(fixture);
    users.resendEmailVerification.mockImplementation(() =>
      Promise.reject(new HttpErrorResponse({ status: 409 })),
    );
    button(compiled).click();
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Verification email sent.');
  });
});
