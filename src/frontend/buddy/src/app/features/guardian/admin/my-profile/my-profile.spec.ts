import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WritableSignal } from '@angular/core';
import { describe, expect, it, vi } from 'vitest';

import { Language } from '../../../../core/i18n/language';
import { CurrentUser, UsersService } from '../../../../core/users.service';
import { MyProfile } from './my-profile';

describe('MyProfile', () => {
  const currentUser: CurrentUser = {
    id: 'user-1',
    email: { value: 'alice@buddy.test', isVerified: true },
    userName: 'alice',
    name: { givenName: 'Alice', familyName: 'Anderson' },
    timeZoneId: 'UTC',
    language: 'en',
  };

  function withChanges(changes: Partial<CurrentUser>): CurrentUser {
    return { ...currentUser, ...changes };
  }

  function deferred<T>() {
    let resolve!: (value: T) => void;
    let reject!: (reason: unknown) => void;
    const promise = new Promise<T>((res, rej) => {
      resolve = res;
      reject = rej;
    });
    return { promise, resolve, reject };
  }

  function apiError(body: unknown): HttpErrorResponse {
    return new HttpErrorResponse({ status: 400, error: body });
  }

  interface UsersStub {
    ensureCurrentUser: ReturnType<typeof vi.fn>;
    updateName: ReturnType<typeof vi.fn>;
    updateEmail: ReturnType<typeof vi.fn>;
    updateTimeZone: ReturnType<typeof vi.fn>;
    updateLanguage: ReturnType<typeof vi.fn>;
  }

  async function setup(options: { user?: Promise<CurrentUser>; settleLoad?: boolean } = {}) {
    const users: UsersStub = {
      ensureCurrentUser: vi.fn(() => options.user ?? Promise.resolve(currentUser)),
      updateName: vi.fn(),
      updateEmail: vi.fn(),
      updateTimeZone: vi.fn(),
      updateLanguage: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [MyProfile],
      providers: [{ provide: UsersService, useValue: users }],
    }).compileComponents();

    const fixture = TestBed.createComponent(MyProfile);
    const compiled = fixture.nativeElement as HTMLElement;

    if (options.settleLoad === false) {
      fixture.detectChanges();
    } else {
      await settle(fixture);
    }

    return { fixture, compiled, users };
  }

  // The save flows chain an awaited service call before the signals settle, so flush a few rounds.
  async function settle(fixture: ComponentFixture<MyProfile>) {
    fixture.detectChanges();

    for (let i = 0; i < 5; i++) {
      await fixture.whenStable();
      fixture.detectChanges();
    }
  }

  // The save guards (blank time zone, unsupported language) can't be reached through the template
  // because the selects only offer valid options, and jsdom strips surrounding whitespace from
  // type="email" input values, so those tests set the signal directly.
  interface MyProfileInternals {
    email: WritableSignal<string>;
    saveEmail(): Promise<void>;
    timeZoneId: WritableSignal<string>;
    language: WritableSignal<Language>;
    saveTimeZone(): Promise<void>;
    saveLanguage(): Promise<void>;
  }

  function internals(fixture: ComponentFixture<MyProfile>): MyProfileInternals {
    return fixture.componentInstance as unknown as MyProfileInternals;
  }

  function findSaveButton(
    compiled: HTMLElement,
    textFragment: string,
  ): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.type === 'submit' && button.textContent?.includes(textFragment),
    );
  }

  function input(compiled: HTMLElement, name: string): HTMLInputElement {
    return compiled.querySelector<HTMLInputElement>(`input[name="${name}"]`)!;
  }

  function select(compiled: HTMLElement, name: string): HTMLSelectElement {
    return compiled.querySelector<HTMLSelectElement>(`select[name="${name}"]`)!;
  }

  async function type(
    fixture: ComponentFixture<MyProfile>,
    element: HTMLInputElement,
    value: string,
  ) {
    element.value = value;
    element.dispatchEvent(new Event('input'));
    await settle(fixture);
  }

  async function choose(
    fixture: ComponentFixture<MyProfile>,
    element: HTMLSelectElement,
    value: string,
  ) {
    element.value = value;
    element.dispatchEvent(new Event('change'));
    await settle(fixture);
  }

  async function submit(
    fixture: ComponentFixture<MyProfile>,
    button: HTMLButtonElement | undefined,
  ) {
    button!.closest('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);
  }

  function hasVerifiedBadge(compiled: HTMLElement): boolean {
    return Array.from(compiled.querySelectorAll('span')).some(
      (span) => span.textContent?.trim() === 'Verified',
    );
  }

  describe('loading', () => {
    it('shows the loading message until the current user resolves', async () => {
      const user = deferred<CurrentUser>();
      const { fixture, compiled } = await setup({ user: user.promise, settleLoad: false });

      expect(compiled.textContent).toContain('Loading your profile…');
      expect(compiled.querySelector('form')).toBeNull();

      user.resolve(currentUser);
      await settle(fixture);

      expect(compiled.textContent).not.toContain('Loading your profile…');
      expect(compiled.querySelector('form')).not.toBeNull();
    });

    it('shows the load error instead of the forms when the current user fails to load', async () => {
      const { compiled } = await setup({ user: Promise.reject(new Error('boom')) });

      expect(compiled.textContent).toContain('Unable to load your profile.');
      expect(compiled.textContent).not.toContain('Loading your profile…');
      expect(compiled.querySelector('form')).toBeNull();
    });

    it('fills every field from the current user', async () => {
      const { compiled } = await setup({
        user: Promise.resolve(
          withChanges({
            name: { givenName: 'Bob', familyName: 'Builder' },
            email: { value: 'bob@buddy.test', isVerified: true },
            timeZoneId: 'Europe/Copenhagen',
            language: 'da',
          }),
        ),
      });

      expect(input(compiled, 'givenName').value).toBe('Bob');
      expect(input(compiled, 'familyName').value).toBe('Builder');
      expect(input(compiled, 'email').value).toBe('bob@buddy.test');
      expect(select(compiled, 'timeZoneId').value).toBe('Europe/Copenhagen');
      expect(select(compiled, 'language').value).toBe('da');
      expect(hasVerifiedBadge(compiled)).toBe(true);
      expect(findSaveButton(compiled, 'name')?.disabled).toBe(true);
      expect(findSaveButton(compiled, 'email')?.disabled).toBe(true);
      expect(findSaveButton(compiled, 'time zone')?.disabled).toBe(true);
      expect(findSaveButton(compiled, 'language')?.disabled).toBe(true);
    });

    it('hides the verified badge when the current email is unverified', async () => {
      const { compiled } = await setup({
        user: Promise.resolve(
          withChanges({ email: { value: 'alice@buddy.test', isVerified: false } }),
        ),
      });

      expect(hasVerifiedBadge(compiled)).toBe(false);
    });

    it('falls back to English when the stored language is not supported', async () => {
      const { compiled } = await setup({ user: Promise.resolve(withChanges({ language: 'fr' })) });

      expect(select(compiled, 'language').value).toBe('en');
      expect(findSaveButton(compiled, 'language')?.disabled).toBe(true);
    });

    it('shows no saved or error messages before anything is saved', async () => {
      const { compiled } = await setup();

      expect(compiled.textContent).not.toContain('Name updated.');
      expect(compiled.textContent).not.toContain('Email updated.');
      expect(compiled.textContent).not.toContain('Time zone updated.');
      expect(compiled.textContent).not.toContain('Language updated.');
      expect(compiled.querySelector('p.text-red-600')).toBeNull();
    });
  });

  describe('name', () => {
    it('keeps the save name button disabled until a field actually changes', async () => {
      const { compiled } = await setup();

      expect(findSaveButton(compiled, 'name')?.disabled).toBe(true);
    });

    it('enables the save name button once a field changes', async () => {
      const { fixture, compiled } = await setup();

      await type(fixture, input(compiled, 'givenName'), 'Alicia');

      expect(findSaveButton(compiled, 'name')?.disabled).toBe(false);
    });

    it('saves the trimmed name, confirms it and treats it as the new current name', async () => {
      const { fixture, compiled, users } = await setup();
      users.updateName.mockResolvedValue(
        withChanges({ name: { givenName: 'Alicia', familyName: 'Andersen' } }),
      );

      await type(fixture, input(compiled, 'givenName'), '  Alicia  ');
      await type(fixture, input(compiled, 'familyName'), ' Andersen ');
      await submit(fixture, findSaveButton(compiled, 'name'));

      expect(users.updateName).toHaveBeenCalledExactlyOnceWith('Alicia', 'Andersen');
      expect(compiled.textContent).toContain('Name updated.');
      expect(findSaveButton(compiled, 'name')?.disabled).toBe(true);
    });

    it('disables the button and hides earlier feedback while the save is in flight', async () => {
      const { fixture, compiled, users } = await setup();
      users.updateName.mockRejectedValueOnce(new Error('boom'));
      await type(fixture, input(compiled, 'givenName'), 'Alicia');
      await submit(fixture, findSaveButton(compiled, 'name'));
      expect(compiled.textContent).toContain('Unable to update your name.');

      const pending = deferred<CurrentUser>();
      users.updateName.mockReturnValueOnce(pending.promise);
      await submit(fixture, findSaveButton(compiled, 'name'));

      expect(findSaveButton(compiled, 'name')?.disabled).toBe(true);
      expect(compiled.textContent).not.toContain('Unable to update your name.');
      expect(compiled.textContent).not.toContain('Name updated.');

      pending.resolve(withChanges({ name: { givenName: 'Alicia', familyName: 'Anderson' } }));
      await settle(fixture);

      expect(compiled.textContent).toContain('Name updated.');
    });

    it('shows the name error and re-enables the button when the save fails', async () => {
      const { fixture, compiled, users } = await setup();
      users.updateName.mockRejectedValue(apiError({ message: 'ignored for names' }));

      await type(fixture, input(compiled, 'givenName'), 'Alicia');
      await submit(fixture, findSaveButton(compiled, 'name'));

      expect(compiled.textContent).toContain('Unable to update your name.');
      expect(compiled.textContent).not.toContain('Name updated.');
      expect(findSaveButton(compiled, 'name')?.disabled).toBe(false);
    });

    it('does not save when the given name is blank', async () => {
      const { fixture, compiled, users } = await setup();

      await type(fixture, input(compiled, 'givenName'), '   ');
      await submit(fixture, findSaveButton(compiled, 'name'));

      expect(users.updateName).not.toHaveBeenCalled();
    });

    it('does not save when the family name is blank', async () => {
      const { fixture, compiled, users } = await setup();

      await type(fixture, input(compiled, 'familyName'), '   ');
      await submit(fixture, findSaveButton(compiled, 'name'));

      expect(users.updateName).not.toHaveBeenCalled();
    });
  });

  describe('email', () => {
    it('enables the save email button once the address changes', async () => {
      const { fixture, compiled } = await setup();

      await type(fixture, input(compiled, 'email'), 'alicia@buddy.test');

      expect(findSaveButton(compiled, 'email')?.disabled).toBe(false);
    });

    it('saves the trimmed email and adopts the returned address and verification state', async () => {
      const { fixture, compiled, users } = await setup();
      users.updateEmail.mockResolvedValue(
        withChanges({ email: { value: 'alicia@buddy.test', isVerified: false } }),
      );

      await type(fixture, input(compiled, 'email'), 'alicia@buddy.test');
      await submit(fixture, findSaveButton(compiled, 'email'));

      expect(users.updateEmail).toHaveBeenCalledExactlyOnceWith('alicia@buddy.test');
      expect(compiled.textContent).toContain('Email updated. Check your inbox to verify it.');
      expect(findSaveButton(compiled, 'email')?.disabled).toBe(true);
      expect(hasVerifiedBadge(compiled)).toBe(false);
    });

    it('trims the email before saving it', async () => {
      const { fixture, users } = await setup();
      users.updateEmail.mockResolvedValue(
        withChanges({ email: { value: 'alicia@buddy.test', isVerified: false } }),
      );

      internals(fixture).email.set('  alicia@buddy.test ');
      await internals(fixture).saveEmail();

      expect(users.updateEmail).toHaveBeenCalledExactlyOnceWith('alicia@buddy.test');
    });

    it('disables the button and hides earlier feedback while the save is in flight', async () => {
      const { fixture, compiled, users } = await setup();
      users.updateEmail.mockRejectedValueOnce(new Error('boom'));
      await type(fixture, input(compiled, 'email'), 'alicia@buddy.test');
      await submit(fixture, findSaveButton(compiled, 'email'));
      expect(compiled.textContent).toContain('Unable to update your email.');

      const pending = deferred<CurrentUser>();
      users.updateEmail.mockReturnValueOnce(pending.promise);
      await submit(fixture, findSaveButton(compiled, 'email'));

      expect(findSaveButton(compiled, 'email')?.disabled).toBe(true);
      expect(compiled.textContent).not.toContain('Unable to update your email.');
      expect(compiled.textContent).not.toContain('Email updated.');

      pending.resolve(withChanges({ email: { value: 'alicia@buddy.test', isVerified: false } }));
      await settle(fixture);

      expect(compiled.textContent).toContain('Email updated.');
    });

    it('shows the message from a structured API error and re-enables the button', async () => {
      const { fixture, compiled, users } = await setup();
      users.updateEmail.mockRejectedValue(
        apiError({ code: 'conflict', message: 'Email already in use' }),
      );

      await type(fixture, input(compiled, 'email'), 'bob@buddy.test');
      await submit(fixture, findSaveButton(compiled, 'email'));

      expect(compiled.textContent).toContain('Email already in use');
      expect(compiled.textContent).not.toContain('Email updated.');
      expect(findSaveButton(compiled, 'email')?.disabled).toBe(false);
    });

    it.each([
      ['a non-HTTP error', new Error('boom')],
      ['an HTTP error without a body', apiError(null)],
      ['an HTTP error with a plain-text body', apiError('Bad request')],
      ['an HTTP error whose body has no message', apiError({ code: 'conflict' })],
    ])('falls back to the generic email error for %s', async (_label, error) => {
      const { fixture, compiled, users } = await setup();
      users.updateEmail.mockRejectedValue(error);

      await type(fixture, input(compiled, 'email'), 'bob@buddy.test');
      await submit(fixture, findSaveButton(compiled, 'email'));

      expect(compiled.textContent).toContain('Unable to update your email.');
    });

    it('does not save a blank email', async () => {
      const { fixture, compiled, users } = await setup();

      await type(fixture, input(compiled, 'email'), '   ');
      await submit(fixture, findSaveButton(compiled, 'email'));

      expect(users.updateEmail).not.toHaveBeenCalled();
    });
  });

  describe('time zone', () => {
    it('enables the save time zone button once the dropdown value changes', async () => {
      const { fixture, compiled } = await setup();

      await choose(fixture, select(compiled, 'timeZoneId'), 'Europe/Copenhagen');

      expect(findSaveButton(compiled, 'time zone')?.disabled).toBe(false);
    });

    it('saves the chosen time zone and treats it as the current one', async () => {
      const { fixture, compiled, users } = await setup();
      users.updateTimeZone.mockResolvedValue(withChanges({ timeZoneId: 'Europe/Copenhagen' }));

      await choose(fixture, select(compiled, 'timeZoneId'), 'Europe/Copenhagen');
      await submit(fixture, findSaveButton(compiled, 'time zone'));

      expect(users.updateTimeZone).toHaveBeenCalledExactlyOnceWith('Europe/Copenhagen');
      expect(compiled.textContent).toContain(
        'Time zone updated. Timestamps across the app now use it.',
      );
      expect(findSaveButton(compiled, 'time zone')?.disabled).toBe(true);
    });

    it('disables the button and hides earlier feedback while the save is in flight', async () => {
      const { fixture, compiled, users } = await setup();
      users.updateTimeZone.mockRejectedValueOnce(new Error('boom'));
      await choose(fixture, select(compiled, 'timeZoneId'), 'Europe/Copenhagen');
      await submit(fixture, findSaveButton(compiled, 'time zone'));
      expect(compiled.textContent).toContain('Unable to update your time zone.');

      const pending = deferred<CurrentUser>();
      users.updateTimeZone.mockReturnValueOnce(pending.promise);
      await submit(fixture, findSaveButton(compiled, 'time zone'));

      expect(findSaveButton(compiled, 'time zone')?.disabled).toBe(true);
      expect(compiled.textContent).not.toContain('Unable to update your time zone.');
      expect(compiled.textContent).not.toContain('Time zone updated.');

      pending.resolve(withChanges({ timeZoneId: 'Europe/Copenhagen' }));
      await settle(fixture);

      expect(compiled.textContent).toContain('Time zone updated.');
    });

    it('shows the API error message and re-enables the button when the save fails', async () => {
      const { fixture, compiled, users } = await setup();
      users.updateTimeZone.mockRejectedValue(apiError({ message: 'Unknown time zone' }));

      await choose(fixture, select(compiled, 'timeZoneId'), 'Europe/Copenhagen');
      await submit(fixture, findSaveButton(compiled, 'time zone'));

      expect(compiled.textContent).toContain('Unknown time zone');
      expect(findSaveButton(compiled, 'time zone')?.disabled).toBe(false);
    });

    it('falls back to the generic time zone error', async () => {
      const { fixture, compiled, users } = await setup();
      users.updateTimeZone.mockRejectedValue(new Error('boom'));

      await choose(fixture, select(compiled, 'timeZoneId'), 'Europe/Copenhagen');
      await submit(fixture, findSaveButton(compiled, 'time zone'));

      expect(compiled.textContent).toContain('Unable to update your time zone.');
    });

    it('does not save an empty time zone', async () => {
      const { fixture, users } = await setup();

      internals(fixture).timeZoneId.set('');
      await internals(fixture).saveTimeZone();

      expect(users.updateTimeZone).not.toHaveBeenCalled();
    });
  });

  describe('language', () => {
    it('enables the save language button once the dropdown value changes', async () => {
      const { fixture, compiled } = await setup();

      await choose(fixture, select(compiled, 'language'), 'da');

      expect(findSaveButton(compiled, 'language')?.disabled).toBe(false);
    });

    it('saves the chosen language and treats it as the current one', async () => {
      const { fixture, compiled, users } = await setup();
      users.updateLanguage.mockResolvedValue(withChanges({ language: 'da' }));

      await choose(fixture, select(compiled, 'language'), 'da');
      await submit(fixture, findSaveButton(compiled, 'language'));

      expect(users.updateLanguage).toHaveBeenCalledExactlyOnceWith('da');
      expect(compiled.textContent).toContain('Language updated.');
      expect(findSaveButton(compiled, 'language')?.disabled).toBe(true);
    });

    it('treats an unsupported language in the response as English', async () => {
      const { fixture, compiled, users } = await setup();
      users.updateLanguage.mockResolvedValue(withChanges({ language: 'fr' }));

      await choose(fixture, select(compiled, 'language'), 'da');
      await submit(fixture, findSaveButton(compiled, 'language'));
      await choose(fixture, select(compiled, 'language'), 'en');

      expect(findSaveButton(compiled, 'language')?.disabled).toBe(true);
    });

    it('disables the button and hides earlier feedback while the save is in flight', async () => {
      const { fixture, compiled, users } = await setup();
      users.updateLanguage.mockRejectedValueOnce(new Error('boom'));
      await choose(fixture, select(compiled, 'language'), 'da');
      await submit(fixture, findSaveButton(compiled, 'language'));
      expect(compiled.textContent).toContain('Unable to update your language.');

      const pending = deferred<CurrentUser>();
      users.updateLanguage.mockReturnValueOnce(pending.promise);
      await submit(fixture, findSaveButton(compiled, 'language'));

      expect(findSaveButton(compiled, 'language')?.disabled).toBe(true);
      expect(compiled.textContent).not.toContain('Unable to update your language.');
      expect(compiled.textContent).not.toContain('Language updated.');

      pending.resolve(withChanges({ language: 'da' }));
      await settle(fixture);

      expect(compiled.textContent).toContain('Language updated.');
    });

    it('shows the API error message and re-enables the button when the save fails', async () => {
      const { fixture, compiled, users } = await setup();
      users.updateLanguage.mockRejectedValue(apiError({ message: 'Unsupported language' }));

      await choose(fixture, select(compiled, 'language'), 'da');
      await submit(fixture, findSaveButton(compiled, 'language'));

      expect(compiled.textContent).toContain('Unsupported language');
      expect(findSaveButton(compiled, 'language')?.disabled).toBe(false);
    });

    it('falls back to the generic language error', async () => {
      const { fixture, compiled, users } = await setup();
      users.updateLanguage.mockRejectedValue(new Error('boom'));

      await choose(fixture, select(compiled, 'language'), 'da');
      await submit(fixture, findSaveButton(compiled, 'language'));

      expect(compiled.textContent).toContain('Unable to update your language.');
    });

    it('does not save an unsupported language', async () => {
      const { fixture, users } = await setup();

      internals(fixture).language.set('fr' as Language);
      await internals(fixture).saveLanguage();

      expect(users.updateLanguage).not.toHaveBeenCalled();
    });
  });
});
