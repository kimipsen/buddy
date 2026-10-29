import { DestroyRef, Signal, effect, inject } from '@angular/core';

import { AuthService } from './auth.service';
import { ScreenWakeLockService } from './screen-wake-lock.service';

// Long enough for the child, or a guardian across the room, to see "Time's up".
export const WAKE_LOCK_GRACE_MS = 2 * 60_000;
// getAccessToken() only refreshes near expiry, so with Keycloak's 5-minute access token this
// refreshes about every 8 minutes, well inside its 30-minute idle timeout.
export const SESSION_KEEP_ALIVE_MS = 4 * 60_000;

/**
 * Keeps the screen on, and the session alive, while `wanted()` is true and for WAKE_LOCK_GRACE_MS
 * after it turns false (docs/frontend/analysis/keep-screen-awake.md). Call it in a constructor:
 * it releases everything when the component is destroyed.
 */
export function keepAwakeWhile(wanted: Signal<boolean>): void {
  const wakeLock = inject(ScreenWakeLockService);
  const auth = inject(AuthService);

  let release: (() => void) | null = null;
  let keepAliveId: ReturnType<typeof setInterval> | undefined;
  let graceId: ReturnType<typeof setTimeout> | undefined;

  const cancelGrace = (): void => {
    clearTimeout(graceId);
    graceId = undefined;
  };

  const stop = (): void => {
    cancelGrace();
    clearInterval(keepAliveId);
    release?.();
    release = null;
  };

  effect(() => {
    if (wanted()) {
      cancelGrace();
      if (!release) {
        release = wakeLock.hold();
        // The keep-alive follows the wish, not the lock: it also helps where the API is missing.
        keepAliveId = setInterval(() => void auth.getAccessToken(), SESSION_KEEP_ALIVE_MS);
      }
    } else if (release && graceId === undefined) {
      graceId = setTimeout(stop, WAKE_LOCK_GRACE_MS);
    }
  });

  inject(DestroyRef).onDestroy(stop);
}
