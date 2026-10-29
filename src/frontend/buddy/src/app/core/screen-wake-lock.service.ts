import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';

// The screen wake lock that keeps a child's timer on screen
// (docs/frontend/analysis/keep-screen-awake.md). Counts holders, so one page releasing can't drop
// another's lock. Where the browser has no Wake Lock API, or refuses the request (low power mode),
// nothing happens and the screen locks as usual.
@Injectable({ providedIn: 'root' })
export class ScreenWakeLockService {
  private readonly document = inject(DOCUMENT);
  private holders = 0;
  private sentinel: WakeLockSentinel | null = null;
  private pending: Promise<void> | null = null;

  constructor() {
    // The browser drops the lock whenever the page is hidden; take it again on the way back.
    this.document.addEventListener('visibilitychange', () => {
      if (this.document.visibilityState === 'visible' && this.holders > 0) {
        void this.acquire();
      }
    });
  }

  /** Takes the lock (if the browser allows) and returns the release function, safe to call twice. */
  hold(): () => void {
    this.holders++;
    void this.acquire();

    let released = false;
    return () => {
      if (released) {
        return;
      }

      released = true;
      this.holders--;
      if (this.holders === 0) {
        void this.drop();
      }
    };
  }

  private acquire(): Promise<void> {
    const wakeLock = this.document.defaultView?.navigator.wakeLock;
    if (!wakeLock || (this.sentinel && !this.sentinel.released)) {
      return Promise.resolve();
    }

    this.pending ??= this.request(wakeLock).finally(() => (this.pending = null));
    return this.pending;
  }

  private async request(wakeLock: WakeLock): Promise<void> {
    try {
      const sentinel = await wakeLock.request('screen');
      // Everyone let go while the request was in flight.
      if (this.holders === 0) {
        await sentinel.release();
        return;
      }

      this.sentinel = sentinel;
    } catch {
      // Refused (low power mode, page hidden meanwhile): the screen locks as it would without Buddy.
    }
  }

  private async drop(): Promise<void> {
    const sentinel = this.sentinel;
    this.sentinel = null;

    try {
      await sentinel?.release();
    } catch {
      // Already gone with the page's visibility; nothing left to release.
    }
  }
}
