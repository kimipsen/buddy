import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import {
  ChildRule,
  ChildRules as ChildRulesData,
  HouseRulesService,
} from '../../../core/house-rules.service';
import { CurrentUser, UsersService } from '../../../core/users.service';
import { ChildRules } from './child-rules';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function rule(id: string, title: string, overrides: Partial<ChildRule> = {}): ChildRule {
  return {
    id,
    title,
    body: '- **Tablet** on the charger',
    revision: 1,
    acknowledgedRevision: null,
    isUpToDate: false,
    lastEditedAt: '2026-10-01T10:00:00Z',
    ...overrides,
  };
}

function rules(overrides: Partial<ChildRulesData> = {}): ChildRulesData {
  return {
    childId: 'emil',
    personal: {
      scopeKind: 'Child',
      scopeId: 'emil',
      label: 'Emil',
      rules: [rule('p1', 'Gaming', { revision: 2, acknowledgedRevision: 1 })],
    },
    households: [
      {
        scopeKind: 'Group',
        scopeId: 'home',
        label: 'Holm family',
        rules: [
          rule('h1', 'Dinner'),
          rule('h2', 'Shoes off', { acknowledgedRevision: 1, isUpToDate: true }),
        ],
      },
      { scopeKind: 'Group', scopeId: 'empty', label: 'Empty home', rules: [] },
    ],
    pendingAcknowledgements: 2,
    ...overrides,
  };
}

describe('ChildRules', () => {
  async function setup(houseRules: Partial<HouseRulesService> = {}) {
    const stub: Partial<HouseRulesService> = {
      getChildRules: vi.fn(async () => rules()),
      acknowledge: vi.fn(async () => undefined),
      ...houseRules,
    };
    const users: Partial<UsersService> = {
      ensureCurrentUser: vi.fn(async () => ({ id: 'emil' }) as CurrentUser),
      timeZoneId: signal('UTC').asReadonly(),
    };

    await TestBed.configureTestingModule({
      imports: [ChildRules],
      providers: [
        provideRouter([]),
        { provide: HouseRulesService, useValue: stub },
        { provide: UsersService, useValue: users },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ChildRules);
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, houseRules: stub };
  }

  function card(compiled: HTMLElement, id: string): HTMLElement {
    return compiled.querySelector(`[data-rule-id="${id}"]`)!;
  }

  function readButton(root: HTMLElement): HTMLButtonElement | undefined {
    return [...root.querySelectorAll('button')].find(
      (b) => b.textContent?.trim() === 'I’ve read this',
    );
  }

  it('shows the child’s own rules first, then each household with rules', async () => {
    const { compiled, houseRules } = await setup();

    expect(houseRules.getChildRules).toHaveBeenCalledWith('emil');
    expect(compiled.querySelector('h1')?.textContent?.trim()).toBe('Our rules');
    expect([...compiled.querySelectorAll('h2')].map((h) => h.textContent?.trim())).toEqual([
      'Just for you',
      'Holm family',
    ]);
    expect(card(compiled, 'p1').querySelector('app-markdown-view strong')?.textContent).toBe(
      'Tablet',
    );
  });

  it('marks new, changed and read rules', async () => {
    const { compiled } = await setup();

    expect(card(compiled, 'h1').textContent).toContain('New');
    expect(card(compiled, 'p1').textContent).toContain('Changed');
    expect(card(compiled, 'h2').textContent).toContain('Read');
    expect(readButton(card(compiled, 'h2'))).toBeUndefined();
  });

  it('acknowledges the revision the child read, in the rule’s own book', async () => {
    const { fixture, compiled, houseRules } = await setup();

    readButton(card(compiled, 'p1'))!.click();
    await settle(fixture);

    expect(houseRules.acknowledge).toHaveBeenCalledWith({ kind: 'Child', id: 'emil' }, 'p1', 2);
    expect(readButton(card(compiled, 'p1'))).toBeUndefined();
    expect(card(compiled, 'p1').textContent).toContain('Read');

    readButton(card(compiled, 'h1'))!.click();
    await settle(fixture);
    expect(houseRules.acknowledge).toHaveBeenLastCalledWith({ kind: 'Group', id: 'home' }, 'h1', 1);
  });

  it('reloads and asks the child to read again when the rule changed meanwhile', async () => {
    const getChildRules = vi
      .fn()
      .mockResolvedValueOnce(rules())
      .mockResolvedValueOnce(
        rules({
          households: [
            {
              scopeKind: 'Group',
              scopeId: 'home',
              label: 'Holm family',
              rules: [rule('h1', 'Dinner', { body: 'New text', revision: 2 })],
            },
          ],
        }),
      );
    const { fixture, compiled } = await setup({
      getChildRules,
      acknowledge: vi.fn(async () => Promise.reject(new HttpErrorResponse({ status: 409 }))),
    });

    readButton(card(compiled, 'h1'))!.click();
    await settle(fixture);

    expect(getChildRules).toHaveBeenCalledTimes(2);
    expect(card(compiled, 'h1').textContent).toContain('New text');
    expect(card(compiled, 'h1').querySelector('[role="status"]')?.textContent).toContain(
      'This rule just changed.',
    );
    expect(readButton(card(compiled, 'h1'))).toBeDefined();
  });

  it('shows an error when acknowledging fails for another reason', async () => {
    const { fixture, compiled } = await setup({
      acknowledge: vi.fn(async () => Promise.reject(new HttpErrorResponse({ status: 500 }))),
    });

    readButton(card(compiled, 'h1'))!.click();
    await settle(fixture);

    expect(card(compiled, 'h1').querySelector('[role="alert"]')?.textContent?.trim()).toBe(
      'That didn’t work. Try again.',
    );
  });

  it('says there are no rules yet', async () => {
    const { compiled } = await setup({
      getChildRules: vi.fn(async () =>
        rules({
          personal: { scopeKind: 'Child', scopeId: 'emil', label: 'Emil', rules: [] },
          households: [],
          pendingAcknowledgements: 0,
        }),
      ),
    });

    expect(compiled.textContent).toContain('There are no rules yet.');
  });

  it('shows a load error', async () => {
    const { compiled } = await setup({
      getChildRules: vi.fn(async () => Promise.reject(new Error('boom'))),
    });

    expect(compiled.textContent).toContain('Unable to load your rules.');
  });
});
