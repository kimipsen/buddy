import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  WritableSignal,
  computed,
  inject,
  linkedSignal,
  resource,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';

import { listTimeZoneIds } from '../../../../core/date-utils';
import {
  DEFAULT_LANGUAGE,
  LANGUAGE_NAMES,
  Language,
  SUPPORTED_LANGUAGES,
  isSupportedLanguage,
} from '../../../../core/i18n/language';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { CurrentUser, UsersService } from '../../../../core/users.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { ResendVerification } from '../../../../shared/resend-verification/resend-verification';

// The current user with the stored language narrowed to one the app supports (English otherwise).
type Profile = Omit<CurrentUser, 'language'> & { language: Language };

function toLanguage(language: string): Language {
  return isSupportedLanguage(language) ? language : DEFAULT_LANGUAGE;
}

@Component({
  selector: 'app-my-profile',
  imports: [FormsModule, ResendVerification, TranslatePipe],
  templateUrl: './my-profile.html',
})
export class MyProfile {
  private readonly users = inject(UsersService);

  // The backend's validation failures now come back as a structured envelope
  // ({ code, message, details, requestId }), not a bare string body.
  private apiErrorMessage(error: unknown, fallback: string): string {
    return error instanceof HttpErrorResponse &&
      error.error &&
      typeof error.error === 'object' &&
      'message' in error.error
      ? String(error.error.message)
      : fallback;
  }

  protected readonly timeZoneIds = listTimeZoneIds();
  protected readonly languages = SUPPORTED_LANGUAGES;
  protected readonly languageNames = LANGUAGE_NAMES;

  protected readonly profile = resource({ loader: () => this.loadProfile() });

  // The form fields start from the loaded profile and reset when a save changes the stored value
  // they mirror. Each one follows only its own stored value, so saving one section keeps unsaved
  // edits in the others. The form only renders once the profile has loaded, so the fallbacks
  // never show.
  protected readonly givenName = this.formField((profile) => profile?.name.givenName ?? '');
  protected readonly familyName = this.formField((profile) => profile?.name.familyName ?? '');
  protected readonly email = this.formField((profile) => profile?.email.value ?? '');
  protected readonly timeZoneId = this.formField((profile) => profile?.timeZoneId ?? '');
  protected readonly language = this.formField<Language>(
    (profile) => profile?.language ?? DEFAULT_LANGUAGE,
  );

  protected readonly nameSave = createAction();
  protected readonly nameSaved = signal(false);
  protected readonly emailSave = createAction();
  protected readonly emailSaved = signal(false);
  protected readonly timeZoneSave = createAction();
  protected readonly timeZoneSaved = signal(false);
  protected readonly languageSave = createAction();
  protected readonly languageSaved = signal(false);

  protected async saveName(): Promise<void> {
    const givenName = this.givenName().trim();
    const familyName = this.familyName().trim();

    if (!givenName || !familyName) {
      return;
    }

    this.nameSaved.set(false);
    await this.nameSave.run(
      true,
      async () => {
        const updated = await this.users.updateName(givenName, familyName);
        this.patchProfile({ name: updated.name });
        this.nameSaved.set(true);
      },
      'profile.name.error',
    );
  }

  protected async saveEmail(): Promise<void> {
    const email = this.email().trim();

    if (!email) {
      return;
    }

    this.emailSaved.set(false);
    await this.emailSave.run(
      true,
      async () => {
        const updated = await this.users.updateEmail(email);
        this.patchProfile({ email: updated.email });
        this.emailSaved.set(true);
      },
      (error) => this.apiErrorMessage(error, 'profile.email.error'),
    );
  }

  protected async saveTimeZone(): Promise<void> {
    const timeZoneId = this.timeZoneId();

    if (!timeZoneId) {
      return;
    }

    this.timeZoneSaved.set(false);
    await this.timeZoneSave.run(
      true,
      async () => {
        const updated = await this.users.updateTimeZone(timeZoneId);
        this.patchProfile({ timeZoneId: updated.timeZoneId });
        this.timeZoneSaved.set(true);
      },
      (error) => this.apiErrorMessage(error, 'profile.timeZone.error'),
    );
  }

  protected async saveLanguage(): Promise<void> {
    const language = this.language();

    if (!isSupportedLanguage(language)) {
      return;
    }

    this.languageSaved.set(false);
    await this.languageSave.run(
      true,
      async () => {
        const updated = await this.users.updateLanguage(language);
        this.patchProfile({ language: toLanguage(updated.language) });
        this.languageSaved.set(true);
      },
      (error) => this.apiErrorMessage(error, 'profile.language.error'),
    );
  }

  private async loadProfile(): Promise<Profile> {
    const user = await this.users.ensureCurrentUser();
    return { ...user, language: toLanguage(user.language) };
  }

  // A computed source only notifies when the field's value actually changes, so replacing the
  // profile object for another section's save doesn't reset this field.
  private formField<T>(read: (profile: Profile | undefined) => T): WritableSignal<T> {
    const stored = computed(() => read(this.profile.value()));
    return linkedSignal({ source: stored, computation: (value) => value });
  }

  private patchProfile(changes: Partial<Profile>): void {
    this.profile.update((current) => current && { ...current, ...changes });
  }
}
