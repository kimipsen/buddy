import { describe, expect, it } from 'vitest';

import { ChildTransitionWarnings } from '../../../core/transition-warnings.service';
import { draftFrom, isDirty, leadChoices, requestFrom, toggleLead } from './warning-settings';

const SAVED: ChildTransitionWarnings = {
  childId: 'c1',
  enabled: false,
  leadMinutes: [15, 5, 1],
  kinds: ['Event', 'Task', 'DropOff', 'PickUp'],
  quietHours: null,
  overrides: [],
};

describe('warning settings form', () => {
  it('starts quiet hours at 21:00-07:00 when none are saved', () => {
    expect(draftFrom(SAVED)).toEqual({
      enabled: false,
      leadMinutes: [15, 5, 1],
      kinds: ['Event', 'Task', 'DropOff', 'PickUp'],
      quietOn: false,
      quietFrom: '21:00',
      quietTo: '07:00',
    });
  });

  it('reads saved quiet hours as HH:mm', () => {
    const draft = draftFrom({ ...SAVED, quietHours: { from: '08:00:00', to: '14:30:00' } });
    expect([draft.quietOn, draft.quietFrom, draft.quietTo]).toEqual([true, '08:00', '14:30']);
  });

  it('sends lead times largest first, kinds in order, and no quiet hours when off', () => {
    expect(
      requestFrom({
        enabled: true,
        leadMinutes: [1, 10],
        kinds: ['PickUp', 'Event'],
        quietOn: false,
        quietFrom: '21:00',
        quietTo: '07:00',
      }),
    ).toEqual({
      enabled: true,
      leadMinutes: [10, 1],
      kinds: ['Event', 'PickUp'],
      quietHours: null,
    });
  });

  it('is dirty only when saving would change something', () => {
    const draft = draftFrom(SAVED);
    expect(isDirty(draft, SAVED)).toBe(false);
    // Typing new quiet-hour times while quiet hours are off changes nothing that is sent.
    expect(isDirty({ ...draft, quietFrom: '20:00' }, SAVED)).toBe(false);
    expect(isDirty({ ...draft, enabled: true }, SAVED)).toBe(true);
    expect(isDirty({ ...draft, leadMinutes: [1, 5, 15] }, SAVED)).toBe(false);
  });

  it('toggles a lead time, keeping at most three', () => {
    expect(toggleLead([15, 5], 10)).toEqual([15, 10, 5]);
    expect(toggleLead([15, 10, 5], 5)).toEqual([15, 10]);
    expect(toggleLead([15, 10, 5], 1)).toEqual([15, 10, 5]);
  });

  it('offers the usual choices plus any other saved value', () => {
    expect(leadChoices([15])).toEqual([30, 20, 15, 10, 5, 2, 1]);
    expect(leadChoices([45, 3])).toEqual([45, 30, 20, 15, 10, 5, 3, 2, 1]);
  });
});
