import { describe, expect, it } from 'vitest';

import { CHILD_GROUP, GUARDIAN_GROUP, readGroups } from './keycloak-groups';

// A JWT with the given payload, base64url-encoded like Keycloak's (no padding, - and _).
function jwt(payload: unknown): string {
  const json = new TextEncoder().encode(JSON.stringify(payload));
  const base64url = btoa(String.fromCharCode(...json))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');

  return `header.${base64url}.signature`;
}

describe('Buddy groups', () => {
  it('match the Keycloak group names the backend checks', () => {
    expect(GUARDIAN_GROUP).toBe('buddy-guardian');
    expect(CHILD_GROUP).toBe('buddy-child');
  });
});

describe('readGroups', () => {
  it('returns the groups claim', () => {
    expect(readGroups(jwt({ groups: ['buddy-guardian', 'buddy-child'] }))).toEqual([
      'buddy-guardian',
      'buddy-child',
    ]);
  });

  it('decodes a base64url payload with non-ASCII text', () => {
    // "ø?>" and the like produce '-' and '_' in base64url and need UTF-8 decoding.
    expect(readGroups(jwt({ name: 'Søren ?>?>?>', groups: ['buddy-child'] }))).toEqual([
      'buddy-child',
    ]);
  });

  it('keeps only the string entries', () => {
    expect(readGroups(jwt({ groups: ['buddy-child', 7, null] }))).toEqual(['buddy-child']);
  });

  it.each([
    { kind: 'no token', token: undefined },
    { kind: 'no payload', token: 'just-one-part' },
    { kind: 'a payload that is not base64', token: 'header.%%%.signature' },
    { kind: 'a payload that is not JSON', token: `header.${btoa('not json')}.signature` },
    { kind: 'no groups claim', token: jwt({ sub: 'x' }) },
    { kind: 'a groups claim that is not a list', token: jwt({ groups: 'buddy-child' }) },
  ])('returns [] for $kind', ({ token }) => {
    expect(readGroups(token)).toEqual([]);
  });
});
