import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { listTimeZoneIds } from '../../../../core/date-utils';
import { LANGUAGE_NAMES, Language, SUPPORTED_LANGUAGES, isSupportedLanguage } from '../../../../core/i18n/language';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { CurrentUser, UsersService } from '../../../../core/users.service';

@Component({
  selector: 'app-my-profile',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './my-profile.html'
})
export class MyProfile implements OnInit {
  private readonly users = inject(UsersService);

  protected readonly timeZoneIds = listTimeZoneIds();
  protected readonly languages = SUPPORTED_LANGUAGES;
  protected readonly languageNames = LANGUAGE_NAMES;

  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly currentEmail = signal<string | null>(null);

  protected readonly givenName = signal('');
  protected readonly familyName = signal('');
  protected readonly currentGivenName = signal('');
  protected readonly currentFamilyName = signal('');
  protected readonly savingName = signal(false);
  protected readonly nameError = signal<string | null>(null);
  protected readonly nameSaved = signal(false);

  protected readonly email = signal('');
  protected readonly savingEmail = signal(false);
  protected readonly emailError = signal<string | null>(null);
  protected readonly emailSaved = signal(false);

  protected readonly timeZoneId = signal('UTC');
  protected readonly currentTimeZoneId = signal<string | null>(null);
  protected readonly savingTimeZone = signal(false);
  protected readonly timeZoneError = signal<string | null>(null);
  protected readonly timeZoneSaved = signal(false);

  protected readonly language = signal<Language>('en');
  protected readonly currentLanguage = signal<Language | null>(null);
  protected readonly savingLanguage = signal(false);
  protected readonly languageError = signal<string | null>(null);
  protected readonly languageSaved = signal(false);

  ngOnInit(): void {
    void this.loadProfile();
  }

  protected async saveName(): Promise<void> {
    const givenName = this.givenName().trim();
    const familyName = this.familyName().trim();

    if (!givenName || !familyName) {
      return;
    }

    this.savingName.set(true);
    this.nameError.set(null);
    this.nameSaved.set(false);

    try {
      const updated = await this.users.updateName(givenName, familyName);
      this.currentGivenName.set(updated.name.givenName);
      this.currentFamilyName.set(updated.name.familyName);
      this.nameSaved.set(true);
    } catch {
      this.nameError.set('profile.name.error');
    } finally {
      this.savingName.set(false);
    }
  }

  protected async saveEmail(): Promise<void> {
    const email = this.email().trim();

    if (!email) {
      return;
    }

    this.savingEmail.set(true);
    this.emailError.set(null);
    this.emailSaved.set(false);

    try {
      const updated = await this.users.updateEmail(email);
      this.currentEmail.set(updated.email.value);
      this.emailSaved.set(true);
    } catch (error) {
      this.emailError.set(
        error instanceof HttpErrorResponse && typeof error.error === 'string' ? error.error : 'profile.email.error'
      );
    } finally {
      this.savingEmail.set(false);
    }
  }

  protected async saveTimeZone(): Promise<void> {
    const timeZoneId = this.timeZoneId();

    if (!timeZoneId) {
      return;
    }

    this.savingTimeZone.set(true);
    this.timeZoneError.set(null);
    this.timeZoneSaved.set(false);

    try {
      const updated = await this.users.updateTimeZone(timeZoneId);
      this.currentTimeZoneId.set(updated.timeZoneId);
      this.timeZoneSaved.set(true);
    } catch (error) {
      this.timeZoneError.set(
        error instanceof HttpErrorResponse && typeof error.error === 'string' ? error.error : 'profile.timeZone.error'
      );
    } finally {
      this.savingTimeZone.set(false);
    }
  }

  protected async saveLanguage(): Promise<void> {
    const language = this.language();

    if (!isSupportedLanguage(language)) {
      return;
    }

    this.savingLanguage.set(true);
    this.languageError.set(null);
    this.languageSaved.set(false);

    try {
      const updated = await this.users.updateLanguage(language);
      this.currentLanguage.set(isSupportedLanguage(updated.language) ? updated.language : 'en');
      this.languageSaved.set(true);
    } catch (error) {
      this.languageError.set(
        error instanceof HttpErrorResponse && typeof error.error === 'string' ? error.error : 'profile.language.error'
      );
    } finally {
      this.savingLanguage.set(false);
    }
  }

  private async loadProfile(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);

    try {
      this.applyCurrentUser(await this.users.ensureCurrentUser());
    } catch {
      this.loadError.set('profile.loadError');
    } finally {
      this.loading.set(false);
    }
  }

  private applyCurrentUser(user: CurrentUser): void {
    this.givenName.set(user.name.givenName);
    this.familyName.set(user.name.familyName);
    this.currentGivenName.set(user.name.givenName);
    this.currentFamilyName.set(user.name.familyName);
    this.email.set(user.email.value);
    this.currentEmail.set(user.email.value);
    this.timeZoneId.set(user.timeZoneId);
    this.currentTimeZoneId.set(user.timeZoneId);

    const language = isSupportedLanguage(user.language) ? user.language : 'en';
    this.language.set(language);
    this.currentLanguage.set(language);
  }
}
