import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { RuntimeConfigService } from './runtime-config.service';
import { TaskLibraryService, TaskTemplate } from './task-library.service';

describe('TaskLibraryService', () => {
  let service: TaskLibraryService;
  let httpMock: HttpTestingController;

  const apiBaseUrl = 'https://api.buddy.test';

  function base(): string {
    return `${apiBaseUrl}/task-templates`;
  }

  // Wire-shaped response, matching TaskTemplateResponse/SubtaskResponse exactly (Duration/
  // TotalDuration as the backend's default TimeSpan "c"-format strings) -- distinct from the
  // TaskTemplate the service hands back, which has already converted those to whole minutes.
  function templateResponse(overrides: Record<string, unknown> = {}): Record<string, unknown> {
    return {
      id: 'template-1',
      name: 'Get ready for school',
      icon: '🎒',
      color: '#6366f1',
      subtasks: [],
      totalDuration: '00:00:00',
      isArchived: false,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      ...overrides
    };
  }

  function template(overrides: Partial<TaskTemplate> = {}): TaskTemplate {
    return {
      id: 'template-1',
      name: 'Get ready for school',
      icon: '🎒',
      color: '#6366f1',
      subtasks: [],
      totalDurationMinutes: 0,
      isArchived: false,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      ...overrides
    };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RuntimeConfigService, useValue: { apiBaseUrl } as Partial<RuntimeConfigService> }
      ]
    });

    service = TestBed.inject(TaskLibraryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('listTaskTemplates', () => {
    it('GETs templates for a child, converts wire durations to minutes, and updates the templates signal', async () => {
      const response = templateResponse({
        subtasks: [
          { id: 'subtask-1', title: 'Brush teeth', icon: '🪥', duration: '00:05:00' },
          { id: 'subtask-2', title: 'Get dressed', icon: null, duration: '00:10:00' }
        ],
        totalDuration: '00:15:00'
      });

      const promise = service.listTaskTemplates('child-1');

      const req = httpMock.expectOne(`${base()}/children/child-1`);
      expect(req.request.method).toBe('GET');
      req.flush([response]);

      const expected = template({
        subtasks: [
          { id: 'subtask-1', title: 'Brush teeth', icon: '🪥', durationMinutes: 5 },
          { id: 'subtask-2', title: 'Get dressed', icon: null, durationMinutes: 10 }
        ],
        totalDurationMinutes: 15
      });

      await expect(promise).resolves.toEqual([expected]);
      expect(service.templates()).toEqual([expected]);
    });

    it('resolves with an empty array, and clears state, when the child has no templates', async () => {
      const promise = service.listTaskTemplates('child-1');

      httpMock.expectOne(`${base()}/children/child-1`).flush([]);

      await expect(promise).resolves.toEqual([]);
      expect(service.templates()).toEqual([]);
    });

    it('correctly parses an hours-and-minutes duration (not just minutes-only)', async () => {
      const response = templateResponse({
        subtasks: [{ id: 'subtask-1', title: 'Long task', icon: null, duration: '01:30:00' }],
        totalDuration: '01:30:00'
      });

      const promise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([response]);

      const [result] = await promise;
      expect(result.totalDurationMinutes).toBe(90);
      expect(result.subtasks[0].durationMinutes).toBe(90);
    });

    it('rejects when the backend returns an error status', async () => {
      const promise = service.listTaskTemplates('child-1');
      promise.catch(() => undefined);

      httpMock.expectOne(`${base()}/children/child-1`).flush('boom', { status: 500, statusText: 'Server Error' });

      await expect(promise).rejects.toBeTruthy();
    });
  });

  describe('createTaskTemplate', () => {
    it('POSTs the template details under the child and appends the created template to state', async () => {
      const created = templateResponse({ id: 'template-new', name: 'Bedtime routine' });

      const promise = service.createTaskTemplate('child-1', { name: 'Bedtime routine', icon: '🎒', color: '#6366f1' });

      const req = httpMock.expectOne(`${base()}/children/child-1`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ name: 'Bedtime routine', icon: '🎒', color: '#6366f1' });
      req.flush(created);

      const expected = template({ id: 'template-new', name: 'Bedtime routine' });
      await expect(promise).resolves.toEqual(expected);
      expect(service.templates()).toEqual([expected]);
    });

    it('appends to existing templates state rather than replacing it', async () => {
      const existing = templateResponse({ id: 'template-1' });
      const listPromise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([existing]);
      await listPromise;

      const created = templateResponse({ id: 'template-2', name: 'Toast' });
      const createPromise = service.createTaskTemplate('child-1', { name: 'Toast', icon: '🍞', color: '#111' });
      httpMock.expectOne((r) => r.url === `${base()}/children/child-1` && r.method === 'POST').flush(created);
      await createPromise;

      expect(service.templates()).toEqual([template({ id: 'template-1' }), template({ id: 'template-2', name: 'Toast' })]);
    });
  });

  describe('updateTaskTemplate', () => {
    it('PATCHes the template details and replaces the matching template in state', async () => {
      const original = templateResponse({ id: 'template-1', name: 'Get ready' });
      const listPromise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([original]);
      await listPromise;

      const updated = templateResponse({ id: 'template-1', name: 'Get ready fast' });
      const promise = service.updateTaskTemplate('template-1', { name: 'Get ready fast', icon: '🎒', color: '#6366f1' });

      const req = httpMock.expectOne(`${base()}/template-1`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual({ name: 'Get ready fast', icon: '🎒', color: '#6366f1' });
      req.flush(updated);

      await expect(promise).resolves.toEqual(template({ id: 'template-1', name: 'Get ready fast' }));
      expect(service.templates()).toEqual([template({ id: 'template-1', name: 'Get ready fast' })]);
    });

    it('leaves unrelated templates untouched in state', async () => {
      const other = templateResponse({ id: 'template-other', name: 'Other' });
      const target = templateResponse({ id: 'template-1', name: 'Get ready' });
      const listPromise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([other, target]);
      await listPromise;

      const updated = templateResponse({ id: 'template-1', name: 'Get ready fast' });
      const promise = service.updateTaskTemplate('template-1', { name: 'Get ready fast', icon: '🎒', color: '#6366f1' });
      httpMock.expectOne(`${base()}/template-1`).flush(updated);
      await promise;

      expect(service.templates()).toEqual([template({ id: 'template-other', name: 'Other' }), template({ id: 'template-1', name: 'Get ready fast' })]);
    });
  });

  describe('archiveTaskTemplate', () => {
    it('DELETEs the template and marks it archived in state rather than removing it', async () => {
      const target = templateResponse({ id: 'template-1' });
      const other = templateResponse({ id: 'template-2' });
      const listPromise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([target, other]);
      await listPromise;

      const promise = service.archiveTaskTemplate('template-1');

      const req = httpMock.expectOne(`${base()}/template-1`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      await promise;
      expect(service.templates()).toEqual([template({ id: 'template-1', isArchived: true }), template({ id: 'template-2' })]);
    });
  });

  describe('addSubtask', () => {
    it('POSTs the subtask with duration converted to the wire "c" format, and replaces the template in state', async () => {
      const original = templateResponse({ id: 'template-1', subtasks: [] });
      const listPromise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([original]);
      await listPromise;

      const withSubtask = templateResponse({
        id: 'template-1',
        subtasks: [{ id: 'subtask-1', title: 'Brush teeth', icon: '🪥', duration: '00:05:00' }],
        totalDuration: '00:05:00'
      });

      const promise = service.addSubtask('template-1', 'Brush teeth', '🪥', 5);

      const req = httpMock.expectOne(`${base()}/template-1/subtasks`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ title: 'Brush teeth', icon: '🪥', duration: '00:05:00', position: null });
      req.flush(withSubtask);

      const expected = template({
        id: 'template-1',
        subtasks: [{ id: 'subtask-1', title: 'Brush teeth', icon: '🪥', durationMinutes: 5 }],
        totalDurationMinutes: 5
      });
      await expect(promise).resolves.toEqual(expected);
      expect(service.templates()).toEqual([expected]);
    });

    it('sends an explicit position when one is given', async () => {
      const promise = service.addSubtask('template-1', 'Brush teeth', null, 5, 0);
      promise.catch(() => undefined);

      const req = httpMock.expectOne(`${base()}/template-1/subtasks`);
      expect(req.request.body).toEqual({ title: 'Brush teeth', icon: null, duration: '00:05:00', position: 0 });
      req.flush(templateResponse());
    });

    it('formats a 90-minute duration as 01:30:00', async () => {
      const promise = service.addSubtask('template-1', 'Long task', null, 90);
      promise.catch(() => undefined);

      const req = httpMock.expectOne(`${base()}/template-1/subtasks`);
      expect(req.request.body).toEqual({ title: 'Long task', icon: null, duration: '01:30:00', position: null });
      req.flush(templateResponse());
    });
  });

  describe('updateSubtask', () => {
    it('PATCHes the subtask and replaces the template in state', async () => {
      const updated = templateResponse({
        id: 'template-1',
        subtasks: [{ id: 'subtask-1', title: 'Brush teeth thoroughly', icon: '🪥', duration: '00:07:00' }],
        totalDuration: '00:07:00'
      });

      const promise = service.updateSubtask('template-1', 'subtask-1', 'Brush teeth thoroughly', '🪥', 7);

      const req = httpMock.expectOne(`${base()}/template-1/subtasks/subtask-1`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual({ title: 'Brush teeth thoroughly', icon: '🪥', duration: '00:07:00' });
      req.flush(updated);

      await expect(promise).resolves.toEqual(
        template({
          id: 'template-1',
          subtasks: [{ id: 'subtask-1', title: 'Brush teeth thoroughly', icon: '🪥', durationMinutes: 7 }],
          totalDurationMinutes: 7
        })
      );
    });
  });

  describe('removeSubtask', () => {
    it('DELETEs the subtask, removes it from state, and recomputes the template total duration', async () => {
      const original = templateResponse({
        id: 'template-1',
        subtasks: [
          { id: 'subtask-1', title: 'Brush teeth', icon: null, duration: '00:05:00' },
          { id: 'subtask-2', title: 'Get dressed', icon: null, duration: '00:10:00' }
        ],
        totalDuration: '00:15:00'
      });
      const listPromise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([original]);
      await listPromise;

      const promise = service.removeSubtask('template-1', 'subtask-1');

      const req = httpMock.expectOne(`${base()}/template-1/subtasks/subtask-1`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      await promise;
      expect(service.templates()).toEqual([
        template({
          id: 'template-1',
          subtasks: [{ id: 'subtask-2', title: 'Get dressed', icon: null, durationMinutes: 10 }],
          totalDurationMinutes: 10
        })
      ]);
    });
  });

  describe('reorderSubtasks', () => {
    it('PUTs the new subtask order and replaces the template in state', async () => {
      const original = templateResponse({
        id: 'template-1',
        subtasks: [
          { id: 'subtask-1', title: 'Brush teeth', icon: null, duration: '00:05:00' },
          { id: 'subtask-2', title: 'Get dressed', icon: null, duration: '00:10:00' }
        ],
        totalDuration: '00:15:00'
      });
      const listPromise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([original]);
      await listPromise;

      const reordered = templateResponse({
        id: 'template-1',
        subtasks: [
          { id: 'subtask-2', title: 'Get dressed', icon: null, duration: '00:10:00' },
          { id: 'subtask-1', title: 'Brush teeth', icon: null, duration: '00:05:00' }
        ],
        totalDuration: '00:15:00'
      });

      const promise = service.reorderSubtasks('template-1', ['subtask-2', 'subtask-1']);

      const req = httpMock.expectOne(`${base()}/template-1/subtasks/order`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ newOrder: ['subtask-2', 'subtask-1'] });
      req.flush(reordered);

      const expected = template({
        id: 'template-1',
        subtasks: [
          { id: 'subtask-2', title: 'Get dressed', icon: null, durationMinutes: 10 },
          { id: 'subtask-1', title: 'Brush teeth', icon: null, durationMinutes: 5 }
        ],
        totalDurationMinutes: 15
      });
      await expect(promise).resolves.toEqual(expected);
      expect(service.templates()).toEqual([expected]);
    });
  });
});
