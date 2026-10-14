import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { pollWhileOpen } from './visible-poll';

describe('pollWhileOpen', () => {
  let visibility: DocumentVisibilityState;

  beforeEach(() => {
    vi.useFakeTimers();
    visibility = 'visible';
    vi.spyOn(document, 'visibilityState', 'get').mockImplementation(() => visibility);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('refreshes on every interval', () => {
    const refresh = vi.fn();
    const stop = pollWhileOpen(refresh, 60_000);

    vi.advanceTimersByTime(59_999);
    expect(refresh).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(refresh).toHaveBeenCalledTimes(1);
    vi.advanceTimersByTime(60_000);
    expect(refresh).toHaveBeenCalledTimes(2);

    stop();
  });

  it('refreshes when the page becomes visible, not when it is hidden', () => {
    const refresh = vi.fn();
    const stop = pollWhileOpen(refresh, 60_000);

    visibility = 'hidden';
    document.dispatchEvent(new Event('visibilitychange'));
    expect(refresh).not.toHaveBeenCalled();

    visibility = 'visible';
    document.dispatchEvent(new Event('visibilitychange'));
    expect(refresh).toHaveBeenCalledTimes(1);

    stop();
  });

  it('stops both the interval and the listener', () => {
    const refresh = vi.fn();
    pollWhileOpen(refresh, 60_000)();

    vi.advanceTimersByTime(120_000);
    document.dispatchEvent(new Event('visibilitychange'));

    expect(refresh).not.toHaveBeenCalled();
  });
});
