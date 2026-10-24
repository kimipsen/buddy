import { group } from 'k6';

import { Api, json } from '../lib/api.ts';
import { daysFrom, today } from '../lib/dates.ts';
import { sleepEntry } from '../lib/seed.ts';
import { familyFor, pick, think, type RunData } from './common.ts';

// A guardian ticking things off during the day. Every call appends events and updates an inline
// snapshot in the same transaction, which is the write path the load test exists to measure.
export function guardianWrites(data: RunData): void {
  const family = familyFor(data);
  const api = new Api(family.guardian, family.tokens);
  const child = pick(family.children);
  const day = today();

  group('guardian-writes', () => {
    api.put(
      '/medicines/children/{childId}/doses/{medicineId}',
      { childId: child.id, medicineId: child.medicineId },
      { status: pick(['Taken', 'Pending'] as const) },
      `date=${day}&time=07:30:00`,
    );
    think();

    api.patch(
      '/calendars/{calendarId}/items/{itemId}/completion',
      { calendarId: family.calendarId, itemId: child.taskItemId },
      { date: day, isCompleted: Math.random() < 0.5 },
    );
    think();

    api.put(
      '/sleep-diary/children/{childId}/entries/{date}',
      { childId: child.id, date: daysFrom(-1) },
      sleepEntry(Math.floor(Math.random() * 10)),
    );
    think();

    // Add a one-off event and remove it again, so the calendar doesn't grow without bound over a soak.
    const created = api.post(
      '/calendars/{calendarId}/items',
      { calendarId: family.calendarId },
      {
        title: 'Dentist',
        icon: '🦷',
        color: '#f43f5e',
        schedule: {
          kind: 0,
          startsAt: { date: daysFrom(3), time: '10:00:00' },
          endsAt: { date: daysFrom(3), time: '10:30:00' },
          isAllDay: false,
        },
        recurrence: null,
      },
    );

    if (created.status === 200 || created.status === 201) {
      const { id } = json<{ id: string }>(created);
      api.delete('/calendars/{calendarId}/items/{itemId}', {
        calendarId: family.calendarId,
        itemId: id,
      });
    }
  });

  think(10, 30);
}
