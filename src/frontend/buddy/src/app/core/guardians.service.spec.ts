import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import {
  ChildSummary,
  CreateChildResult,
  GuardianInvite,
  GuardianInvitePreview,
  GuardianSummary,
  GuardiansService,
  SiblingSummary
} from './guardians.service';
import { RuntimeConfigService } from './runtime-config.service';

describe('GuardiansService', () => {
  const apiBaseUrl = 'https://api.buddy.test';

  let service: GuardiansService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    const runtimeConfigStub: Partial<RuntimeConfigService> = { apiBaseUrl };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RuntimeConfigService, useValue: runtimeConfigStub }
      ]
    });

    service = TestBed.inject(GuardiansService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('listMyChildren', () => {
    it('GETs the caller’s children and resolves them', async () => {
      const children: ChildSummary[] = [
        { id: 'child-1', name: { givenName: 'Sam', familyName: 'Kid' }, guardianLinkId: 'link-1', kind: 0, language: 'en' }
      ];

      const promise = service.listMyChildren();

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/children`);
      expect(req.request.method).toBe('GET');
      req.flush(children);

      await expect(promise).resolves.toEqual(children);
    });

    it('resolves an empty list when the caller has no children', async () => {
      const promise = service.listMyChildren();

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/children`);
      req.flush([]);

      await expect(promise).resolves.toEqual([]);
    });

    it('rejects when the server returns an error status', async () => {
      const promise = service.listMyChildren();

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/children`);
      req.flush('boom', { status: 500, statusText: 'Server Error' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('listMyGuardians', () => {
    it('GETs the caller’s guardians and resolves them', async () => {
      const guardians: GuardianSummary[] = [
        { id: 'guardian-1', name: { givenName: 'Gina', familyName: 'G' }, guardianLinkId: 'link-1', kind: 0 }
      ];

      const promise = service.listMyGuardians();

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/guardians`);
      expect(req.request.method).toBe('GET');
      req.flush(guardians);

      await expect(promise).resolves.toEqual(guardians);
    });

    it('resolves an empty list when the caller is not a child of anyone', async () => {
      const promise = service.listMyGuardians();

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/guardians`);
      req.flush([]);

      await expect(promise).resolves.toEqual([]);
    });
  });

  describe('listChildGuardians', () => {
    it('GETs the guardians for a specific child and resolves them', async () => {
      const guardians: GuardianSummary[] = [
        { id: 'guardian-2', name: { givenName: 'Pat', familyName: 'P' }, guardianLinkId: 'link-2', kind: 1 }
      ];

      const promise = service.listChildGuardians('child-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/children/child-1/guardians`);
      expect(req.request.method).toBe('GET');
      req.flush(guardians);

      await expect(promise).resolves.toEqual(guardians);
    });

    it('rejects when the child cannot be found', async () => {
      const promise = service.listChildGuardians('missing-child');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/children/missing-child/guardians`);
      req.flush('not found', { status: 404, statusText: 'Not Found' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('listMySiblings', () => {
    it('GETs the caller’s siblings and resolves them', async () => {
      const siblings: SiblingSummary[] = [{ id: 'sibling-1', name: { givenName: 'Alex', familyName: 'Kid' } }];

      const promise = service.listMySiblings();

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/siblings`);
      expect(req.request.method).toBe('GET');
      req.flush(siblings);

      await expect(promise).resolves.toEqual(siblings);
    });

    it('resolves an empty list when the caller has no siblings', async () => {
      const promise = service.listMySiblings();

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/siblings`);
      req.flush([]);

      await expect(promise).resolves.toEqual([]);
    });
  });

  describe('createChild', () => {
    it('POSTs the request body and resolves the created child with credentials', async () => {
      const created: CreateChildResult = {
        id: 'child-2',
        name: { givenName: 'Sam', familyName: 'Kid' },
        guardianLinkId: 'link-3',
        kind: 0,
        language: 'en',
        username: 'sam.kid',
        temporaryPassword: 'temp-pass-123'
      };

      const promise = service.createChild({ givenName: 'Sam', familyName: 'Kid', username: 'sam.kid' });

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/children`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ givenName: 'Sam', familyName: 'Kid', username: 'sam.kid' });
      req.flush(created);

      await expect(promise).resolves.toEqual(created);
    });

    it('rejects when the username is already taken', async () => {
      const promise = service.createChild({ givenName: 'Sam', familyName: 'Kid', username: 'sam.kid' });

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/children`);
      req.flush('conflict', { status: 409, statusText: 'Conflict' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('revokeChild', () => {
    it('DELETEs the guardian link and resolves', async () => {
      const promise = service.revokeChild('child-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/children/child-1/guardian-link`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      await expect(promise).resolves.toBeNull();
    });

    it('rejects when the link cannot be found', async () => {
      const promise = service.revokeChild('missing-child');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/children/missing-child/guardian-link`);
      req.flush('not found', { status: 404, statusText: 'Not Found' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('updateChildLanguage', () => {
    it('PATCHes the language and resolves the updated child', async () => {
      const updated: ChildSummary = {
        id: 'child-1',
        name: { givenName: 'Sam', familyName: 'Kid' },
        guardianLinkId: 'link-1',
        kind: 0,
        language: 'da'
      };

      const promise = service.updateChildLanguage('child-1', 'da');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/children/child-1/language`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual({ language: 'da' });
      req.flush(updated);

      await expect(promise).resolves.toEqual(updated);
    });

    it('rejects on an unsupported language', async () => {
      const promise = service.updateChildLanguage('child-1', 'xx');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/children/child-1/language`);
      req.flush('invalid', { status: 400, statusText: 'Bad Request' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('listGuardianInvites', () => {
    it('GETs the pending guardian invites for a child and resolves them', async () => {
      const invites: GuardianInvite[] = [
        { id: 'invite-1', email: 'co-parent@buddy.test', kind: 1, invitedAt: '2026-08-01T00:00:00Z', expiresAt: '2026-08-08T00:00:00Z' }
      ];

      const promise = service.listGuardianInvites('child-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/children/child-1/guardian-invites`);
      expect(req.request.method).toBe('GET');
      req.flush(invites);

      await expect(promise).resolves.toEqual(invites);
    });

    it('resolves an empty list when there are no pending invites', async () => {
      const promise = service.listGuardianInvites('child-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/children/child-1/guardian-invites`);
      req.flush([]);

      await expect(promise).resolves.toEqual([]);
    });
  });

  describe('inviteGuardian', () => {
    it('POSTs the invite request and resolves the created invite', async () => {
      const invite: GuardianInvite = {
        id: 'invite-2',
        email: 'co-parent@buddy.test',
        kind: 1,
        invitedAt: '2026-08-01T00:00:00Z',
        expiresAt: '2026-08-08T00:00:00Z'
      };

      const promise = service.inviteGuardian('child-1', { email: 'co-parent@buddy.test', kind: 1 });

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/children/child-1/guardian-invites`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ email: 'co-parent@buddy.test', kind: 1 });
      req.flush(invite);

      await expect(promise).resolves.toEqual(invite);
    });

    it('rejects when the invite already exists', async () => {
      const promise = service.inviteGuardian('child-1', { email: 'co-parent@buddy.test', kind: 1 });

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/children/child-1/guardian-invites`);
      req.flush('conflict', { status: 409, statusText: 'Conflict' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('revokeGuardianInvite', () => {
    it('DELETEs the invite and resolves', async () => {
      const promise = service.revokeGuardianInvite('child-1', 'invite-2');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/children/child-1/guardian-invites/invite-2`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      await expect(promise).resolves.toBeNull();
    });

    it('rejects when the invite cannot be found', async () => {
      const promise = service.revokeGuardianInvite('child-1', 'missing-invite');

      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/children/child-1/guardian-invites/missing-invite`);
      req.flush('not found', { status: 404, statusText: 'Not Found' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('previewGuardianInvite', () => {
    it('GETs the invite preview by token and resolves it', async () => {
      const preview: GuardianInvitePreview = { childGivenName: 'Sam', kind: 1 };

      const promise = service.previewGuardianInvite('token-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/guardian-invites/token-1/preview`);
      expect(req.request.method).toBe('GET');
      req.flush(preview);

      await expect(promise).resolves.toEqual(preview);
    });

    it('rejects when the token is invalid or expired', async () => {
      const promise = service.previewGuardianInvite('expired-token');

      const req = httpMock.expectOne(`${apiBaseUrl}/guardian-invites/expired-token/preview`);
      req.flush('gone', { status: 410, statusText: 'Gone' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('acceptGuardianInvite', () => {
    it('POSTs an empty body to accept the invite and resolves', async () => {
      const promise = service.acceptGuardianInvite('token-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/guardian-invites/token-1/accept`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({});
      req.flush(null);

      await expect(promise).resolves.toBeNull();
    });

    it('rejects when the token has already been used', async () => {
      const promise = service.acceptGuardianInvite('used-token');

      const req = httpMock.expectOne(`${apiBaseUrl}/guardian-invites/used-token/accept`);
      req.flush('conflict', { status: 409, statusText: 'Conflict' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });
});
