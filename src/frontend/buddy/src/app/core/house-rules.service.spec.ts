import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import {
  ChildRules,
  HouseRulesService,
  Rule,
  RuleBook,
  RuleScope,
  isRevisionChanged,
  scopeOf,
  workingContent,
} from './house-rules.service';
import { RuntimeConfigService } from './runtime-config.service';

describe('HouseRulesService', () => {
  let service: HouseRulesService;
  let httpMock: HttpTestingController;

  const apiBaseUrl = 'https://api.buddy.test';
  const child: RuleScope = { kind: 'Child', id: 'child-1' };
  const group: RuleScope = { kind: 'Group', id: 'group-1' };
  const childRules = `${apiBaseUrl}/house-rules/children/child-1/rules`;
  const groupRules = `${apiBaseUrl}/house-rules/groups/group-1/rules`;

  const book: RuleBook = {
    scopeKind: 'Group',
    scopeId: 'group-1',
    access: 'Manage',
    children: ['child-1'],
    rules: [],
  };

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

    service = TestBed.inject(HouseRulesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('lists a child book and a group book on their own routes', async () => {
    const forChild = service.listRules(child);
    httpMock.expectOne({ method: 'GET', url: childRules }).flush(book);
    expect(await forChild).toEqual(book);

    const forGroup = service.listRules(group);
    httpMock.expectOne({ method: 'GET', url: groupRules }).flush(book);
    expect(await forGroup).toEqual(book);
  });

  it('adds a rule with an idempotency key', async () => {
    const promise = service.addRule(group, { title: 'Dinner', body: '- No phones' });

    const req = httpMock.expectOne({ method: 'POST', url: groupRules });
    expect(req.request.body).toEqual({ title: 'Dinner', body: '- No phones' });
    expect(req.request.headers.get('Idempotency-Key')).toBeTruthy();
    req.flush(book);

    expect(await promise).toEqual(book);
  });

  it('edits a rule, passing whether children must read it again', async () => {
    const promise = service.editRule(child, 'rule-1', { title: 'Bedtime', body: '' }, false);

    const req = httpMock.expectOne({ method: 'PUT', url: `${childRules}/rule-1` });
    expect(req.request.body).toEqual({
      title: 'Bedtime',
      body: '',
      requireReacknowledgement: false,
    });
    req.flush(book);

    expect(await promise).toEqual(book);
  });

  it('removes a rule', async () => {
    const promise = service.removeRule(group, 'rule-1');

    httpMock.expectOne({ method: 'DELETE', url: `${groupRules}/rule-1` }).flush(null);

    await expect(promise).resolves.toBeNull();
  });

  it('publishes one rule with an idempotency key', async () => {
    const promise = service.publishRule(child, 'rule-1');

    const req = httpMock.expectOne({ method: 'POST', url: `${childRules}/rule-1/publish` });
    expect(req.request.body).toBeNull();
    expect(req.request.headers.get('Idempotency-Key')).toBeTruthy();
    req.flush(book);

    expect(await promise).toEqual(book);
  });

  it('publishes every draft in a book', async () => {
    const promise = service.publishAll(group);

    const req = httpMock.expectOne({ method: 'POST', url: `${groupRules}/publish` });
    expect(req.request.headers.get('Idempotency-Key')).toBeTruthy();
    req.flush(book);

    expect(await promise).toEqual(book);
  });

  it('discards a rule’s draft', async () => {
    const promise = service.discardDraft(group, 'rule-1');

    httpMock.expectOne({ method: 'DELETE', url: `${groupRules}/rule-1/draft` }).flush(null);

    await expect(promise).resolves.toBeNull();
  });

  it('reorders rules with the full new order', async () => {
    const promise = service.reorderRules(child, ['b', 'a']);

    const req = httpMock.expectOne({ method: 'PUT', url: `${childRules}/order` });
    expect(req.request.body).toEqual({ newOrder: ['b', 'a'] });
    req.flush(book);

    expect(await promise).toEqual(book);
  });

  it('acknowledges as the child without a childId, and on behalf of a child with one', async () => {
    const asChild = service.acknowledge(group, 'rule-1', 3);
    const own = httpMock.expectOne({ method: 'PUT', url: `${groupRules}/rule-1/acknowledgement` });
    expect(own.request.body).toEqual({ revision: 3 });
    own.flush(null);
    await asChild;

    const onBehalf = service.acknowledge(child, 'rule-2', 1, 'child-1');
    const proxy = httpMock.expectOne({
      method: 'PUT',
      url: `${childRules}/rule-2/acknowledgement`,
    });
    expect(proxy.request.body).toEqual({ revision: 1, childId: 'child-1' });
    proxy.flush(null);
    await onBehalf;
  });

  it('gets everything one child is asked to keep', async () => {
    const rules: ChildRules = {
      childId: 'child-1',
      personal: { scopeKind: 'Child', scopeId: 'child-1', label: 'Emil', rules: [] },
      households: [],
      pendingAcknowledgements: 0,
    };
    const promise = service.getChildRules('child-1');

    httpMock
      .expectOne({ method: 'GET', url: `${apiBaseUrl}/house-rules/children/child-1` })
      .flush(rules);

    expect(await promise).toEqual(rules);
  });

  it('recognizes the revision-changed conflict', () => {
    expect(isRevisionChanged(new HttpErrorResponse({ status: 409 }))).toBe(true);
    expect(isRevisionChanged(new HttpErrorResponse({ status: 400 }))).toBe(false);
    expect(isRevisionChanged(new Error('boom'))).toBe(false);
  });

  it('turns a child-rules section into a scope', () => {
    expect(scopeOf({ scopeKind: 'Group', scopeId: 'g' })).toEqual({ kind: 'Group', id: 'g' });
  });

  it('edits the draft when there is one, otherwise the published text', () => {
    const published: Rule = {
      id: 'r1',
      title: 'Dinner',
      body: 'No phones',
      revision: 1,
      acknowledgementRevision: 1,
      lastEditedAt: '2026-10-01T10:00:00Z',
      acknowledgements: [],
      isPublished: true,
      draft: null,
    };

    expect(workingContent(published)).toEqual({ title: 'Dinner', body: 'No phones' });
    expect(
      workingContent({
        ...published,
        draft: {
          title: 'Dinner!',
          body: 'No tablets',
          requiresReacknowledgement: true,
          editedAt: '2026-10-02T10:00:00Z',
        },
      }),
    ).toEqual({ title: 'Dinner!', body: 'No tablets' });
  });
});
