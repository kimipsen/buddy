import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { Language } from './i18n/language';
import { TranslationService } from './i18n/translation.service';
import { PersonName } from './guardians.service';
import { postIdempotent } from './http-idempotency';
import { RuntimeConfigService } from './runtime-config.service';

export interface Email {
  value: string;
  isVerified: boolean;
}

export interface CurrentUser {
  id: string;
  email: Email;
  userName: string;
  name: PersonName;
  timeZoneId: string;
  language: string;
}

export interface PreviewPerson {
  id: string;
  givenName: string;
  familyName: string;
}

// GET /users/me/deletion-preview: children erased with the account (no other guardian), owned
// groups that pass to another member, and owned groups nobody else is left in.
export interface AccountDeletionPreview {
  childrenErased: PreviewPerson[];
  groupsHandedOver: { id: string; name: string; newOwner: PreviewPerson }[];
  groupsDeleted: { id: string; name: string }[];
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
   * Every other endpoint rejects a caller without one (403 user_not_provisioned), so the route
   * guards call this before any page loads. Memoized so repeated calls (e.g. a guard firing on
   * every navigation) only hit the network once -- but a failure is not memoized, so the next
   * navigation retries instead of leaving the session unprovisioned until a reload.
   */
  ensureCurrentUser(): Promise<CurrentUser> {
    this.currentUserPromise ??= firstValueFrom(
      this.http.get<CurrentUser>(`${this.runtimeConfig.apiBaseUrl}/users/me`),
    ).then(
      (user) => {
        this.timeZoneState.set(user.timeZoneId);
        this.i18n.setLanguageFromServer(user.language);
        return user;
      },
      (error: unknown) => {
        this.currentUserPromise = null;
        throw error;
      },
    );
    return this.currentUserPromise;
  }

  async updateName(givenName: string, familyName: string): Promise<CurrentUser> {
    const updated = await firstValueFrom(
      this.http.patch<CurrentUser>(`${this.runtimeConfig.apiBaseUrl}/users/me/name`, {
        givenName,
        familyName,
      }),
    );
    this.currentUserPromise = Promise.resolve(updated);
    return updated;
  }

  async updateEmail(email: string): Promise<CurrentUser> {
    const updated = await firstValueFrom(
      this.http.patch<CurrentUser>(`${this.runtimeConfig.apiBaseUrl}/users/me/email`, { email }),
    );
    this.currentUserPromise = Promise.resolve(updated);
    return updated;
  }

  async verifyEmail(token: string): Promise<CurrentUser> {
    const updated = await firstValueFrom(
      postIdempotent<CurrentUser>(
        this.http,
        `${this.runtimeConfig.apiBaseUrl}/users/me/email/verify`,
        { token },
      ),
    );
    this.currentUserPromise = Promise.resolve(updated);
    return updated;
  }

  async updateTimeZone(timeZoneId: string): Promise<CurrentUser> {
    const updated = await firstValueFrom(
      this.http.patch<CurrentUser>(`${this.runtimeConfig.apiBaseUrl}/users/me/timezone`, {
        timeZoneId,
      }),
    );
    this.currentUserPromise = Promise.resolve(updated);
    this.timeZoneState.set(updated.timeZoneId);
    return updated;
  }

  async updateLanguage(language: Language): Promise<CurrentUser> {
    const updated = await firstValueFrom(
      this.http.patch<CurrentUser>(`${this.runtimeConfig.apiBaseUrl}/users/me/language`, {
        language,
      }),
    );
    this.currentUserPromise = Promise.resolve(updated);
    this.i18n.setLanguageFromServer(updated.language);
    return updated;
  }

  // What deleting the account would also take with it, for the confirmation dialog.
  getAccountDeletionPreview(): Promise<AccountDeletionPreview> {
    return firstValueFrom(
      this.http.get<AccountDeletionPreview>(
        `${this.runtimeConfig.apiBaseUrl}/users/me/deletion-preview`,
      ),
    );
  }

  async deleteCurrentUser(): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/users/me`));
    this.currentUserPromise = null;
  }
}
