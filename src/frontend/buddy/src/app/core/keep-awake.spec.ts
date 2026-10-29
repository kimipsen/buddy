import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from './auth.service';
import { SESSION_KEEP_ALIVE_MS, WAKE_LOCK_GRACE_MS, keepAwakeWhile } from './keep-awake';
import { ScreenWakeLockService } from './screen-wake-lock.service';

const wanted = signal(false);

@Component({ selector: 'app-keep-awake-host', template: '' })
class Host {
  constructor() {
    keepAwakeWhile(wanted);
  }
}

describe('keepAwakeWhile', () => {
  let release: ReturnType<typeof vi.fn>;
  let hold: ReturnType<typeof vi.fn>;
  let getAccessToken: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.useFakeTimers();
    wanted.set(false);
    release = vi.fn();
    hold = vi.fn(() => release);
    getAccessToken = vi.fn(async () => 'token');

    TestBed.configureTestingModule({
      imports: [Host],
      providers: [
        { provide: ScreenWakeLockService, useValue: { hold } },
        { provide: AuthService, useValue: { getAccessToken } },
      ],
    });
  });

  afterEach(() => vi.useRealTimers());

  function create() {
    const fixture = TestBed.createComponent(Host);
    TestBed.tick();
    return fixture;
  }

  function set(value: boolean): void {
    wanted.set(value);
    TestBed.tick();
  }

  it('holds nothing while no timer runs', () => {
    create();
    vi.advanceTimersByTime(SESSION_KEEP_ALIVE_MS * 3);

    expect(hold).not.toHaveBeenCalled();
    expect(getAccessToken).not.toHaveBeenCalled();
  });

  it('holds the lock once while a timer runs and keeps the session alive', () => {
    create();
    set(true);
    set(true);

    expect(hold).toHaveBeenCalledTimes(1);
    vi.advanceTimersByTime(SESSION_KEEP_ALIVE_MS - 1);
    expect(getAccessToken).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(getAccessToken).toHaveBeenCalledTimes(1);
    vi.advanceTimersByTime(SESSION_KEEP_ALIVE_MS);
    expect(getAccessToken).toHaveBeenCalledTimes(2);
  });

  it('lets go only after the grace once the timer stops', () => {
    create();
    set(true);
    set(false);

    vi.advanceTimersByTime(WAKE_LOCK_GRACE_MS - 1);
    expect(release).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(release).toHaveBeenCalledTimes(1);

    getAccessToken.mockClear();
    vi.advanceTimersByTime(SESSION_KEEP_ALIVE_MS * 3);
    expect(getAccessToken).not.toHaveBeenCalled();
  });

  it('keeps the lock when the timer runs again inside the grace', () => {
    create();
    set(true);
    set(false);
    vi.advanceTimersByTime(WAKE_LOCK_GRACE_MS - 1);
    set(true);
    vi.advanceTimersByTime(WAKE_LOCK_GRACE_MS * 2);

    expect(hold).toHaveBeenCalledTimes(1);
    expect(release).not.toHaveBeenCalled();
  });

  it('does not restart the grace on a second stop', () => {
    create();
    set(true);
    set(false);
    vi.advanceTimersByTime(WAKE_LOCK_GRACE_MS - 1);
    // An unrelated recalculation that still says "not running".
    wanted.set(true);
    wanted.set(false);
    TestBed.tick();
    vi.advanceTimersByTime(1);

    expect(release).toHaveBeenCalledTimes(1);
  });

  it('holds again after the grace ran out', () => {
    create();
    set(true);
    set(false);
    vi.advanceTimersByTime(WAKE_LOCK_GRACE_MS);
    set(true);

    expect(hold).toHaveBeenCalledTimes(2);
  });

  it('releases everything when the page goes away', () => {
    const fixture = create();
    set(true);
    fixture.destroy();

    expect(release).toHaveBeenCalledTimes(1);
    vi.advanceTimersByTime(SESSION_KEEP_ALIVE_MS * 3);
    expect(getAccessToken).not.toHaveBeenCalled();
  });

  it('releases nothing on destroy when it never held', () => {
    create().destroy();

    expect(release).not.toHaveBeenCalled();
  });
});
