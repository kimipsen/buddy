import { HttpErrorResponse, HttpHandlerFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { firstValueFrom, of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';
import { takePendingReturnUrl } from './pending-return-url';
import { RuntimeConfigService } from './runtime-config.service';

const API_BASE_URL = 'https://api.buddy.test/api';

describe('authInterceptor', () => {
  interface Stubs {
    auth?: Partial<AuthService>;
    apiBaseUrl?: string;
    currentUrl?: string;
  }

  beforeEach(() => {
    sessionStorage.clear();
  });

  function setup(stubs: Stubs = {}) {
    const authStub: Partial<AuthService> = {
      getAccessToken: vi.fn(async () => 'access-token-1'),
      sessionExpired: signal(false).asReadonly(),
      expireSession: vi.fn(),
      ...stubs.auth,
    };
    const runtimeConfigStub: Partial<RuntimeConfigService> = {
      apiBaseUrl: stubs.apiBaseUrl ?? API_BASE_URL,
    };

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authStub },
        { provide: RuntimeConfigService, useValue: runtimeConfigStub },
      ],
    });

    const router = TestBed.inject(Router);
    vi.spyOn(router, 'url', 'get').mockReturnValue(stubs.currentUrl ?? '/guardian/calendar');
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    return { authStub, navigate };
  }

  const passThroughResponse = new HttpResponse({ status: 200 });

  function run(req: HttpRequest<unknown>, next: HttpHandlerFn) {
    return TestBed.runInInjectionContext(() => firstValueFrom(authInterceptor(req, next)));
  }

  it('forwards a request for a non-API URL unchanged, without checking for a token', async () => {
    const { authStub } = setup();
    const req = new HttpRequest('GET', 'https://other.example.com/resource');
    const next = vi.fn<HttpHandlerFn>(() => of(passThroughResponse));

    const result = await run(req, next);

    expect(next).toHaveBeenCalledWith(req);
    expect(authStub.getAccessToken).not.toHaveBeenCalled();
    expect(result).toBe(passThroughResponse);
  });

  it('attaches a bearer token to a request for the configured API base URL', async () => {
    setup({ auth: { getAccessToken: vi.fn(async () => 'my-access-token') } });
    const req = new HttpRequest('GET', `${API_BASE_URL}/users/me`);
    const next = vi.fn<HttpHandlerFn>(() => of(passThroughResponse));

    await run(req, next);

    expect(next).toHaveBeenCalledTimes(1);
    const forwarded = next.mock.calls[0][0];
    expect(forwarded).not.toBe(req);
    expect(forwarded.headers.get('Authorization')).toBe('Bearer my-access-token');
    // The original request object passed in is left untouched -- HttpRequest.clone returns a new instance.
    expect(req.headers.get('Authorization')).toBeNull();
  });

  it('forwards an API request unmodified when there is no access token', async () => {
    const { navigate } = setup({ auth: { getAccessToken: vi.fn(async () => null) } });
    const req = new HttpRequest('GET', `${API_BASE_URL}/users/me`);
    const next = vi.fn<HttpHandlerFn>(() => of(passThroughResponse));

    await run(req, next);

    expect(next).toHaveBeenCalledWith(req);
    const forwarded = next.mock.calls[0][0];
    expect(forwarded.headers.has('Authorization')).toBe(false);
    // An anonymous page (share link, invite preview) never had a session, so it isn't sent to log in.
    expect(navigate).not.toHaveBeenCalled();
  });

  describe('when the session has expired', () => {
    const expiredAuth = (): Partial<AuthService> => ({
      getAccessToken: vi.fn(async () => null),
      sessionExpired: signal(true).asReadonly(),
    });

    it('does not send the request, redirects to /login with the reason and errors with 401', async () => {
      const { navigate } = setup({ auth: expiredAuth() });
      const req = new HttpRequest('GET', `${API_BASE_URL}/calendars?from=1`);
      const next = vi.fn<HttpHandlerFn>(() => of(passThroughResponse));

      const error = await run(req, next).catch((e: unknown) => e);

      expect(next).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledExactlyOnceWith(['/login'], {
        queryParams: { reason: 'session-expired' },
      });
      expect(error).toBeInstanceOf(HttpErrorResponse);
      expect((error as HttpErrorResponse).status).toBe(401);
      expect((error as HttpErrorResponse).url).toBe(`${API_BASE_URL}/calendars?from=1`);
    });

    it('remembers the current page so login can return to it', async () => {
      setup({ auth: expiredAuth(), currentUrl: '/guardian/mealplan?week=2026-W40' });
      const req = new HttpRequest('GET', `${API_BASE_URL}/users/me`);

      await run(req, vi.fn<HttpHandlerFn>()).catch(() => undefined);

      expect(takePendingReturnUrl()).toBe('/guardian/mealplan?week=2026-W40');
    });

    it('does not navigate again or overwrite the return URL once already on /login', async () => {
      const { navigate } = setup({
        auth: expiredAuth(),
        currentUrl: '/login?reason=session-expired',
      });
      const req = new HttpRequest('GET', `${API_BASE_URL}/users/me`);

      const error = await run(req, vi.fn<HttpHandlerFn>()).catch((e: unknown) => e);

      expect(navigate).not.toHaveBeenCalled();
      expect(takePendingReturnUrl()).toBeNull();
      expect((error as HttpErrorResponse).status).toBe(401);
    });
  });

  describe('when the API rejects the token', () => {
    it('expires the session, redirects to /login and rethrows as 401', async () => {
      const { authStub, navigate } = setup();
      const req = new HttpRequest('GET', `${API_BASE_URL}/users/me`);
      const next = vi.fn<HttpHandlerFn>(() =>
        throwError(() => new HttpErrorResponse({ status: 401 })),
      );

      const error = await run(req, next).catch((e: unknown) => e);

      expect(authStub.expireSession).toHaveBeenCalledOnce();
      expect(navigate).toHaveBeenCalledExactlyOnceWith(['/login'], {
        queryParams: { reason: 'session-expired' },
      });
      expect((error as HttpErrorResponse).status).toBe(401);
    });

    it('passes any other error through untouched, without ending the session', async () => {
      const { authStub, navigate } = setup();
      const req = new HttpRequest('GET', `${API_BASE_URL}/users/me`);
      const serverError = new HttpErrorResponse({ status: 500 });
      const next = vi.fn<HttpHandlerFn>(() => throwError(() => serverError));

      const error = await run(req, next).catch((e: unknown) => e);

      expect(error).toBe(serverError);
      expect(authStub.expireSession).not.toHaveBeenCalled();
      expect(navigate).not.toHaveBeenCalled();
    });

    it('passes a 403 through untouched (a permission answer, not an ended session)', async () => {
      const { authStub, navigate } = setup();
      const req = new HttpRequest('GET', `${API_BASE_URL}/users/me`);
      const forbidden = new HttpErrorResponse({ status: 403 });

      const error = await run(
        req,
        vi.fn<HttpHandlerFn>(() => throwError(() => forbidden)),
      ).catch((e: unknown) => e);

      expect(error).toBe(forbidden);
      expect(authStub.expireSession).not.toHaveBeenCalled();
      expect(navigate).not.toHaveBeenCalled();
    });
  });

  it('treats a URL that merely contains, but does not start with, the API base URL as non-API', async () => {
    const { authStub } = setup();
    const req = new HttpRequest(
      'GET',
      `https://other.example.com/proxy?target=${API_BASE_URL}/users/me`,
    );
    const next = vi.fn<HttpHandlerFn>(() => of(passThroughResponse));

    await run(req, next);

    expect(authStub.getAccessToken).not.toHaveBeenCalled();
    expect(next).toHaveBeenCalledWith(req);
  });

  it('propagates the response emitted by the next handler', async () => {
    setup();
    const req = new HttpRequest('GET', `${API_BASE_URL}/users/me`);
    const response = new HttpResponse({ status: 200, body: { ok: true } });
    const next = vi.fn<HttpHandlerFn>(() => of(response));

    const result = await run(req, next);

    expect(result).toBe(response);
  });
});
