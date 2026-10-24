import { group } from 'k6';

import { pollFeed } from '../lib/api.ts';
import { familyFor, think, type RunData } from './common.ts';

// A calendar app polling a family's three feeds, anonymously, with the ETag of its last fetch.
// The calendar feed is the most expensive read per request: it expands every occurrence from 90
// days back to 365 days ahead before the body is hashed (rate-limiting.md, conditional-get-etags.md).
// Real apps poll hourly; the wait here is compressed so a 10-minute run still sees repeat polls.
export function icalPoll(data: RunData): void {
  const { icalPaths } = familyFor(data);

  group('ical-poll', () => {
    pollFeed(icalPaths.calendar, '/calendars/{calendarId}/ical/{token}');
    pollFeed(icalPaths.mealplan, '/mealplans/{mealPlanId}/ical/{token}');
    pollFeed(icalPaths.pickups, '/pickups/feeds/{feedId}/ical/{token}');
  });

  think(30, 60);
}
