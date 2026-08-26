import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { TranslationService } from './i18n/translation.service';
import { RuntimeConfigService } from './runtime-config.service';
import { CurrentUser, UsersService } from './users.service';

describe('UsersService', () => {
  const apiBaseUrl = 'https://api.buddy.test';

  let service: UsersService;
  let httpMock: HttpTestingController;
  let i18n: { setLanguageFromServer: ReturnType<typeof vi.fn> };

  function currentUser(overrides: Partial<CurrentUser> = {}): CurrentUser {
    return {
      id: 'user-1',
      email: { value: 'user@buddy.test', isVerified: true },
      userName: 'user',
      name: { givenName: 'Uma', familyName: 'User' },
      timeZoneId: 'UTC',
      language: 'en',
      ...overrides
    };
  }

  beforeEach(() => {
    i18n = { setLanguageFromServer: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RuntimeConfigService, useValue: { apiBaseUrl } as Partial<RuntimeConfigService> },
        { provide: TranslationService, useValue: i18n as Partial<TranslationService> }
      ]
    });

    service = TestBed.inject(UsersService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('ensureCurrentUser', () => {
    it('fetches the current user, sets the time zone signal, and forwards the language to i18n', async () => {
      const promise = service.ensureCurrentUser();

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me`);
      expect(req.request.method).toBe('GET');
      req.flush(currentUser({ timeZoneId: 'Europe/Copenhagen', language: 'da' }));

      const resolved = await promise;

      expect(resolved.timeZoneId).toBe('Europe/Copenhagen');
      expect(service.timeZoneId()).toBe('Europe/Copenhagen');
      expect(i18n.setLanguageFromServer).toHaveBeenCalledWith('da');
    });

    it('defaults the time zone signal to UTC before the current user resolves', () => {
      expect(service.timeZoneId()).toBe('UTC');
    });

    it('memoizes the request: a second call before or after resolution issues no additional HTTP request', async () => {
      const first = service.ensureCurrentUser();
      const second = service.ensureCurrentUser();

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me`);
      req.flush(currentUser());

      await first;
      await second;

      const third = service.ensureCurrentUser();
      httpMock.expectNone(`${apiBaseUrl}/users/me`);

      await expect(third).resolves.toEqual(currentUser());
    });

    it('propagates a failed request and keeps it memoized rather than retrying', async () => {
      const first = service.ensureCurrentUser();

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me`);
      req.flush('boom', { status: 500, statusText: 'Server Error' });

      await expect(first).rejects.toBeInstanceOf(HttpErrorResponse);

      // A follow-up call reuses the same rejected promise instead of firing a new request.
      const second = service.ensureCurrentUser();
      httpMock.expectNone(`${apiBaseUrl}/users/me`);
      await expect(second).rejects.toBeInstanceOf(HttpErrorResponse);

      expect(service.timeZoneId()).toBe('UTC');
      expect(i18n.setLanguageFromServer).not.toHaveBeenCalled();
    });
  });

  describe('updateName', () => {
    it('PATCHes the new name and re-memoizes the current user', async () => {
      const updated = currentUser({ name: { givenName: 'New', familyName: 'Name' } });
      const promise = service.updateName('New', 'Name');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/name`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual({ givenName: 'New', familyName: 'Name' });
      req.flush(updated);

      await expect(promise).resolves.toEqual(updated);

      // The updated user is now memoized, so ensureCurrentUser resolves it without a new request.
      await expect(service.ensureCurrentUser()).resolves.toEqual(updated);
      httpMock.expectNone(`${apiBaseUrl}/users/me`);
    });

    it('propagates an error response without mutating memoized state', async () => {
      const promise = service.updateName('New', 'Name');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/name`);
      req.flush('bad request', { status: 400, statusText: 'Bad Request' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('updateEmail', () => {
    it('PATCHes the new email and re-memoizes the current user', async () => {
      const updated = currentUser({ email: { value: 'new@buddy.test', isVerified: false } });
      const promise = service.updateEmail('new@buddy.test');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/email`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual({ email: 'new@buddy.test' });
      req.flush(updated);

      await expect(promise).resolves.toEqual(updated);

      await expect(service.ensureCurrentUser()).resolves.toEqual(updated);
      httpMock.expectNone(`${apiBaseUrl}/users/me`);
    });

    it('propagates an error response', async () => {
      const promise = service.updateEmail('taken@buddy.test');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/email`);
      req.flush('conflict', { status: 409, statusText: 'Conflict' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('verifyEmail', () => {
    it('POSTs the verification token and re-memoizes the current user', async () => {
      const updated = currentUser({ email: { value: 'user@buddy.test', isVerified: true } });
      const promise = service.verifyEmail('token-123');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/email/verify`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ token: 'token-123' });
      req.flush(updated);

      await expect(promise).resolves.toEqual(updated);

      await expect(service.ensureCurrentUser()).resolves.toEqual(updated);
      httpMock.expectNone(`${apiBaseUrl}/users/me`);
    });

    it('propagates an error response for an invalid token', async () => {
      const promise = service.verifyEmail('bad-token');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/email/verify`);
      req.flush('invalid token', { status: 400, statusText: 'Bad Request' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('updateTimeZone', () => {
    it('PATCHes the new time zone, re-memoizes the current user, and updates the signal', async () => {
      const updated = currentUser({ timeZoneId: 'America/New_York' });
      const promise = service.updateTimeZone('America/New_York');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/timezone`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual({ timeZoneId: 'America/New_York' });
      req.flush(updated);

      await expect(promise).resolves.toEqual(updated);

      expect(service.timeZoneId()).toBe('America/New_York');

      await expect(service.ensureCurrentUser()).resolves.toEqual(updated);
      httpMock.expectNone(`${apiBaseUrl}/users/me`);
    });

    it('leaves the time zone signal untouched when the request fails', async () => {
      const promise = service.updateTimeZone('Invalid/Zone');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/timezone`);
      req.flush('bad request', { status: 400, statusText: 'Bad Request' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
      expect(service.timeZoneId()).toBe('UTC');
    });
  });

  describe('updateLanguage', () => {
    it('PATCHes the new language, re-memoizes the current user, and forwards it to i18n', async () => {
      const updated = currentUser({ language: 'da' });
      const promise = service.updateLanguage('da');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/language`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual({ language: 'da' });
      req.flush(updated);

      await expect(promise).resolves.toEqual(updated);

      expect(i18n.setLanguageFromServer).toHaveBeenCalledWith('da');

      await expect(service.ensureCurrentUser()).resolves.toEqual(updated);
      httpMock.expectNone(`${apiBaseUrl}/users/me`);
    });

    it('does not forward the language to i18n when the request fails', async () => {
      const promise = service.updateLanguage('da');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/language`);
      req.flush('bad request', { status: 400, statusText: 'Bad Request' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
      expect(i18n.setLanguageFromServer).not.toHaveBeenCalled();
    });
  });

  describe('deleteCurrentUser', () => {
    it('DELETEs the current user and clears the memoized promise so the next call re-fetches', async () => {
      const initial = service.ensureCurrentUser();
      httpMock.expectOne(`${apiBaseUrl}/users/me`).flush(currentUser());
      await initial;

      const deletion = service.deleteCurrentUser();
      const deleteReq = httpMock.expectOne(`${apiBaseUrl}/users/me`);
      expect(deleteReq.request.method).toBe('DELETE');
      deleteReq.flush(null);
      await deletion;

      // Memoization was reset by the delete, so ensureCurrentUser fires a fresh request.
      const refetch = service.ensureCurrentUser();
      const refetchReq = httpMock.expectOne(`${apiBaseUrl}/users/me`);
      refetchReq.flush(currentUser({ id: 'user-2' }));

      await expect(refetch).resolves.toEqual(currentUser({ id: 'user-2' }));
    });

    it('propagates an error response and leaves the promise as-is on failure', async () => {
      const promise = service.deleteCurrentUser();

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me`);
      expect(req.request.method).toBe('DELETE');
      req.flush('server error', { status: 500, statusText: 'Server Error' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });
});
