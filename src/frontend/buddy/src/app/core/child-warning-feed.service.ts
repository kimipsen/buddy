import { Injectable, computed, inject, signal } from '@angular/core';

import { CalendarOccurrence, CalendarsService } from './calendars.service';
import { ChildClockService } from './child-clock.service';
import { FeaturesService } from './features.service';
import { PickupOccurrence, PickupsService } from './pickups.service';
import {
  MyTransitionWarnings,
  TransitionWarning,
  computeTransitionWarnings,
} from './transition-warnings';
import { TransitionWarningsService } from './transition-warnings.service';
import { UsersService } from './users.service';

// How old the loaded day may get before the banner refetches it: a guardian moving football, or
// switching warnings on, reaches an open iPad within this long (or on its next wake).
export const WARNING_REFRESH_MS = 10 * 60_000;

/**
 * The data behind the child's transition warning banner (Question 6), kept at the root so moving
 * between child pages doesn't refetch. Settings load first; while they are off (the default) it
 * stops there: no occurrences, no pickups. "Got it" dismissals live here, in memory only.
 */
@Injectable({ providedIn: 'root' })
export class ChildWarningFeed {
  private readonly api = inject(TransitionWarningsService);
  private readonly calendars = inject(CalendarsService);
  private readonly pickupsApi = inject(PickupsService);
  private readonly users = inject(UsersService);
  private readonly features = inject(FeaturesService);
  private readonly clock = inject(ChildClockService);

  private readonly settings = signal<MyTransitionWarnings | null>(null);
  private readonly occurrences = signal<readonly CalendarOccurrence[]>([]);
  private readonly pickups = signal<readonly PickupOccurrence[]>([]);
  private readonly dismissed = signal<ReadonlySet<string>>(new Set());
  private loadedAt: number | null = null;
  private loadedDate: string | null = null;
  private inFlight: Promise<void> | null = null;

  /** Whether this child gets warnings at all: the feature is on and a guardian enabled them. */
  readonly active = computed(
    () => this.features.enabled('transitionWarnings') && (this.settings()?.enabled ?? false),
  );

  /** The warnings due at `nowMs`, soonest first, without the stages the child already dismissed. */
  warningsAt(nowMs: number): TransitionWarning[] {
    const settings = this.settings();
    if (!settings || !this.active()) {
      return [];
    }

    const dismissed = this.dismissed();
    return computeTransitionWarnings({
      occurrences: this.occurrences(),
      pickups: this.pickups(),
      settings,
      timeZone: this.clock.timeZone(),
      nowMs,
    }).filter((warning) => !dismissed.has(warning.key));
  }

  dismiss(key: string): void {
    this.dismissed.update((keys) => new Set([...keys, key]));
  }

  /**
   * Loads the first time, and again once the day changed or the data is older than
   * WARNING_REFRESH_MS (or always, with `force`, e.g. when the device wakes).
   */
  refresh(force = false, nowMs = Date.now()): Promise<void> {
    const stale =
      this.loadedAt === null ||
      this.loadedDate !== this.clock.today(nowMs) ||
      nowMs - this.loadedAt >= WARNING_REFRESH_MS;

    if (!this.features.enabled('transitionWarnings') || (!force && !stale)) {
      return Promise.resolve();
    }

    this.inFlight ??= this.load(nowMs).finally(() => (this.inFlight = null));
    return this.inFlight;
  }

  private async load(nowMs: number): Promise<void> {
    const firstLoad = this.loadedAt === null;
    this.loadedAt = nowMs;

    let settings: MyTransitionWarnings | null;
    try {
      settings = await this.api.getMine();
    } catch {
      // Supplementary, like progress: a failed load shows no banner and leaves the page alone.
      settings = null;
    }
    this.settings.set(settings);
    const today = this.clock.today(nowMs);
    this.loadedDate = today;

    if (!settings?.enabled) {
      this.occurrences.set([]);
      this.pickups.set([]);
      return;
    }

    const [occurrences, pickups] = await Promise.all([
      this.loadOccurrences(today, firstLoad),
      this.loadPickups(today),
    ]);
    this.occurrences.set(occurrences);
    this.pickups.set(pickups);
  }

  // The first load shares the child home's same-day cache; later ones ask again, because that
  // cache only knows about this device's own writes.
  private async loadOccurrences(today: string, firstLoad: boolean): Promise<CalendarOccurrence[]> {
    try {
      return firstLoad
        ? await this.calendars.listTodayOccurrences(today)
        : await this.calendars.listOccurrencesInRange(today, today);
    } catch {
      return [];
    }
  }

  private async loadPickups(today: string): Promise<PickupOccurrence[]> {
    if (!this.features.enabled('pickups')) {
      return [];
    }

    try {
      const me = await this.users.ensureCurrentUser();
      return await this.pickupsApi.listSchedule(me.id, today, today);
    } catch {
      return [];
    }
  }
}
