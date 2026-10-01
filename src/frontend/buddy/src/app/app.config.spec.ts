import { HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { appConfig } from './app.config';
import { routes } from './app.routes';
import { AuthService } from './core/auth.service';
import { RuntimeConfigService } from './core/runtime-config.service';

const API_BASE_URL = 'https://api.buddy.test/api';

describe('appConfig', () => {
  let load: ReturnType<typeof vi.fn<() => Promise<void>>>;

  async function setup(): Promise<void> {
    load = vi.fn(async () => {});

    TestBed.configureTestingModule({
      providers: [
        ...appConfig.providers,
        provideHttpClientTesting(),
        { provide: RuntimeConfigService, useValue: { load, apiBaseUrl: API_BASE_URL } },
        { provide: AuthService, useValue: { getAccessToken: async () => 'access-token-1' } },
      ],
    });

    await TestBed.inject(ApplicationInitStatus).donePromise;
  }

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('loads the runtime config during app initialization', async () => {
    await setup();

    expect(load).toHaveBeenCalledTimes(1);
  });

  it('registers the app routes with the router', async () => {
    await setup();

    expect(TestBed.inject(Router).config).toEqual(routes);
  });

  it('runs API requests through authInterceptor', async () => {
    await setup();

    TestBed.inject(HttpClient).get(`${API_BASE_URL}/things`).subscribe();
    // authInterceptor awaits the token before forwarding the request.
    await Promise.resolve();
    await Promise.resolve();

    const req = TestBed.inject(HttpTestingController).expectOne(`${API_BASE_URL}/things`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-token-1');
    req.flush({});
  });
});
