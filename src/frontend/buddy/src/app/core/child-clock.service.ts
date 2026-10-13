import { Injectable, computed, inject } from '@angular/core';

import {
  addDaysIso,
  browserTimeZoneId,
  minutesOfDayInTimeZone,
  toIsoDateInTimeZone,
} from './date-utils';
import { UsersService } from './users.service';

/**
 * "Today" and "now" on the child's own clock: the time zone a guardian set on the child's profile
 * (User.TimeZoneId, docs/backend/analysis/child-timezone-settings.md), not the device's. A family
 * iPad left on another zone, or a trip abroad with the profile unchanged, then still shows the
 * child's day as their calendar times read (UserDatePipe uses the same zone). Falls back to the
 * device's zone until the signed-in user has loaded. Every child page decides "today" here
 * (docs/backend/analysis/transition-warnings.md, Question 9).
 */
@Injectable({ providedIn: 'root' })
export class ChildClockService {
  private readonly users = inject(UsersService);

  readonly timeZone = computed(() => this.users.profileTimeZoneId() ?? browserTimeZoneId());

  /** The child's date ("yyyy-MM-dd") at `nowMs`. */
  today(nowMs = Date.now()): string {
    return toIsoDateInTimeZone(new Date(nowMs), this.timeZone());
  }

  tomorrow(nowMs = Date.now()): string {
    return addDaysIso(this.today(nowMs), 1);
  }

  /** Minutes since the child's midnight (0-1439) at `nowMs`. */
  minutesOfDay(nowMs = Date.now()): number {
    return minutesOfDayInTimeZone(new Date(nowMs), this.timeZone());
  }
}
