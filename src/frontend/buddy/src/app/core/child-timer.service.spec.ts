import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { nestOccurrence } from '../../testing/occurrence-fixture';
import { CalendarOccurrence } from './calendars.service';
import { readTimerAnchor, writeTimerAnchor } from './child-timer-storage';
import { ChildTimerService } from './child-timer.service';
import { todayIsoDate } from './date-utils';
import { TaskRun, occurrenceKey } from './task-run';
import { UsersService } from './users.service';

describe('ChildTimerService', () => {
  const today = todayIsoDate();

  function subtask(subtaskId: string, isCompleted = false): CalendarOccurrence {
    return nestOccurrence<CalendarOccurrence>({
      itemId: 'run-1',
      kind: 'Task',
      title: subtaskId,
      icon: '🧹',
      iconOverride: null,
      color: '#0f0',
      startsAt: `${today}T07:00:00Z`,
      endsAt: `${today}T07:05:00Z`,
      isAllDay: false,
      isCompleted,
      createdBy: 'g',
      lastModifiedBy: 'g',
      assignedTo: 'c',
      calendarId: 'cal',
      calendarName: 'Home',
      parentTitle: 'Morning',
      subtaskId,
    });
  }

  function run(done: [boolean, boolean] = [false, false]): TaskRun {
    return {
      itemId: 'run-1',
      parentTitle: 'Morning',
      calendarId: 'cal',
      calendarName: 'Home',
      color: '#0f0',
      icon: '🌅',
      subtasks: [subtask('sub-1', done[0]), subtask('sub-2', done[1])],
    };
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  function create(): ChildTimerService {
    return TestBed.inject(ChildTimerService);
  }

  it('starts from what this device stored for today', () => {
    const anchor = { subtaskKey: 'k', startedAtMs: 1, pausedAtMs: null };
    writeTimerAnchor('run-1', today, anchor);

    const service = create();

    expect(service.anchorFor('run-1')).toEqual(anchor);
    expect(service.anchorFor('run-2')).toBeUndefined();
  });

  it('starts, pauses and resumes a run, persisting each step', () => {
    const service = create();
    const key = occurrenceKey(subtask('sub-1'));

    service.start(run(), 1000);
    expect(service.stored()).toEqual({
      itemId: 'run-1',
      anchor: { subtaskKey: key, startedAtMs: 1000, pausedAtMs: null },
    });

    service.pause('run-1', 3000);
    expect(readTimerAnchor(today)?.anchor).toEqual({
      subtaskKey: key,
      startedAtMs: 1000,
      pausedAtMs: 3000,
    });

    service.resume('run-1', 10_000);
    expect(service.anchorFor('run-1')).toEqual({
      subtaskKey: key,
      startedAtMs: 8000,
      pausedAtMs: null,
    });
  });

  it('ignores pause and resume for a run without a timer', () => {
    const service = create();

    service.pause('run-1', 1);
    service.resume('run-1', 2);

    expect(service.stored()).toBeNull();
    expect(readTimerAnchor(today)).toBeNull();
  });

  it('chains to the next step after a tick, and stops when the run is finished', () => {
    const service = create();
    service.start(run(), 1000);

    service.afterCompletionChange(run([true, false]), 5000);
    expect(service.anchorFor('run-1')).toEqual({
      subtaskKey: occurrenceKey(subtask('sub-2')),
      startedAtMs: 5000,
      pausedAtMs: null,
    });

    service.afterCompletionChange(run([true, true]), 6000);
    expect(service.stored()).toBeNull();
    expect(readTimerAnchor(today)).toBeNull();
  });

  it('leaves the schedule alone after a tick on a run it is not timing', () => {
    const service = create();

    service.afterCompletionChange(run([true, false]), 5000);

    expect(service.stored()).toBeNull();
  });

  it("keeps the timer until midnight on the child's profile clock, not the device's", () => {
    // The device says 01:00 on the 13th (UTC); the child's profile zone (New York) is still on the
    // evening of the 12th, so a timer started at bedtime there belongs to the 12th.
    TestBed.configureTestingModule({
      providers: [
        {
          provide: UsersService,
          useValue: { profileTimeZoneId: signal<string | null>('America/New_York').asReadonly() },
        },
      ],
    });
    vi.useFakeTimers({ toFake: ['Date'] });
    try {
      vi.setSystemTime(new Date('2026-10-13T01:00:00Z'));
      const service = create();
      service.start(run(), 1000);
      expect(readTimerAnchor('2026-10-12')?.itemId).toBe('run-1');

      // 23:59 in New York: still the same day there.
      vi.setSystemTime(new Date('2026-10-13T03:59:00Z'));
      expect(service.anchorFor('run-1')).toBeDefined();

      // 00:01 in New York: the next day, so the timer is gone.
      vi.setSystemTime(new Date('2026-10-13T04:01:00Z'));
      expect(service.stored()).toBeNull();
    } finally {
      vi.useRealTimers();
    }
  });

  it("forgets yesterday's timer when the app stays open past midnight", () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    try {
      vi.setSystemTime(new Date(2026, 9, 12, 21, 0));
      const service = create();
      service.start(run(), 1000);
      expect(service.anchorFor('run-1')).toBeDefined();

      vi.setSystemTime(new Date(2026, 9, 13, 7, 0));

      expect(service.stored()).toBeNull();
      expect(service.anchorFor('run-1')).toBeUndefined();
    } finally {
      vi.useRealTimers();
    }
  });

  it('stops the timer and forgets it on this device', () => {
    const service = create();
    service.start(run(), 1000);

    service.stop();

    expect(service.stored()).toBeNull();
    expect(readTimerAnchor(today)).toBeNull();
  });
});
