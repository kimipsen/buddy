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
      kind: 'Routine',
      checklist: null,
      suggestions: [],
      ...overrides,
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
      kind: 'Routine',
      eveningBeforeFrom: null,
      suggestions: [],
      ...overrides,
    };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: RuntimeConfigService,
          useValue: { apiBaseUrl } as Partial<RuntimeConfigService>,
        },
      ],
    });

    service = TestBed.inject(TaskLibraryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('listTaskTemplates', () => {
    it('resolves and exposes the templates sorted by name', async () => {
      const promise = service.listTaskTemplates('child-1');
      httpMock
        .expectOne(`${base()}/children/child-1`)
        .flush([
          templateResponse({ id: 'template-2', name: 'Pack bag' }),
          templateResponse({ id: 'template-1', name: 'Get ready' }),
        ]);

      const sorted = [
        template({ id: 'template-1', name: 'Get ready' }),
        template({ id: 'template-2', name: 'Pack bag' }),
      ];
      await expect(promise).resolves.toEqual(sorted);
      expect(service.templates()).toEqual(sorted);
    });

    it('GETs templates for a child, converts wire durations to minutes, and updates the templates signal', async () => {
      const response = templateResponse({
        subtasks: [
          { id: 'subtask-1', title: 'Brush teeth', icon: '🪥', duration: '00:05:00', weekdays: [] },
          { id: 'subtask-2', title: 'Get dressed', icon: null, duration: '00:10:00', weekdays: [] },
        ],
        totalDuration: '00:15:00',
      });

      const promise = service.listTaskTemplates('child-1');

      const req = httpMock.expectOne(`${base()}/children/child-1`);
      expect(req.request.method).toBe('GET');
      req.flush([response]);

      const expected = template({
        subtasks: [
          { id: 'subtask-1', title: 'Brush teeth', icon: '🪥', durationMinutes: 5, weekdays: [] },
          { id: 'subtask-2', title: 'Get dressed', icon: null, durationMinutes: 10, weekdays: [] },
        ],
        totalDurationMinutes: 15,
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
        subtasks: [
          { id: 'subtask-1', title: 'Long task', icon: null, duration: '01:30:00', weekdays: [] },
        ],
        totalDuration: '01:30:00',
      });

      const promise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([response]);

      const [result] = await promise;
      expect(result.totalDurationMinutes).toBe(90);
      expect(result.subtasks[0].durationMinutes).toBe(90);
    });

    it('includes the day component of a "c"-format duration, including multi-digit days', async () => {
      const response = templateResponse({
        subtasks: [{ id: 'subtask-1', title: 'Camp', icon: null, duration: '1.02:30:00' }],
        totalDuration: '12.00:05:00',
      });

      const promise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([response]);

      const [result] = await promise;
      expect(result.subtasks[0].durationMinutes).toBe((24 + 2) * 60 + 30);
      expect(result.totalDurationMinutes).toBe(12 * 24 * 60 + 5);
    });

    it('treats a duration that does not start with the "c" format as zero minutes', async () => {
      const response = templateResponse({
        subtasks: [{ id: 'subtask-1', title: 'Odd', icon: null, duration: 'x00:05:00' }],
        totalDuration: 'not-a-duration',
      });

      const promise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([response]);

      const [result] = await promise;
      expect(result.subtasks[0].durationMinutes).toBe(0);
      expect(result.totalDurationMinutes).toBe(0);
    });

    it('rejects when the backend returns an error status', async () => {
      const promise = service.listTaskTemplates('child-1');
      promise.catch(() => undefined);

      httpMock
        .expectOne(`${base()}/children/child-1`)
        .flush('boom', { status: 500, statusText: 'Server Error' });

      await expect(promise).rejects.toBeTruthy();
    });
  });

  describe('clearTemplates', () => {
    it('empties the shared templates state', async () => {
      const listPromise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([templateResponse()]);
      await listPromise;
      expect(service.templates()).toHaveLength(1);

      service.clearTemplates();

      expect(service.templates()).toEqual([]);
    });
  });

  describe('createTaskTemplate', () => {
    it('POSTs the template details under the child and appends the created template to state', async () => {
      const created = templateResponse({ id: 'template-new', name: 'Bedtime routine' });

      const promise = service.createTaskTemplate('child-1', {
        name: 'Bedtime routine',
        icon: '🎒',
        color: '#6366f1',
      });

      const req = httpMock.expectOne(`${base()}/children/child-1`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        name: 'Bedtime routine',
        icon: '🎒',
        color: '#6366f1',
        kind: 'Routine',
        eveningBeforeFrom: null,
      });
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
      const createPromise = service.createTaskTemplate('child-1', {
        name: 'Toast',
        icon: '🍞',
        color: '#111',
      });
      httpMock
        .expectOne((r) => r.url === `${base()}/children/child-1` && r.method === 'POST')
        .flush(created);
      await createPromise;

      expect(service.templates()).toEqual([
        template({ id: 'template-1' }),
        template({ id: 'template-2', name: 'Toast' }),
      ]);
    });
  });

  describe('updateTaskTemplate', () => {
    it('PATCHes the template details and replaces the matching template in state', async () => {
      const original = templateResponse({ id: 'template-1', name: 'Get ready' });
      const listPromise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([original]);
      await listPromise;

      const updated = templateResponse({ id: 'template-1', name: 'Get ready fast' });
      const promise = service.updateTaskTemplate('template-1', {
        name: 'Get ready fast',
        icon: '🎒',
        color: '#6366f1',
      });

      const req = httpMock.expectOne(`${base()}/template-1`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual({ name: 'Get ready fast', icon: '🎒', color: '#6366f1' });
      req.flush(updated);

      await expect(promise).resolves.toEqual(
        template({ id: 'template-1', name: 'Get ready fast' }),
      );
      expect(service.templates()).toEqual([template({ id: 'template-1', name: 'Get ready fast' })]);
    });

    it('leaves unrelated templates untouched in state', async () => {
      const other = templateResponse({ id: 'template-other', name: 'Other' });
      const target = templateResponse({ id: 'template-1', name: 'Get ready' });
      const listPromise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([other, target]);
      await listPromise;

      const updated = templateResponse({ id: 'template-1', name: 'Get ready fast' });
      const promise = service.updateTaskTemplate('template-1', {
        name: 'Get ready fast',
        icon: '🎒',
        color: '#6366f1',
      });
      httpMock.expectOne(`${base()}/template-1`).flush(updated);
      await promise;

      expect(service.templates()).toEqual([
        template({ id: 'template-1', name: 'Get ready fast' }),
        template({ id: 'template-other', name: 'Other' }),
      ]);
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
      expect(service.templates()).toEqual([
        template({ id: 'template-1', isArchived: true }),
        template({ id: 'template-2' }),
      ]);
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
        subtasks: [
          { id: 'subtask-1', title: 'Brush teeth', icon: '🪥', duration: '00:05:00', weekdays: [] },
        ],
        totalDuration: '00:05:00',
      });

      const promise = service.addSubtask('template-1', 'Brush teeth', '🪥', 5);

      const req = httpMock.expectOne(`${base()}/template-1/subtasks`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        title: 'Brush teeth',
        icon: '🪥',
        duration: '00:05:00',
        position: null,
        weekdays: null,
      });
      req.flush(withSubtask);

      const expected = template({
        id: 'template-1',
        subtasks: [
          { id: 'subtask-1', title: 'Brush teeth', icon: '🪥', durationMinutes: 5, weekdays: [] },
        ],
        totalDurationMinutes: 5,
      });
      await expect(promise).resolves.toEqual(expected);
      expect(service.templates()).toEqual([expected]);
    });

    it('sends an explicit position when one is given', async () => {
      const promise = service.addSubtask('template-1', 'Brush teeth', null, 5, 0);
      promise.catch(() => undefined);

      const req = httpMock.expectOne(`${base()}/template-1/subtasks`);
      expect(req.request.body).toEqual({
        title: 'Brush teeth',
        icon: null,
        duration: '00:05:00',
        position: 0,
        weekdays: null,
      });
      req.flush(templateResponse());
    });

    it('formats a 90-minute duration as 01:30:00', async () => {
      const promise = service.addSubtask('template-1', 'Long task', null, 90);
      promise.catch(() => undefined);

      const req = httpMock.expectOne(`${base()}/template-1/subtasks`);
      expect(req.request.body).toEqual({
        title: 'Long task',
        icon: null,
        duration: '01:30:00',
        position: null,
        weekdays: null,
      });
      req.flush(templateResponse());
    });
  });

  describe('updateSubtask', () => {
    it('PATCHes the subtask and replaces the template in state', async () => {
      const original = templateResponse({
        id: 'template-1',
        subtasks: [
          { id: 'subtask-1', title: 'Brush teeth', icon: '🪥', duration: '00:05:00', weekdays: [] },
        ],
        totalDuration: '00:05:00',
      });
      const listPromise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([original]);
      await listPromise;

      const updated = templateResponse({
        id: 'template-1',
        subtasks: [
          {
            id: 'subtask-1',
            title: 'Brush teeth thoroughly',
            icon: '🪥',
            duration: '00:07:00',
            weekdays: [],
          },
        ],
        totalDuration: '00:07:00',
      });

      const promise = service.updateSubtask(
        'template-1',
        'subtask-1',
        'Brush teeth thoroughly',
        '🪥',
        7,
      );

      const req = httpMock.expectOne(`${base()}/template-1/subtasks/subtask-1`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual({
        title: 'Brush teeth thoroughly',
        icon: '🪥',
        duration: '00:07:00',
        weekdays: null,
      });
      req.flush(updated);

      const expected = template({
        id: 'template-1',
        subtasks: [
          {
            id: 'subtask-1',
            title: 'Brush teeth thoroughly',
            icon: '🪥',
            durationMinutes: 7,
            weekdays: [],
          },
        ],
        totalDurationMinutes: 7,
      });
      await expect(promise).resolves.toEqual(expected);
      expect(service.templates()).toEqual([expected]);
    });
  });

  describe('removeSubtask', () => {
    it('DELETEs the subtask, removes it from state, and recomputes the template total duration', async () => {
      const original = templateResponse({
        id: 'template-1',
        subtasks: [
          { id: 'subtask-1', title: 'Brush teeth', icon: null, duration: '00:05:00', weekdays: [] },
          { id: 'subtask-2', title: 'Get dressed', icon: null, duration: '00:10:00', weekdays: [] },
        ],
        totalDuration: '00:15:00',
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
          subtasks: [
            {
              id: 'subtask-2',
              title: 'Get dressed',
              icon: null,
              durationMinutes: 10,
              weekdays: [],
            },
          ],
          totalDurationMinutes: 10,
        }),
      ]);
    });
    it('leaves other templates in state untouched (same object)', async () => {
      const target = templateResponse({
        id: 'template-1',
        subtasks: [
          { id: 'subtask-1', title: 'Brush teeth', icon: null, duration: '00:05:00', weekdays: [] },
        ],
        totalDuration: '00:05:00',
      });
      const other = templateResponse({
        id: 'template-2',
        subtasks: [
          { id: 'subtask-9', title: 'Pack bag', icon: null, duration: '00:03:00', weekdays: [] },
        ],
        totalDuration: '00:03:00',
      });
      const listPromise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([target, other]);
      await listPromise;
      const otherBefore = service.templates()[1];

      const promise = service.removeSubtask('template-1', 'subtask-1');
      httpMock.expectOne(`${base()}/template-1/subtasks/subtask-1`).flush(null);
      await promise;

      expect(service.templates()[1]).toBe(otherBefore);
      expect(service.templates()[0].subtasks).toEqual([]);
    });
  });

  describe('reorderSubtasks', () => {
    it('PUTs the new subtask order and replaces the template in state', async () => {
      const original = templateResponse({
        id: 'template-1',
        subtasks: [
          { id: 'subtask-1', title: 'Brush teeth', icon: null, duration: '00:05:00', weekdays: [] },
          { id: 'subtask-2', title: 'Get dressed', icon: null, duration: '00:10:00', weekdays: [] },
        ],
        totalDuration: '00:15:00',
      });
      const listPromise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([original]);
      await listPromise;

      const reordered = templateResponse({
        id: 'template-1',
        subtasks: [
          { id: 'subtask-2', title: 'Get dressed', icon: null, duration: '00:10:00', weekdays: [] },
          { id: 'subtask-1', title: 'Brush teeth', icon: null, duration: '00:05:00', weekdays: [] },
        ],
        totalDuration: '00:15:00',
      });

      const promise = service.reorderSubtasks('template-1', ['subtask-2', 'subtask-1']);

      const req = httpMock.expectOne(`${base()}/template-1/subtasks/order`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ newOrder: ['subtask-2', 'subtask-1'] });
      req.flush(reordered);

      const expected = template({
        id: 'template-1',
        subtasks: [
          { id: 'subtask-2', title: 'Get dressed', icon: null, durationMinutes: 10, weekdays: [] },
          { id: 'subtask-1', title: 'Brush teeth', icon: null, durationMinutes: 5, weekdays: [] },
        ],
        totalDurationMinutes: 15,
      });
      await expect(promise).resolves.toEqual(expected);
      expect(service.templates()).toEqual([expected]);
    });
  });

  describe('packing checklists', () => {
    const suggestion = {
      id: 'suggestion-1',
      title: 'Recorder',
      weekdays: ['Friday'],
      suggestedBy: 'child-1',
      status: 'Pending',
      suggestedAt: '2026-10-12T15:00:00Z',
      resolvedAt: null,
      subtaskId: null,
    };

    function checklistResponse(overrides: Record<string, unknown> = {}): Record<string, unknown> {
      return templateResponse({
        name: 'School bag',
        kind: 'Checklist',
        checklist: { eveningBeforeFrom: '17:00:00' },
        subtasks: [
          {
            id: 'item-1',
            title: 'Gym clothes',
            icon: null,
            duration: '00:00:00',
            weekdays: ['Tuesday'],
          },
        ],
        suggestions: [suggestion],
        ...overrides,
      });
    }

    async function seedChecklist(): Promise<void> {
      const listPromise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([checklistResponse()]);
      await listPromise;
    }

    it('maps the kind, the evening-before time as HH:mm, the item weekdays and the suggestions', async () => {
      const promise = service.listTaskTemplates('child-1');
      httpMock.expectOne(`${base()}/children/child-1`).flush([checklistResponse()]);

      const [checklist] = await promise;
      expect(checklist?.kind).toBe('Checklist');
      expect(checklist?.eveningBeforeFrom).toBe('17:00');
      expect(checklist?.subtasks[0]?.weekdays).toEqual(['Tuesday']);
      expect(checklist?.subtasks[0]?.durationMinutes).toBe(0);
      expect(checklist?.suggestions).toEqual([suggestion]);
    });

    it('fetchTaskTemplates reads a child without replacing the shared state', async () => {
      await seedChecklist();

      const promise = service.fetchTaskTemplates('child-2');
      httpMock
        .expectOne(`${base()}/children/child-2`)
        .flush([templateResponse({ id: 'template-9', name: 'Bedtime' })]);

      expect((await promise).map((t) => t.id)).toEqual(['template-9']);
      expect(service.templates().map((t) => t.id)).toEqual(['template-1']);
    });

    it('creates a checklist with its kind and the evening-before time on the wire format', async () => {
      const promise = service.createTaskTemplate('child-1', {
        name: 'School bag',
        icon: '🎒',
        color: '#6366f1',
        kind: 'Checklist',
        eveningBeforeFrom: '17:00',
      });

      const req = httpMock.expectOne(`${base()}/children/child-1`);
      expect(req.request.headers.has('Idempotency-Key')).toBe(true);
      expect(req.request.body).toEqual({
        name: 'School bag',
        icon: '🎒',
        color: '#6366f1',
        kind: 'Checklist',
        eveningBeforeFrom: '17:00:00',
      });
      req.flush(checklistResponse());

      expect((await promise).kind).toBe('Checklist');
    });

    it('sends an item weekday list when adding and updating, and null for every day', async () => {
      const add = service.addSubtask('template-1', 'Gym clothes', null, 0, null, ['Tuesday']);
      const addReq = httpMock.expectOne(`${base()}/template-1/subtasks`);
      expect(addReq.request.body).toEqual({
        title: 'Gym clothes',
        icon: null,
        duration: '00:00:00',
        position: null,
        weekdays: ['Tuesday'],
      });
      addReq.flush(checklistResponse());
      await add;

      const update = service.updateSubtask('template-1', 'item-1', 'Gym clothes', null, 0, []);
      const updateReq = httpMock.expectOne(`${base()}/template-1/subtasks/item-1`);
      expect(updateReq.request.body).toEqual({
        title: 'Gym clothes',
        icon: null,
        duration: '00:00:00',
        weekdays: null,
      });
      updateReq.flush(checklistResponse());
      await update;
    });

    it('PUTs the checklist options and replaces the template in state', async () => {
      await seedChecklist();

      const promise = service.setChecklistOptions('template-1', '18:30');
      const req = httpMock.expectOne(`${base()}/template-1/checklist-options`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ eveningBeforeFrom: '18:30:00' });
      req.flush(checklistResponse({ checklist: { eveningBeforeFrom: '18:30:00' } }));

      await promise;
      expect(service.templates()[0]?.eveningBeforeFrom).toBe('18:30');
    });

    it('clears the evening-before time with null', async () => {
      const promise = service.setChecklistOptions('template-1', null);
      const req = httpMock.expectOne(`${base()}/template-1/checklist-options`);
      expect(req.request.body).toEqual({ eveningBeforeFrom: null });
      req.flush(checklistResponse({ checklist: { eveningBeforeFrom: null } }));

      expect((await promise).eveningBeforeFrom).toBeNull();
    });

    it('copies a template to another child with an idempotency key and leaves the state alone', async () => {
      await seedChecklist();

      const promise = service.copyTaskTemplate('template-1', 'child-2');
      const req = httpMock.expectOne(`${base()}/template-1/copy`);
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.has('Idempotency-Key')).toBe(true);
      expect(req.request.body).toEqual({ childId: 'child-2' });
      req.flush(checklistResponse({ id: 'template-copy' }));

      expect((await promise).id).toBe('template-copy');
      expect(service.templates().map((t) => t.id)).toEqual(['template-1']);
    });

    it('suggests an item with an idempotency key', async () => {
      const promise = service.suggestChecklistItem('template-1', 'Recorder', ['Friday']);
      const req = httpMock.expectOne(`${base()}/template-1/suggestions`);
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.has('Idempotency-Key')).toBe(true);
      expect(req.request.body).toEqual({ title: 'Recorder', weekdays: ['Friday'] });
      req.flush(checklistResponse());

      expect((await promise).suggestions).toEqual([suggestion]);
    });

    it('suggests an item for every day with null weekdays', async () => {
      const promise = service.suggestChecklistItem('template-1', 'Recorder');
      const req = httpMock.expectOne(`${base()}/template-1/suggestions`);
      expect(req.request.body).toEqual({ title: 'Recorder', weekdays: null });
      req.flush(checklistResponse());
      await promise;
    });

    it('withdraws a suggestion', async () => {
      const promise = service.withdrawChecklistSuggestion('template-1', 'suggestion-1');
      const req = httpMock.expectOne(`${base()}/template-1/suggestions/suggestion-1/withdraw`);
      expect(req.request.method).toBe('POST');
      req.flush(checklistResponse({ suggestions: [{ ...suggestion, status: 'Withdrawn' }] }));

      expect((await promise).suggestions[0]?.status).toBe('Withdrawn');
    });

    it('approves a suggestion with the guardian title and days and replaces the template in state', async () => {
      await seedChecklist();

      const promise = service.approveChecklistSuggestion('template-1', 'suggestion-1', 'Recorder', [
        'Friday',
      ]);
      const req = httpMock.expectOne(`${base()}/template-1/suggestions/suggestion-1/approve`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ title: 'Recorder', weekdays: ['Friday'], position: null });
      req.flush(checklistResponse({ suggestions: [{ ...suggestion, status: 'Approved' }] }));

      await promise;
      expect(service.templates()[0]?.suggestions[0]?.status).toBe('Approved');
    });

    it('declines a suggestion and replaces the template in state', async () => {
      await seedChecklist();

      const promise = service.declineChecklistSuggestion('template-1', 'suggestion-1');
      const req = httpMock.expectOne(`${base()}/template-1/suggestions/suggestion-1/decline`);
      expect(req.request.method).toBe('POST');
      req.flush(checklistResponse({ suggestions: [{ ...suggestion, status: 'Declined' }] }));

      await promise;
      expect(service.templates()[0]?.suggestions[0]?.status).toBe('Declined');
    });
  });
});
