import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { AccountService } from './account.service';
import { GuardianSummary, GuardiansService } from './guardians.service';

describe('AccountService', () => {
  let listMyGuardians: ReturnType<typeof vi.fn<() => Promise<GuardianSummary[]>>>;

  const guardian: GuardianSummary = {
    id: 'guardian-1',
    name: { givenName: 'Gina', familyName: 'G' },
    guardianLinkId: 'link-1',
    kind: 0
  };

  function setup(): AccountService {
    listMyGuardians = vi.fn();

    TestBed.configureTestingModule({
      providers: [{ provide: GuardiansService, useValue: { listMyGuardians } as Partial<GuardiansService> }]
    });

    return TestBed.inject(AccountService);
  }

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('starts with a null role', () => {
    const service = setup();
    expect(service.role()).toBeNull();
  });

  it('resolves to "guardian" when the caller has no guardians of their own, and sets the role signal', async () => {
    const service = setup();
    listMyGuardians.mockResolvedValue([]);

    const role = await service.resolveRole();

    expect(role).toBe('guardian');
    expect(service.role()).toBe('guardian');
    expect(listMyGuardians).toHaveBeenCalledTimes(1);
  });

  it('resolves to "child" when the caller has at least one guardian', async () => {
    const service = setup();
    listMyGuardians.mockResolvedValue([guardian]);

    const role = await service.resolveRole();

    expect(role).toBe('child');
    expect(service.role()).toBe('child');
  });

  it('deduplicates concurrent resolutions into a single underlying request', async () => {
    const service = setup();
    listMyGuardians.mockResolvedValue([]);

    const [first, second] = await Promise.all([service.resolveRole(), service.resolveRole()]);

    expect(first).toBe('guardian');
    expect(second).toBe('guardian');
    expect(listMyGuardians).toHaveBeenCalledTimes(1);
  });

  it('does not re-fetch once the role has already been resolved', async () => {
    const service = setup();
    listMyGuardians.mockResolvedValue([]);

    await service.resolveRole();
    const role = await service.resolveRole();

    expect(role).toBe('guardian');
    expect(listMyGuardians).toHaveBeenCalledTimes(1);
  });

  it('propagates a failed lookup, leaves the role signal null, and allows a subsequent retry', async () => {
    const service = setup();
    listMyGuardians.mockRejectedValueOnce(new Error('boom'));

    await expect(service.resolveRole()).rejects.toThrow('boom');
    expect(service.role()).toBeNull();
    expect(listMyGuardians).toHaveBeenCalledTimes(1);

    // The failed pending resolution is cleared, so a follow-up call retries rather than reusing
    // the rejected promise.
    listMyGuardians.mockResolvedValueOnce([]);
    const role = await service.resolveRole();

    expect(role).toBe('guardian');
    expect(listMyGuardians).toHaveBeenCalledTimes(2);
  });
});
