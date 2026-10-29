import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { ScreenWakeLockService } from './screen-wake-lock.service';

describe('ScreenWakeLockService', () => {
  interface FakeSentinel {
    released: boolean;
    release: ReturnType<typeof vi.fn>;
  }

  function sentinel(): FakeSentinel {
    const fake: FakeSentinel = {
      released: false,
      release: vi.fn(async () => {
        fake.released = true;
      }),
    };
    return fake;
  }

  function stubWakeLock(request: () => Promise<unknown>): ReturnType<typeof vi.fn> {
    const spy = vi.fn(request);
    Object.defineProperty(navigator, 'wakeLock', { configurable: true, value: { request: spy } });
    return spy;
  }

  function setVisibility(state: DocumentVisibilityState): void {
    Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => state });
    document.dispatchEvent(new Event('visibilitychange'));
  }

  const flush = () => new Promise((resolve) => setTimeout(resolve, 0));

  afterEach(() => {
    Reflect.deleteProperty(navigator, 'wakeLock');
    Reflect.deleteProperty(document, 'visibilityState');
  });

  it('requests one screen lock for two holders and releases it after the last one', async () => {
    const lock = sentinel();
    const request = stubWakeLock(async () => lock);
    const service = TestBed.inject(ScreenWakeLockService);

    const releaseFirst = service.hold();
    const releaseSecond = service.hold();
    await flush();

    expect(request).toHaveBeenCalledTimes(1);
    expect(request).toHaveBeenCalledWith('screen');

    releaseFirst();
    await flush();
    expect(lock.release).not.toHaveBeenCalled();

    releaseSecond();
    await flush();
    expect(lock.release).toHaveBeenCalledTimes(1);
  });

  it('counts a holder once even when its release is called twice', async () => {
    const lock = sentinel();
    stubWakeLock(async () => lock);
    const service = TestBed.inject(ScreenWakeLockService);

    const releaseFirst = service.hold();
    service.hold();
    await flush();
    releaseFirst();
    releaseFirst();
    await flush();

    expect(lock.release).not.toHaveBeenCalled();
  });

  it('releases a lock that arrives after everyone let go', async () => {
    const lock = sentinel();
    let grant: (value: FakeSentinel) => void = () => undefined;
    stubWakeLock(() => new Promise((resolve) => (grant = resolve)));
    const service = TestBed.inject(ScreenWakeLockService);

    service.hold()();
    grant(lock);
    await flush();

    expect(lock.release).toHaveBeenCalledTimes(1);
  });

  it('requests again after the page comes back if the browser dropped the lock', async () => {
    const first = sentinel();
    const second = sentinel();
    const request = stubWakeLock(
      vi.fn().mockResolvedValueOnce(first).mockResolvedValueOnce(second),
    );
    const service = TestBed.inject(ScreenWakeLockService);

    service.hold();
    await flush();
    first.released = true;
    setVisibility('hidden');
    await flush();
    expect(request).toHaveBeenCalledTimes(1);

    setVisibility('visible');
    await flush();
    expect(request).toHaveBeenCalledTimes(2);
  });

  it('does not request again on visibility while the lock is still held, or with no holders', async () => {
    const request = stubWakeLock(async () => sentinel());
    const service = TestBed.inject(ScreenWakeLockService);

    setVisibility('visible');
    await flush();
    expect(request).not.toHaveBeenCalled();

    service.hold();
    await flush();
    setVisibility('visible');
    await flush();
    expect(request).toHaveBeenCalledTimes(1);
  });

  it('does nothing without the Wake Lock API', async () => {
    const service = TestBed.inject(ScreenWakeLockService);

    expect(() => service.hold()()).not.toThrow();
    await flush();
  });

  it('swallows a refused request', async () => {
    const request = stubWakeLock(async () => {
      throw new DOMException('Low power', 'NotAllowedError');
    });
    const service = TestBed.inject(ScreenWakeLockService);

    const release = service.hold();
    await flush();

    expect(request).toHaveBeenCalledTimes(1);
    expect(() => release()).not.toThrow();
  });

  it('swallows a failing release', async () => {
    const lock = sentinel();
    lock.release.mockRejectedValue(new Error('gone'));
    stubWakeLock(async () => lock);
    const service = TestBed.inject(ScreenWakeLockService);

    const release = service.hold();
    await flush();
    release();
    await flush();

    expect(lock.release).toHaveBeenCalledTimes(1);
  });
});
