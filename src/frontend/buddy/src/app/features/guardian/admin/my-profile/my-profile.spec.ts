import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { CurrentUser, UsersService } from '../../../../core/users.service';
import { MyProfile } from './my-profile';

describe('MyProfile', () => {
  const currentUser: CurrentUser = {
    id: 'user-1',
    email: { value: 'alice@buddy.test', isVerified: true },
    userName: 'alice',
    name: { givenName: 'Alice', familyName: 'Anderson' },
    timeZoneId: 'UTC',
    language: 'en'
  };

  async function setup() {
    const usersServiceStub: Partial<UsersService> = {
      ensureCurrentUser: () => Promise.resolve(currentUser),
      updateTimeZone: vi.fn(),
      updateLanguage: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [MyProfile],
      providers: [{ provide: UsersService, useValue: usersServiceStub }]
    }).compileComponents();

    const fixture = TestBed.createComponent(MyProfile);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;

    return { fixture, compiled };
  }

  function findSaveButton(compiled: HTMLElement, textFragment: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.type === 'submit' && button.textContent?.includes(textFragment)
    );
  }

  it('enables the save time zone button once the dropdown value changes', async () => {
    const { fixture, compiled } = await setup();
    const timeZoneSelect = compiled.querySelectorAll('select')[0];

    timeZoneSelect.value = 'Europe/Copenhagen';
    timeZoneSelect.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(findSaveButton(compiled, 'time zone')?.disabled).toBe(false);
  });

  it('enables the save language button once the dropdown value changes', async () => {
    const { fixture, compiled } = await setup();
    const languageSelect = compiled.querySelectorAll('select')[1];

    languageSelect.value = 'da';
    languageSelect.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(findSaveButton(compiled, 'language')?.disabled).toBe(false);
  });
});
