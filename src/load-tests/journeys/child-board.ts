import { group } from 'k6';
import exec from 'k6/execution';

import { Api } from '../lib/api.ts';
import { today } from '../lib/dates.ts';
import { familyFor, pick, think, type RunData } from './common.ts';

// A child on their own board (features/child): today's tasks, the First-Then board, feelings,
// progress, transition warnings and meals, then ticking off a task and, now and then, a feelings
// check-in.
export function childBoard(data: RunData): void {
  const family = familyFor(data);
  const child = pick(family.children);
  const api = new Api(child.username, child.tokens);
  const day = today();
  const range = `from=${day}&to=${day}`;

  group('child-board', () => {
    api.get('/users/me');
    api.get('/calendars');
    api.getAll([
      {
        route: '/calendars/{calendarId}/occurrences',
        params: { calendarId: family.calendarId },
        query: range,
      },
      { route: '/first-then/me' },
      { route: '/feelings/me/today' },
      { route: '/progress/me' },
      { route: '/transition-warnings/me' },
      { route: '/mealplans/children/{childId}/plan', params: { childId: child.id }, query: range },
    ]);
    think();

    api.patch(
      '/calendars/{calendarId}/items/{itemId}/completion',
      { calendarId: family.calendarId, itemId: child.taskItemId },
      { date: day, isCompleted: Math.random() < 0.5 },
    );

    if (exec.vu.iterationInScenario % 3 === 0) {
      think();
      api.post(
        '/feelings/me/check-ins',
        {},
        { zone: pick(['Blue', 'Green', 'Yellow', 'Red'] as const) },
      );
    }
  });

  think(10, 30);
}
