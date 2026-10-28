import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { isRuntimeConfig, RuntimeConfig, RuntimeConfigService } from './runtime-config.service';

describe('RuntimeConfigService', () => {
  let service: RuntimeConfigService;
  let fetchMock: ReturnType<typeof vi.fn>;

  const config: RuntimeConfig = {
    keycloak: {
      authority: 'https://auth.buddy.test',
      clientId: 'buddy-web',
      realm: 'buddy',
      redirectPath: '/auth/callback',
    },
    apiBaseUrl: 'https://api.buddy.test',
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
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => config,
    });

    await service.load();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledWith('/config/runtime-config.json', { cache: 'no-cache' });
  });

  it('rejects a config missing a required field instead of loading it', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => ({ ...config, keycloak: { ...config.keycloak, realm: undefined } }),
    });

    await expect(service.load()).rejects.toThrow('Runtime config is missing');
    expect(() => service.apiBaseUrl).toThrow('Runtime config has not been loaded.');
  });

  it.each([
    ['apiBaseUrl', { ...config, apiBaseUrl: '' }],
    ['keycloak.authority', { ...config, keycloak: { ...config.keycloak, authority: '' } }],
    ['keycloak.realm', { ...config, keycloak: { ...config.keycloak, realm: '' } }],
  ])(
    'rejects a config whose %s is empty, as the image serves it when a variable is unset',
    async (_, body) => {
      fetchMock.mockResolvedValue({
        ok: true,
        status: 200,
        statusText: 'OK',
        json: async () => body,
      });

      await expect(service.load()).rejects.toThrow('Runtime config is missing');
    },
  );

  it('treats an empty version as none and an empty repository URL as the default', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => ({ ...config, version: '', repositoryUrl: '' }),
    });

    await service.load();

    expect(service.version).toBeNull();
    expect(service.repositoryUrl).toBe('https://github.com/kimipsen/buddy');
  });

  it('exposes apiBaseUrl and keycloak from the fetched config once load() resolves', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => config,
    });

    await service.load();

    expect(service.apiBaseUrl).toBe('https://api.buddy.test');
    expect(service.keycloak).toEqual(config.keycloak);
  });

  it('exposes the build version from the config, and null when the config has none', async () => {
    expect(service.version).toBeNull();

    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => config,
    });
    await service.load();
    expect(service.version).toBeNull();

    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => ({ ...config, version: '1.2.0' }),
    });
    await service.load();
    expect(service.version).toBe('1.2.0');
  });

  it('exposes the repository URL from the config, and the kimipsen/buddy default without one', async () => {
    expect(service.repositoryUrl).toBe('https://github.com/kimipsen/buddy');

    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => config,
    });
    await service.load();
    expect(service.repositoryUrl).toBe('https://github.com/kimipsen/buddy');

    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => ({ ...config, repositoryUrl: 'https://github.com/someone/buddy' }),
    });
    await service.load();
    expect(service.repositoryUrl).toBe('https://github.com/someone/buddy');
  });

  it('rejects a config whose repositoryUrl is not a string', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => ({ ...config, repositoryUrl: 42 }),
    });

    await expect(service.load()).rejects.toThrow('Runtime config is missing');
  });

  it('rejects a config whose version is not a string', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => ({ ...config, version: 120 }),
    });

    await expect(service.load()).rejects.toThrow('Runtime config is missing');
  });

  it('throws with the response status and status text when the fetch response is not ok', async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      status: 404,
      statusText: 'Not Found',
      json: async () => ({}),
    });

    await expect(service.load()).rejects.toThrow('Unable to load runtime config: 404 Not Found');
  });

  it('leaves the config unset (still throwing on access) after a failed load', async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      status: 500,
      statusText: 'Internal Server Error',
      json: async () => ({}),
    });

    await expect(service.load()).rejects.toThrow();

    expect(() => service.apiBaseUrl).toThrow('Runtime config has not been loaded.');
  });

  it('propagates a rejected fetch (e.g. network failure) without swallowing it', async () => {
    fetchMock.mockRejectedValue(new Error('network down'));

    await expect(service.load()).rejects.toThrow('network down');
  });

  it.each([
    ['null', null],
    ['a JSON string', 'https://api.buddy.test'],
  ])('rejects a config body that is %s instead of an object', async (_, body) => {
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => body,
    });

    await expect(service.load()).rejects.toThrow('Runtime config is missing');
  });

  it('rejects a config whose keycloak is null', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => ({ ...config, keycloak: null }),
    });

    await expect(service.load()).rejects.toThrow('Runtime config is missing');
  });
});

describe('isRuntimeConfig', () => {
  const keycloak = {
    authority: 'https://auth.buddy.test',
    clientId: 'buddy-web',
    realm: 'buddy',
    redirectPath: '/auth/callback',
  };

  it('accepts a plain object with apiBaseUrl and every keycloak field', () => {
    expect(isRuntimeConfig({ keycloak, apiBaseUrl: 'https://api.buddy.test' })).toBe(true);
  });

  it('rejects null and non-object values, even a function carrying the right fields', () => {
    expect(isRuntimeConfig(null)).toBe(false);
    expect(isRuntimeConfig(undefined)).toBe(false);
    expect(
      isRuntimeConfig(Object.assign(() => undefined, { keycloak, apiBaseUrl: 'https://api' })),
    ).toBe(false);
  });

  it('rejects a keycloak section that is null or not an object, even a function carrying the right fields', () => {
    expect(isRuntimeConfig({ keycloak: null, apiBaseUrl: 'https://api' })).toBe(false);
    expect(
      isRuntimeConfig({
        keycloak: Object.assign(() => undefined, keycloak),
        apiBaseUrl: 'https://api',
      }),
    ).toBe(false);
  });
});
