import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { GroupDetail, GroupsService } from '../../../../core/groups.service';
import { ChildRule, HouseRulesService } from '../../../../core/house-rules.service';
import { HouseRulesPrintPage, PrintScope } from './house-rules-print-page';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function childRule(id: string, title: string, body = ''): ChildRule {
  return {
    id,
    title,
    body,
    revision: 1,
    acknowledgedRevision: null,
    isUpToDate: false,
    lastEditedAt: '2026-10-01T10:00:00Z',
  };
}

describe('HouseRulesPrintPage', () => {
  async function setup(scope: PrintScope, houseRules: Partial<HouseRulesService> = {}) {
    const stub: Partial<HouseRulesService> = {
      getChildRules: vi.fn(async () => ({
        childId: 'emil',
        personal: {
          scopeKind: 'Child' as const,
          scopeId: 'emil',
          label: 'Emil',
          rules: [childRule('p1', 'Gaming', '| Day | Time |\n|---|---|\n| Mon | 45 min |')],
        },
        households: [
          { scopeKind: 'Group' as const, scopeId: 'dad', label: 'Dad’s', rules: [] },
          {
            scopeKind: 'Group' as const,
            scopeId: 'mum',
            label: 'Mum’s',
            rules: [childRule('h1', 'Shoes off'), childRule('h2', 'Dinner')],
          },
        ],
        pendingAcknowledgements: 3,
      })),
      listRules: vi.fn(async () => ({
        scopeKind: 'Group' as const,
        scopeId: 'mum',
        access: 'View' as const,
        children: [],
        rules: [
          {
            id: 'h1',
            title: 'Shoes off',
            body: '',
            revision: 1,
            acknowledgementRevision: 1,
            lastEditedAt: '2026-10-01T10:00:00Z',
            acknowledgements: [],
          },
        ],
      })),
      ...houseRules,
    };
    const groups: Partial<GroupsService> = {
      getGroup: vi.fn(async () => ({ name: 'Mum’s' }) as GroupDetail),
    };

    await TestBed.configureTestingModule({
      imports: [HouseRulesPrintPage],
      providers: [
        provideRouter([]),
        { provide: HouseRulesService, useValue: stub },
        { provide: GroupsService, useValue: groups },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              data: { printScope: scope },
              paramMap: convertToParamMap(
                scope === 'child' ? { childId: 'emil' } : { groupId: 'mum' },
              ),
            },
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(HouseRulesPrintPage);
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, houseRules: stub, groups };
  }

  it('prints a child’s personal rules first, then each household', async () => {
    const { compiled, houseRules } = await setup('child');

    expect(houseRules.getChildRules).toHaveBeenCalledWith('emil');
    expect(compiled.querySelector('h1')?.textContent?.trim()).toBe('Emil’s rules');
    expect([...compiled.querySelectorAll('h2')].map((h) => h.textContent?.trim())).toEqual([
      'Just for Emil',
      'Mum’s',
    ]);
    expect([...compiled.querySelectorAll('h3')].map((h) => h.textContent?.trim())).toEqual([
      '1. Gaming',
      '1. Shoes off',
      '2. Dinner',
    ]);
    expect(compiled.querySelector('app-markdown-view table')).not.toBeNull();
    expect(compiled.textContent).not.toContain('Dad’s');
    expect(compiled.textContent).not.toContain('No rules yet.');
  });

  it('says so once when a child has no rules anywhere', async () => {
    const { compiled } = await setup('child', {
      getChildRules: vi.fn(async () => ({
        childId: 'emil',
        personal: { scopeKind: 'Child' as const, scopeId: 'emil', label: 'Emil', rules: [] },
        households: [{ scopeKind: 'Group' as const, scopeId: 'dad', label: 'Dad’s', rules: [] }],
        pendingAcknowledgements: 0,
      })),
    });

    expect(compiled.querySelector('h1')?.textContent?.trim()).toBe('Emil’s rules');
    expect(compiled.querySelectorAll('h2')).toHaveLength(0);
    expect(compiled.textContent?.match(/No rules yet\./g)).toHaveLength(1);
  });

  it('says so when a household has no rules', async () => {
    const { compiled } = await setup('group', {
      listRules: vi.fn(async () => ({
        scopeKind: 'Group' as const,
        scopeId: 'mum',
        access: 'View' as const,
        children: [],
        rules: [],
      })),
    });

    expect(compiled.querySelector('h1')?.textContent?.trim()).toBe('Mum’s');
    expect(compiled.textContent).toContain('No rules yet.');
  });

  it('prints one household under its name', async () => {
    const { compiled, houseRules, groups } = await setup('group');

    expect(houseRules.listRules).toHaveBeenCalledWith({ kind: 'Group', id: 'mum' });
    expect(groups.getGroup).toHaveBeenCalledWith('mum');
    expect(compiled.querySelector('h1')?.textContent?.trim()).toBe('Mum’s');
    expect(compiled.querySelector('h2')).toBeNull();
    expect(compiled.querySelector('h3')?.textContent?.trim()).toBe('1. Shoes off');
  });

  it('keeps the toolbar off paper and prints with the browser', async () => {
    const { compiled } = await setup('group');
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);

    const toolbar = compiled.querySelector('a[href="/guardian/house-rules"]')!.parentElement!;
    expect(toolbar.classList).toContain('print:hidden');
    [...toolbar.querySelectorAll('button')].find((b) => b.textContent?.trim() === 'Print')!.click();

    expect(print).toHaveBeenCalled();
    print.mockRestore();
  });

  it('shows a load error', async () => {
    const { compiled } = await setup('child', {
      getChildRules: vi.fn(async () => Promise.reject(new Error('boom'))),
    });

    expect(compiled.textContent).toContain('Unable to load the rules to print.');
  });
});
