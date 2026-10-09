import { Injectable, inject } from '@angular/core';

import { CalendarsService } from '../../../core/calendars.service';
import { addDaysIso } from '../../../core/date-utils';
import { FeaturesService } from '../../../core/features.service';
import { GuardiansService } from '../../../core/guardians.service';
import {
  PER_ITEM_REQUEST_CONCURRENCY,
  mapWithConcurrency,
} from '../../../core/map-with-concurrency';
import { MealplanScope, MealplansService } from '../../../core/mealplans.service';
import { PickupsService } from '../../../core/pickups.service';
import { PRINT_ROW_KIND, PrintRowKind, PrintTemplate } from '../../../core/print-templates.service';
import { WorkLocationsService } from '../../../core/work-locations.service';
import { WEEK_PLAN_DAY_COUNT } from './assemble-week-plan';
import { rowKindFeature } from './template-rows';
import { WeekPlanSources, mealSourceKey } from './week-plan-model';

const CALENDAR_KINDS: PrintRowKind[] = [
  PRINT_ROW_KIND.calendarMarker,
  PRINT_ROW_KIND.calendarEvents,
  PRINT_ROW_KIND.taskChecklist,
];

// Fetches everything one week of a template needs, through the existing endpoints only -- each
// runs its own authorization for the printing guardian. Every source is fetched once however
// many rows name it, and fails on its own: a rejected request becomes null, which marks just the
// rows that depend on it as unavailable (the sheet never fails as a whole).
@Injectable({ providedIn: 'root' })
export class WeekPlanLoader {
  private readonly calendars = inject(CalendarsService);
  private readonly guardians = inject(GuardiansService);
  private readonly mealplans = inject(MealplansService);
  private readonly pickups = inject(PickupsService);
  private readonly workLocations = inject(WorkLocationsService);
  private readonly features = inject(FeaturesService);

  async load(template: PrintTemplate, start: string): Promise<WeekPlanSources> {
    const from = start;
    const to = addDaysIso(start, WEEK_PLAN_DAY_COUNT - 1);
    const rows = template.rows;

    const meals = new Map<string, MealplanScope>();
    for (const row of rows.filter(
      (r) => r.kind === PRINT_ROW_KIND.meal && (Boolean(r.childId) || Boolean(r.mealGroupId)),
    )) {
      // The group scope's name and tier only matter to the meal planner UI; listMealPlan uses the id.
      const scope: MealplanScope = row.mealGroupId
        ? { kind: 'group', groupId: row.mealGroupId, groupName: '', accessTier: 'None' }
        : { kind: 'family', childId: row.childId ?? '' };
      meals.set(mealSourceKey(row), scope);
    }

    const childIds = unique(
      rows.filter((r) => r.kind === PRINT_ROW_KIND.pickup).map((r) => r.childId),
    );
    const guardianIds = unique(
      rows.filter((r) => r.kind === PRINT_ROW_KIND.workLocation).map((r) => r.guardianId),
    );
    const calendarIds = unique(
      rows.filter((r) => CALENDAR_KINDS.includes(r.kind)).flatMap((r) => r.calendarIds ?? []),
    );

    const result = {
      meals: new Map(),
      pickups: new Map(),
      workDays: new Map(),
      occurrences: new Map(),
      names: new Map<string, string>(),
    } satisfies WeekPlanSources;

    const tasks: (() => Promise<void>)[] = [
      ...[...meals].map(
        ([key, scope]) =>
          () =>
            settle(
              this.unlessOff(PRINT_ROW_KIND.meal, () =>
                this.mealplans.listMealPlan(scope, from, to),
              ),
              (v) => result.meals.set(key, v),
            ),
      ),
      ...childIds.map(
        (id) => () =>
          settle(
            this.unlessOff(PRINT_ROW_KIND.pickup, () => this.pickups.listSchedule(id, from, to)),
            (v) => result.pickups.set(id, v),
          ),
      ),
      ...guardianIds.map(
        (id) => () =>
          settle(
            this.unlessOff(PRINT_ROW_KIND.workLocation, () =>
              this.workLocations.listWorkDays(id, from, to),
            ),
            (v) => result.workDays.set(id, v),
          ),
      ),
      ...calendarIds.map(
        (id) => () =>
          settle(this.calendars.listOccurrences(id, from, to), (v) =>
            result.occurrences.set(id, v),
          ),
      ),
      () => this.loadNames(result.names),
    ];

    await mapWithConcurrency(tasks, PER_ITEM_REQUEST_CONCURRENCY, (task) => task());

    return result;
  }

  // A disabled feature's routes aren't mapped, so its rows aren't fetched: an empty source prints
  // them blank rather than "unavailable" -- nothing failed.
  private unlessOff<T>(kind: PrintRowKind, request: () => Promise<T[]>): Promise<T[]> {
    const feature = rowKindFeature(kind);
    return feature === undefined || this.features.enabled(feature)
      ? request()
      : Promise.resolve([]);
  }

  // Given names for the guardian's children and every guardian of those children -- covers pickup
  // guardians and siblings, co-guardians and assignees. Best effort: a failure only loses names.
  private async loadNames(names: Map<string, string>): Promise<void> {
    try {
      const children = await this.guardians.listMyChildren();
      for (const child of children) {
        names.set(child.id, child.name.givenName);
      }

      const guardianLists = await mapWithConcurrency(
        children,
        PER_ITEM_REQUEST_CONCURRENCY,
        (child) => this.guardians.listChildGuardians(child.id).catch(() => []),
      );
      for (const guardian of guardianLists.flat()) {
        names.set(guardian.id, guardian.name.givenName);
      }
    } catch {
      // Names are decoration; the sheet still prints without them.
    }
  }
}

async function settle<T>(request: Promise<T>, store: (value: T | null) => void): Promise<void> {
  try {
    store(await request);
  } catch {
    store(null);
  }
}

function unique(ids: (string | null)[]): string[] {
  return [...new Set(ids.filter((id): id is string => !!id))];
}
