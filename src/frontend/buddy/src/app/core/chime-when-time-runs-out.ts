import { Signal, effect, inject } from '@angular/core';

import { ChimePlace, chimeFor } from './timer-preferences';
import { TimeUpChimeService } from './time-up-chime.service';
import { UsersService } from './users.service';
import { TimerPhase } from './visual-timer';

// The timer on screen: which step it times (any stable key) and its phase; null when there is none.
export interface WatchedTimer {
  key: string;
  phase: TimerPhase;
}

/**
 * Plays the child's chime when the timer on screen runs out while the child watches (GitHub issues
 * #49 and #50): only on running -> timesUp for the same step, not when a page opens on a step that
 * was already over, or when the step changes. Silent where the child or a guardian turned the
 * chime off for `place`. Call it in a constructor; the page still has to unlock audio from a tap.
 */
export function chimeWhenTimeRunsOut(timer: Signal<WatchedTimer | null>, place: ChimePlace): void {
  const chime = inject(TimeUpChimeService);
  const users = inject(UsersService);

  let previous: WatchedTimer | null = null;
  effect(() => {
    const current = timer();

    if (
      previous?.key === current?.key &&
      previous?.phase === 'running' &&
      current?.phase === 'timesUp'
    ) {
      const sound = chimeFor(users.currentUser(), place);
      if (sound) {
        chime.play(sound);
      }
    }
    previous = current;
  });
}
