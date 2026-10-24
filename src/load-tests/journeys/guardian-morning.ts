import { group } from 'k6';

import { Api, type Read } from '../lib/api.ts';
import { today } from '../lib/dates.ts';
import { familyFor, think, type RunData } from './common.ts';

// A guardian opening the dashboard in the morning: the same requests the dashboard widgets make
// (features/guardian/*-today), including the per-child fan-out that once exhausted the Postgres
// pools. The per-child and per-calendar requests go at most 4 at a time, like the frontend's
// mapWithConcurrency(…, PER_ITEM_REQUEST_CONCURRENCY). Later iterations send If-None-Match.
export function guardianMorning(data: RunData): void {
  const family = familyFor(data);
  const api = new Api(family.guardian, family.tokens);
  const day = today();
  const range = `from=${day}&to=${day}`;
  const calendar = { calendarId: family.calendarId };

  group('guardian-morning', () => {
    api.get('/users/me');
    api.get('/users/me/children');

    api.getAll(
      family.children.flatMap(({ id }): Read[] => {
        const child = { childId: id };
        return [
          { route: '/medicines/children/{childId}/doses', params: child, query: range },
          { route: '/feelings/children/{childId}/check-ins', params: child, query: range },
          { route: '/first-then/children/{childId}', params: child },
          { route: '/pickups/children/{childId}/schedule', params: child, query: range },
          { route: '/users/me/children/{childId}/guardians', params: child },
          { route: '/mealplans/children/{childId}/plan', params: child, query: range },
        ];
      }),
    );

    api.get('/calendars');
    api.getAll([
      { route: '/calendars/{calendarId}/occurrences', params: calendar, query: range },
      { route: '/calendars/{calendarId}/assignable-members', params: calendar },
    ]);
  });

  think(10, 30);
}
