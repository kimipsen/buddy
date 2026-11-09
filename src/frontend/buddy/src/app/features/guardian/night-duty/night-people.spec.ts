import { describe, expect, it } from 'vitest';

import {
  NOBODY_CHOICE,
  NightPeople,
  SLEEPOVER_CHOICE,
  babysitterChoice,
  choiceOf,
  isCompleteEntry,
  responsibleFromChoice,
  responsibleText,
  sleepoverOf,
} from './night-people';

describe('night-people', () => {
  const people: NightPeople = {
    guardians: [
      {
        id: 'mom',
        name: { givenName: 'Sara', familyName: 'Holm' },
        callingName: 'Mom',
        kind: 'Parent',
      },
      {
        id: 'dad',
        name: { givenName: 'Jonas', familyName: 'Holm' },
        callingName: '',
        kind: 'Parent',
      },
    ] as NightPeople['guardians'],
    babysitters: [{ guardianId: 'mom', id: 'maja', name: 'Maja', contactInfo: '' }],
  };
  const labels = {
    guardian: 'A guardian',
    babysitter: 'Babysitter',
    sleepover: (host: string) => `Sleepover: ${host}`,
  };
  const noSleepover = { hostName: '', location: '', contactInfo: '' };

  it('round-trips every responsible person through a select value', () => {
    expect(choiceOf({ kind: 0, guardianId: 'mom' })).toBe('g:mom');
    expect(choiceOf({ kind: 1, guardianId: 'mom', babysitterId: 'maja' })).toBe('b:mom:maja');
    expect(choiceOf({ kind: 2, hostName: 'Lis' })).toBe(SLEEPOVER_CHOICE);
    expect(choiceOf({ kind: 3 })).toBe(NOBODY_CHOICE);
    expect(babysitterChoice(people.babysitters[0]!)).toBe('b:mom:maja');

    expect(responsibleFromChoice('g:mom', noSleepover)).toEqual({ kind: 0, guardianId: 'mom' });
    expect(responsibleFromChoice('b:mom:maja', noSleepover)).toEqual({
      kind: 1,
      guardianId: 'mom',
      babysitterId: 'maja',
    });
    expect(
      responsibleFromChoice('s', { hostName: ' Lis ', location: ' Skovvej ', contactInfo: ' 20 ' }),
    ).toEqual({ kind: 2, hostName: 'Lis', location: 'Skovvej', contactInfo: '20' });
    expect(responsibleFromChoice('', noSleepover)).toEqual({ kind: 3 });
  });

  it('reads a sleepover draft back', () => {
    expect(
      sleepoverOf({ kind: 2, hostName: 'Lis', location: 'Skovvej', contactInfo: '20' }),
    ).toEqual({ hostName: 'Lis', location: 'Skovvej', contactInfo: '20' });
    expect(sleepoverOf({ kind: 2, hostName: 'Lis' })).toEqual({
      hostName: 'Lis',
      location: '',
      contactInfo: '',
    });
    expect(sleepoverOf({ kind: 3 })).toEqual(noSleepover);
    expect(sleepoverOf(null)).toEqual(noSleepover);
  });

  it('accepts a person, a note or both, and a sleepover only with a host', () => {
    expect(isCompleteEntry({ responsible: { kind: 0, guardianId: 'mom' }, note: '' })).toBe(true);
    expect(isCompleteEntry({ responsible: { kind: 3 }, note: ' Screen ' })).toBe(true);
    expect(isCompleteEntry({ responsible: { kind: 3 }, note: '  ' })).toBe(false);
    expect(isCompleteEntry({ responsible: { kind: 3 } })).toBe(false);
    expect(isCompleteEntry({ responsible: { kind: 2, hostName: ' ' }, note: 'x' })).toBe(false);
    expect(isCompleteEntry({ responsible: { kind: 2, hostName: 'Lis' } })).toBe(true);
  });

  it('names who is on duty', () => {
    expect(responsibleText({ kind: 0, guardianId: 'mom' }, people, labels)).toEqual({
      icon: '👤',
      text: 'Mom',
    });
    expect(responsibleText({ kind: 0, guardianId: 'dad' }, people, labels)?.text).toBe('Jonas');
    expect(responsibleText({ kind: 0, guardianId: 'gone' }, people, labels)?.text).toBe(
      'A guardian',
    );
    expect(
      responsibleText(
        { kind: 1, guardianId: 'mom', babysitterId: 'm', name: 'Maja' },
        people,
        labels,
      ),
    ).toEqual({ icon: '🧑‍🍼', text: 'Maja' });
    expect(
      responsibleText({ kind: 1, guardianId: 'mom', babysitterId: 'm', name: '' }, people, labels)
        ?.text,
    ).toBe('Babysitter');
    expect(responsibleText({ kind: 2, hostName: 'Lis' }, people, labels)).toEqual({
      icon: '🛏️',
      text: 'Sleepover: Lis',
    });
    expect(responsibleText({ kind: 3 }, people, labels)).toBeNull();
  });
});
