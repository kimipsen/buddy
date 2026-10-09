import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import {
  CalendarPermissionPolicy,
  GroupDetail,
  GroupInvite,
  GroupInvitePreview,
  GroupMember,
  GroupSummary,
  GroupsService,
  MealplanPermissionPolicy,
  SentGroupInvite,
} from './groups.service';
import { RuntimeConfigService } from './runtime-config.service';

describe('GroupsService', () => {
  const apiBaseUrl = 'https://api.buddy.test';

  let service: GroupsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    const runtimeConfigStub: Partial<RuntimeConfigService> = { apiBaseUrl };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RuntimeConfigService, useValue: runtimeConfigStub },
      ],
    });

    service = TestBed.inject(GroupsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('listMyGroups', () => {
    it('resolves the groups sorted by name', async () => {
      const work: GroupSummary = { id: 'group-2', name: 'Work', role: 'Owner' };
      const home: GroupSummary = { id: 'group-1', name: 'Home', role: 'Owner' };

      const promise = service.listMyGroups();
      httpMock.expectOne(`${apiBaseUrl}/groups`).flush([work, home]);

      await expect(promise).resolves.toEqual([home, work]);
    });

    it('GETs the caller’s groups and resolves them', async () => {
      const groups: GroupSummary[] = [{ id: 'group-1', name: 'Home', role: 'Owner' }];

      const promise = service.listMyGroups();

      const req = httpMock.expectOne(`${apiBaseUrl}/groups`);
      expect(req.request.method).toBe('GET');
      req.flush(groups);

      await expect(promise).resolves.toEqual(groups);
    });

    it('resolves an empty list when the caller has no groups', async () => {
      const promise = service.listMyGroups();

      const req = httpMock.expectOne(`${apiBaseUrl}/groups`);
      req.flush([]);

      await expect(promise).resolves.toEqual([]);
    });

    it('rejects when the server returns an error status', async () => {
      const promise = service.listMyGroups();

      const req = httpMock.expectOne(`${apiBaseUrl}/groups`);
      req.flush('boom', { status: 500, statusText: 'Server Error' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('createGroup', () => {
    it('POSTs the request body and resolves the created group', async () => {
      const created: GroupSummary = { id: 'group-2', name: 'Weekend House', role: 'Owner' };

      const promise = service.createGroup({ name: 'Weekend House' });

      const req = httpMock.expectOne(`${apiBaseUrl}/groups`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ name: 'Weekend House' });
      req.flush(created);

      await expect(promise).resolves.toEqual(created);
    });

    it('rejects when creation fails', async () => {
      const promise = service.createGroup({ name: 'Weekend House' });

      const req = httpMock.expectOne(`${apiBaseUrl}/groups`);
      req.flush('invalid', { status: 400, statusText: 'Bad Request' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('listInvites', () => {
    it('GETs the invites for a group and resolves them', async () => {
      const invites: GroupInvite[] = [
        {
          id: 'invite-1',
          email: 'a@b.test',
          role: 'Member',
          invitedAt: '2026-08-01T00:00:00Z',
          expiresAt: '2026-08-08T00:00:00Z',
        },
      ];

      const promise = service.listInvites('group-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/groups/group-1/invites`);
      expect(req.request.method).toBe('GET');
      req.flush(invites);

      await expect(promise).resolves.toEqual(invites);
    });

    it('resolves an empty list when there are no invites', async () => {
      const promise = service.listInvites('group-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/groups/group-1/invites`);
      req.flush([]);

      await expect(promise).resolves.toEqual([]);
    });
  });

  describe('inviteToGroup', () => {
    it('POSTs the invite request and resolves the created invite', async () => {
      const invite: SentGroupInvite = {
        id: 'invite-2',
        email: 'c@d.test',
        role: 'Admin',
        invitedAt: '2026-08-01T00:00:00Z',
        expiresAt: '2026-08-08T00:00:00Z',
        inviteUrl: 'http://localhost:4300/invite/tok',
      };

      const promise = service.inviteToGroup('group-1', { email: 'c@d.test', role: 'Admin' });

      const req = httpMock.expectOne(`${apiBaseUrl}/groups/group-1/invites`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ email: 'c@d.test', role: 'Admin' });
      req.flush(invite);

      await expect(promise).resolves.toEqual(invite);
    });

    it('rejects when the invite already exists', async () => {
      const promise = service.inviteToGroup('group-1', { email: 'c@d.test', role: 'Admin' });

      const req = httpMock.expectOne(`${apiBaseUrl}/groups/group-1/invites`);
      req.flush('conflict', { status: 409, statusText: 'Conflict' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('revokeInvite', () => {
    it('DELETEs the invite and resolves', async () => {
      const promise = service.revokeInvite('group-1', 'invite-2');

      const req = httpMock.expectOne(`${apiBaseUrl}/groups/group-1/invites/invite-2`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      await expect(promise).resolves.toBeNull();
    });

    it('rejects when the invite cannot be found', async () => {
      const promise = service.revokeInvite('group-1', 'invite-2');

      const req = httpMock.expectOne(`${apiBaseUrl}/groups/group-1/invites/invite-2`);
      req.flush('not found', { status: 404, statusText: 'Not Found' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('addChildToGroup', () => {
    it('PUTs an empty body to add the child and resolves', async () => {
      const promise = service.addChildToGroup('group-1', 'child-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/groups/group-1/children/child-1`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({});
      req.flush(null);

      await expect(promise).resolves.toBeNull();
    });
  });

  describe('deleteGroup', () => {
    it('DELETEs the group and resolves', async () => {
      const promise = service.deleteGroup('group-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/groups/group-1`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      await expect(promise).resolves.toBeNull();
    });
  });

  describe('getGroup', () => {
    it('resolves the members sorted by full name', async () => {
      const sam: GroupMember = {
        userId: 'u-1',
        givenName: 'Sam',
        familyName: 'Kid',
        role: 'Member',
        isChild: true,
      };
      const jamie: GroupMember = {
        userId: 'u-2',
        givenName: 'Jamie',
        familyName: 'Adult',
        role: 'Owner',
        isChild: false,
      };
      const detail: GroupDetail = {
        id: 'group-1',
        name: 'Home',
        members: [sam, jamie],
        calendarPermissionPolicy: { Owner: 'Owner', Admin: 'Contributor', Member: 'Viewer' },
        mealplanPermissionPolicy: { Owner: 'Manage', Admin: 'Manage', Member: 'None' },
        medicinePermissionPolicy: { Owner: 'Manage', Admin: 'Mark', Member: 'None' },
      };

      const promise = service.getGroup('group-1');
      httpMock.expectOne(`${apiBaseUrl}/groups/group-1`).flush(detail);

      await expect(promise).resolves.toEqual({ ...detail, members: [jamie, sam] });
    });

    it('GETs the group detail and resolves it', async () => {
      const detail: GroupDetail = {
        id: 'group-1',
        name: 'Home',
        members: [
          {
            userId: 'user-1',
            givenName: 'Jamie',
            familyName: 'Adult',
            role: 'Owner',
            isChild: false,
          },
        ],
        calendarPermissionPolicy: { Owner: 'Owner', Admin: 'Contributor', Member: 'Viewer' },
        mealplanPermissionPolicy: { Owner: 'Manage', Admin: 'Manage', Member: 'None' },
        medicinePermissionPolicy: { Owner: 'Manage', Admin: 'Mark', Member: 'None' },
      };

      const promise = service.getGroup('group-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/groups/group-1`);
      expect(req.request.method).toBe('GET');
      req.flush(detail);

      await expect(promise).resolves.toEqual(detail);
    });

    it('rejects when the group cannot be found', async () => {
      const promise = service.getGroup('missing-group');

      const req = httpMock.expectOne(`${apiBaseUrl}/groups/missing-group`);
      req.flush('not found', { status: 404, statusText: 'Not Found' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('updateCalendarPermissionPolicy', () => {
    it('PUTs the policy wrapped in a { policy } envelope and resolves', async () => {
      const policy: CalendarPermissionPolicy = {
        Owner: 'Owner',
        Admin: 'Contributor',
        Member: 'Viewer',
      };

      const promise = service.updateCalendarPermissionPolicy('group-1', policy);

      const req = httpMock.expectOne(`${apiBaseUrl}/groups/group-1/calendar-permission-policy`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ policy });
      req.flush(null);

      await expect(promise).resolves.toBeNull();
    });

    it('rejects on a validation error', async () => {
      const policy: CalendarPermissionPolicy = {
        Owner: 'Owner',
        Admin: 'Contributor',
        Member: 'Viewer',
      };

      const promise = service.updateCalendarPermissionPolicy('group-1', policy);

      const req = httpMock.expectOne(`${apiBaseUrl}/groups/group-1/calendar-permission-policy`);
      req.flush('invalid', { status: 400, statusText: 'Bad Request' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('updateMealplanPermissionPolicy', () => {
    it('PUTs the policy wrapped in a { policy } envelope and resolves', async () => {
      const policy: MealplanPermissionPolicy = { Owner: 'Manage', Admin: 'Manage', Member: 'None' };

      const promise = service.updateMealplanPermissionPolicy('group-1', policy);

      const req = httpMock.expectOne(`${apiBaseUrl}/groups/group-1/mealplan-permission-policy`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ policy });
      req.flush(null);

      await expect(promise).resolves.toBeNull();
    });
  });

  describe('previewInvite', () => {
    it('GETs the invite preview by token and resolves it', async () => {
      const preview: GroupInvitePreview = { groupName: 'Home' };

      const promise = service.previewInvite('token-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/invites/token-1/preview`);
      expect(req.request.method).toBe('GET');
      req.flush(preview);

      await expect(promise).resolves.toEqual(preview);
    });

    it('rejects when the token is invalid or expired', async () => {
      const promise = service.previewInvite('expired-token');

      const req = httpMock.expectOne(`${apiBaseUrl}/invites/expired-token/preview`);
      req.flush('gone', { status: 410, statusText: 'Gone' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('acceptInvite', () => {
    it('POSTs an empty body to accept the invite and resolves', async () => {
      const promise = service.acceptInvite('token-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/invites/token-1/accept`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({});
      req.flush(null);

      await expect(promise).resolves.toBeNull();
    });

    it('rejects when the token has already been used', async () => {
      const promise = service.acceptInvite('used-token');

      const req = httpMock.expectOne(`${apiBaseUrl}/invites/used-token/accept`);
      req.flush('conflict', { status: 409, statusText: 'Conflict' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });
});
