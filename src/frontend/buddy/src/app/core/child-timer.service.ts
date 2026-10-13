import { Injectable, inject, signal } from '@angular/core';

import {
  StoredTimer,
  clearTimerAnchor,
  readTimerAnchor,
  writeTimerAnchor,
} from './child-timer-storage';
import { ChildClockService } from './child-clock.service';
import { TaskRun } from './task-run';
import { TimerAnchor, pauseAnchor, reanchor, resumeAnchor, startAnchor } from './visual-timer';

// The child-started timer shared by the child home and the focus view, so a timer started on one
// page keeps running (or stays paused) on the other. Backed by child-timer-storage, so it also
// survives a reload on this device.
@Injectable({ providedIn: 'root' })
export class ChildTimerService {
  // The child's own day, so a timer started late in the evening on a device in another zone still
  // belongs to the day its routine was on.
  private readonly clock = inject(ChildClockService);
  private readonly state = signal<(StoredTimer & { date: string }) | null>(this.load());

  // Today's started timer. Checked against the date on every read, not only at start-up: a tablet
  // left open overnight must not keep yesterday's timer (recurring routines reuse their itemId).
  stored(): StoredTimer | null {
    const state = this.state();
    return state?.date === this.clock.today()
      ? { itemId: state.itemId, anchor: state.anchor }
      : null;
  }

  // The anchor for this run, or undefined when it is schedule-anchored.
  anchorFor(itemId: string): TimerAnchor | undefined {
    const stored = this.stored();
    return stored?.itemId === itemId ? stored.anchor : undefined;
  }

  start(run: TaskRun, nowMs = Date.now()): void {
    this.save(run.itemId, startAnchor(run, nowMs));
  }

  pause(itemId: string, nowMs = Date.now()): void {
    const anchor = this.anchorFor(itemId);
    if (anchor) {
      this.save(itemId, pauseAnchor(anchor, nowMs));
    }
  }

  resume(itemId: string, nowMs = Date.now()): void {
    const anchor = this.anchorFor(itemId);
    if (anchor) {
      this.save(itemId, resumeAnchor(anchor, nowMs));
    }
  }

  // Call with the run as it is after a subtask was ticked or unticked.
  afterCompletionChange(run: TaskRun, nowMs = Date.now()): void {
    const anchor = this.anchorFor(run.itemId);
    if (anchor) {
      this.save(run.itemId, reanchor(run, anchor, nowMs));
    }
  }

  stop(): void {
    this.state.set(null);
    clearTimerAnchor();
  }

  private load(): (StoredTimer & { date: string }) | null {
    const date = this.clock.today();
    const stored = readTimerAnchor(date);
    return stored ? { ...stored, date } : null;
  }

  private save(itemId: string, anchor: TimerAnchor | undefined): void {
    if (!anchor) {
      this.stop();
      return;
    }

    const date = this.clock.today();
    this.state.set({ itemId, anchor, date });
    writeTimerAnchor(itemId, date, anchor);
  }
}
