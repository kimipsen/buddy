import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { RuntimeConfig, RuntimeConfigService } from './runtime-config.service';

describe('RuntimeConfigService', () => {
  let service: RuntimeConfigService;
  let fetchMock: ReturnType<typeof vi.fn>;

  const config: RuntimeConfig = {
    keycloak: {
      authority: 'https://auth.buddy.test',
      clientId: 'buddy-web',
      realm: 'buddy',
      redirectPath: '/auth/callback'
    },
    apiBaseUrl: 'https://api.buddy.test'
  };

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(RuntimeConfigService);
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('throws when apiBaseUrl is read before load() resolves', () => {
    expect(() => service.apiBaseUrl).toThrow('Runtime config has not been loaded.');
  });

  it('throws when keycloak is read before load() resolves', () => {
    expect(() => service.keycloak).toThrow('Runtime config has not been loaded.');
  });

  it('fetches the runtime config from the well-known static path with no-cache semantics', async () => {
    fetchMock.mockResolvedValue({ ok: true, status: 200, statusText: 'OK', json: async () => config });

    await service.load();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledWith('/config/runtime-config.json', { cache: 'no-cache' });
  });

  it('exposes apiBaseUrl and keycloak from the fetched config once load() resolves', async () => {
    fetchMock.mockResolvedValue({ ok: true, status: 200, statusText: 'OK', json: async () => config });

    await service.load();

    expect(service.apiBaseUrl).toBe('https://api.buddy.test');
    expect(service.keycloak).toEqual(config.keycloak);
  });

  it('throws with the response status and status text when the fetch response is not ok', async () => {
    fetchMock.mockResolvedValue({ ok: false, status: 404, statusText: 'Not Found', json: async () => ({}) });

    await expect(service.load()).rejects.toThrow('Unable to load runtime config: 404 Not Found');
  });

  it('leaves the config unset (still throwing on access) after a failed load', async () => {
    fetchMock.mockResolvedValue({ ok: false, status: 500, statusText: 'Internal Server Error', json: async () => ({}) });

    await expect(service.load()).rejects.toThrow();

    expect(() => service.apiBaseUrl).toThrow('Runtime config has not been loaded.');
  });

  it('propagates a rejected fetch (e.g. network failure) without swallowing it', async () => {
    fetchMock.mockRejectedValue(new Error('network down'));

    await expect(service.load()).rejects.toThrow('network down');
  });
});
