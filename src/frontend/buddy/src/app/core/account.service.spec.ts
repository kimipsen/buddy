import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { AccountService } from './account.service';
import { AuthService } from './auth.service';

describe('AccountService', () => {
  function setup(groups: string[]) {
    const tokenGroups = signal(groups);

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { groups: tokenGroups } as Partial<AuthService> },
      ],
    });

    return { service: TestBed.inject(AccountService), tokenGroups };
  }

  it.each([
    { groups: ['buddy-guardian'], role: 'guardian' },
    { groups: ['buddy-child'], role: 'child' },
    { groups: ['buddy-child', 'buddy-guardian'], role: 'guardian' },
    { groups: ['other', 'buddy-child'], role: 'child' },
    { groups: [], role: null },
    { groups: ['other'], role: null },
  ])('is $role for an account in $groups', ({ groups, role }) => {
    const { service } = setup(groups);

    expect(service.role()).toBe(role);
  });

  it('follows the groups of a refreshed token', () => {
    const { service, tokenGroups } = setup(['buddy-child']);
    expect(service.role()).toBe('child');

    tokenGroups.set([]);

    expect(service.role()).toBeNull();
  });
});
