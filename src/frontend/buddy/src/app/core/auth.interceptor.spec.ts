import { HttpHandlerFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';

import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';
import { RuntimeConfigService } from './runtime-config.service';

const API_BASE_URL = 'https://api.buddy.test/api';

describe('authInterceptor', () => {
  interface Stubs {
    auth?: Partial<AuthService>;
    apiBaseUrl?: string;
  }

  function setup(stubs: Stubs = {}) {
    const authStub: Partial<AuthService> = {
      getAccessToken: vi.fn(async () => 'access-token-1'),
      ...stubs.auth
    };
    const runtimeConfigStub: Partial<RuntimeConfigService> = {
      apiBaseUrl: stubs.apiBaseUrl ?? API_BASE_URL
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authStub },
        { provide: RuntimeConfigService, useValue: runtimeConfigStub }
      ]
    });

    return { authStub };
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
    setup({ auth: { getAccessToken: vi.fn(async () => null) } });
    const req = new HttpRequest('GET', `${API_BASE_URL}/users/me`);
    const next = vi.fn<HttpHandlerFn>(() => of(passThroughResponse));

    await run(req, next);

    expect(next).toHaveBeenCalledWith(req);
    const forwarded = next.mock.calls[0][0];
    expect(forwarded.headers.has('Authorization')).toBe(false);
  });

  it('treats a URL that merely contains, but does not start with, the API base URL as non-API', async () => {
    const { authStub } = setup();
    const req = new HttpRequest('GET', `https://other.example.com/proxy?target=${API_BASE_URL}/users/me`);
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
