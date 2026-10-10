import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  FEATURES_LOAD_TIMEOUT_MS,
  FeatureName,
  FeaturesService,
  InstallationFeatures,
} from './features.service';
import { RuntimeConfigService } from './runtime-config.service';

describe('FeaturesService', () => {
  let service: FeaturesService;
  let fetchMock: ReturnType<typeof vi.fn>;

  const allOn: InstallationFeatures = {
    mealplans: true,
    mealplanAiAssistant: true,
    mealplanImport: true,
    medicines: true,
    sleepDiary: true,
    houseRules: true,
    pickups: true,
    babysitters: true,
    workLocations: true,
    printing: true,
    progress: true,
    taskLibrary: true,
    help: true,
  };

  const names = Object.keys(allOn) as FeatureName[];

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        { provide: RuntimeConfigService, useValue: { apiBaseUrl: 'https://api.buddy.test' } },
      ],
    });
    service = TestBed.inject(FeaturesService);
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('offers every feature before the flags have loaded', () => {
    expect(names.every((name) => service.enabled(name))).toBe(true);
  });

  it('loads the flags anonymously from the API, without the auth interceptor', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ ...allOn, medicines: false, mealplanAiAssistant: false }),
    });

    await service.load();

    expect(fetchMock).toHaveBeenCalledWith('https://api.buddy.test/features', {
      signal: expect.any(AbortSignal),
    });
    expect(service.enabled('medicines')).toBe(false);
    expect(service.enabled('mealplanAiAssistant')).toBe(false);
    expect(service.enabled('mealplans')).toBe(true);
  });

  it('treats a flag the API does not report as on', async () => {
    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ medicines: false }) });

    await service.load();

    expect(service.enabled('medicines')).toBe(false);
    expect(service.enabled('pickups')).toBe(true);
  });

  it('keeps every feature on and warns when the API answers with an error', async () => {
    fetchMock.mockResolvedValue({ ok: false, status: 503, statusText: 'Service Unavailable' });

    await service.load();

    expect(names.every((name) => service.enabled(name))).toBe(true);
    expect(console.warn).toHaveBeenCalledTimes(1);
  });

  it('keeps every feature on when the API cannot be reached', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));

    await expect(service.load()).resolves.toBeUndefined();

    expect(names.every((name) => service.enabled(name))).toBe(true);
  });

  it('gives up on a stalled API after the timeout and keeps every feature on', async () => {
    vi.useFakeTimers();
    try {
      fetchMock.mockImplementation(
        (_url: string, init: RequestInit) =>
          new Promise((_resolve, reject) =>
            init.signal!.addEventListener('abort', () =>
              reject(new DOMException('', 'AbortError')),
            ),
          ),
      );

      const load = service.load();
      await vi.advanceTimersByTimeAsync(FEATURES_LOAD_TIMEOUT_MS - 1);
      expect((fetchMock.mock.calls[0][1] as RequestInit).signal!.aborted).toBe(false);

      await vi.advanceTimersByTimeAsync(1);
      await expect(load).resolves.toBeUndefined();

      expect(names.every((name) => service.enabled(name))).toBe(true);
      expect(console.warn).toHaveBeenCalledTimes(1);
    } finally {
      vi.useRealTimers();
    }
  });

  it('reports whether any feature is turned off', async () => {
    expect(service.someOff()).toBe(false);

    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ ...allOn, sleepDiary: false }) });
    await service.load();

    expect(service.someOff()).toBe(true);
  });

  it('offers an untagged item, and a tagged one only while its feature is on', async () => {
    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ ...allOn, progress: false }) });
    await service.load();

    expect(service.offers({})).toBe(true);
    expect(service.offers({ feature: 'medicines' })).toBe(true);
    expect(service.offers({ feature: 'progress' })).toBe(false);
  });
});
