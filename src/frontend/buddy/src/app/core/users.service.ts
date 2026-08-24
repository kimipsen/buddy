import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { Language } from './i18n/language';
import { TranslationService } from './i18n/translation.service';
import { PersonName } from './guardians.service';
import { RuntimeConfigService } from './runtime-config.service';

export interface Email {
  value: string;
  isVerified: boolean;
}

export interface CurrentUser {
  id: string;
  email: Email;
  userName: string | null;
  name: PersonName;
  timeZoneId: string;
  language: string;
}

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);
  private readonly i18n = inject(TranslationService);

  private currentUserPromise: Promise<CurrentUser> | null = null;

  // Defaults to UTC until the current user resolves (see ensureCurrentUser) -- read by the
  // UserDatePipe so every timestamp in the app renders in the signed-in user's own time zone.
  private readonly timeZoneState = signal('UTC');
  readonly timeZoneId = this.timeZoneState.asReadonly();

  /**
   * Resolves the authenticated Keycloak subject to a backend user, creating one on first login.
   * Every other endpoint tolerates an unprovisioned caller by degrading silently (e.g. empty
   * lists), but create actions need a real UserId to attribute ownership to -- this must be
   * called at least once per session before any create action, or those calls 401. Memoized so
   * repeated calls (e.g. from a route guard firing on every navigation) only hit the network once.
   */
  ensureCurrentUser(): Promise<CurrentUser> {
    this.currentUserPromise ??= firstValueFrom(this.http.get<CurrentUser>(`${this.runtimeConfig.apiBaseUrl}/users/me`)).then(
      (user) => {
        this.timeZoneState.set(user.timeZoneId);
        this.i18n.setLanguageFromServer(user.language);
        return user;
      }
    );
    return this.currentUserPromise;
  }

  async updateName(givenName: string, familyName: string): Promise<CurrentUser> {
    const updated = await firstValueFrom(
      this.http.patch<CurrentUser>(`${this.runtimeConfig.apiBaseUrl}/users/me/name`, { givenName, familyName })
    );
    this.currentUserPromise = Promise.resolve(updated);
    return updated;
  }

  async updateEmail(email: string): Promise<CurrentUser> {
    const updated = await firstValueFrom(
      this.http.patch<CurrentUser>(`${this.runtimeConfig.apiBaseUrl}/users/me/email`, { email })
    );
    this.currentUserPromise = Promise.resolve(updated);
    return updated;
  }

  async verifyEmail(token: string): Promise<CurrentUser> {
    const updated = await firstValueFrom(
      this.http.post<CurrentUser>(`${this.runtimeConfig.apiBaseUrl}/users/me/email/verify`, { token })
    );
    this.currentUserPromise = Promise.resolve(updated);
    return updated;
  }

  async updateTimeZone(timeZoneId: string): Promise<CurrentUser> {
    const updated = await firstValueFrom(
      this.http.patch<CurrentUser>(`${this.runtimeConfig.apiBaseUrl}/users/me/timezone`, { timeZoneId })
    );
    this.currentUserPromise = Promise.resolve(updated);
    this.timeZoneState.set(updated.timeZoneId);
    return updated;
  }

  async updateLanguage(language: Language): Promise<CurrentUser> {
    const updated = await firstValueFrom(
      this.http.patch<CurrentUser>(`${this.runtimeConfig.apiBaseUrl}/users/me/language`, { language })
    );
    this.currentUserPromise = Promise.resolve(updated);
    this.i18n.setLanguageFromServer(updated.language);
    return updated;
  }

  async deleteCurrentUser(): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/users/me`));
    this.currentUserPromise = null;
  }
}
