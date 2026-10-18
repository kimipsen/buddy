import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { GroupDetail, GroupSummary, GroupsService } from '../../../core/groups.service';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { HouseRulesService, Rule, RuleBook } from '../../../core/house-rules.service';
import { GuardianHouseRules } from './house-rules';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function child(id: string, givenName: string): ChildSummary {
  return {
    id,
    name: { givenName, familyName: 'Holm' },
    guardianLinkId: `link-${id}`,
    kind: 'Parent',
    language: 'en',
    timeZoneId: 'Europe/Copenhagen',
  } as ChildSummary;
}

function rule(id: string, title: string, overrides: Partial<Rule> = {}): Rule {
  return {
    id,
    title,
    body: '- **Phones** in the basket',
    revision: 1,
    acknowledgementRevision: 1,
    lastEditedAt: '2026-10-01T10:00:00Z',
    acknowledgements: [
      { childId: 'emil', acknowledgedRevision: 1, isUpToDate: true },
      { childId: 'ida', acknowledgedRevision: null, isUpToDate: false },
      { childId: 'noah', acknowledgedRevision: 1, isUpToDate: false },
    ],
    isPublished: true,
    draft: null,
    ...overrides,
  };
}

// A rule the children have never seen: no published text, only a draft.
function draftRule(id: string, title: string): Rule {
  return rule(id, '', {
    body: '',
    revision: 0,
    acknowledgementRevision: 0,
    acknowledgements: [],
    isPublished: false,
    draft: {
      title,
      body: 'Ask two days before',
      requiresReacknowledgement: true,
      editedAt: '2026-10-02T10:00:00Z',
    },
  });
}

// A published rule with an unpublished change.
function changedRule(id: string, title: string): Rule {
  return rule(id, title, {
    draft: {
      title,
      body: '- **Tablets** in the basket too',
      requiresReacknowledgement: true,
      editedAt: '2026-10-02T10:00:00Z',
    },
  });
}

function book(overrides: Partial<RuleBook> = {}): RuleBook {
  return {
    scopeKind: 'Group',
    scopeId: 'home',
    access: 'Manage',
    children: ['emil', 'ida', 'noah'],
    rules: [rule('r1', 'Dinner'), rule('r2', 'Bedtime')],
    ...overrides,
  };
}

const GROUPS: GroupSummary[] = [{ id: 'home', name: 'Holm family', role: 'Owner' }];

describe('GuardianHouseRules', () => {
  async function setup(
    options: {
      groups?: GroupSummary[];
      children?: ChildSummary[];
      houseRules?: Partial<HouseRulesService>;
    } = {},
  ) {
    const guardians: Partial<GuardiansService> = {
      listMyChildren: vi.fn(
        async () => options.children ?? [child('emil', 'Emil'), child('ida', 'Ida')],
      ),
    };
    const groups: Partial<GroupsService> = {
      listMyGroups: vi.fn(async () => options.groups ?? GROUPS),
      getGroup: vi.fn(
        async () =>
          ({
            members: [
              {
                userId: 'emil',
                givenName: 'Emil',
                familyName: 'Holm',
                role: 'Member',
                isChild: true,
              },
              {
                userId: 'ida',
                givenName: 'Ida',
                familyName: 'Holm',
                role: 'Member',
                isChild: true,
              },
              {
                userId: 'noah',
                givenName: 'Noah',
                familyName: 'Berg',
                role: 'Member',
                isChild: true,
              },
            ],
          }) as unknown as GroupDetail,
      ),
    };
    const houseRules: Partial<HouseRulesService> = {
      listRules: vi.fn(async () => book()),
      addRule: vi.fn(async () =>
        book({ rules: [rule('r1', 'Dinner'), rule('r2', 'Bedtime'), rule('r3', 'Shoes')] }),
      ),
      editRule: vi.fn(async () => book({ rules: [rule('r1', 'Dinner!'), rule('r2', 'Bedtime')] })),
      removeRule: vi.fn(async () => undefined),
      reorderRules: vi.fn(async () =>
        book({ rules: [rule('r2', 'Bedtime'), rule('r1', 'Dinner')] }),
      ),
      acknowledge: vi.fn(async () => undefined),
      publishRule: vi.fn(async () => book()),
      publishAll: vi.fn(async () => book()),
      discardDraft: vi.fn(async () => undefined),
      ...options.houseRules,
    };

    await TestBed.configureTestingModule({
      imports: [GuardianHouseRules],
      providers: [
        provideRouter([]),
        { provide: GuardiansService, useValue: guardians },
        { provide: GroupsService, useValue: groups },
        { provide: HouseRulesService, useValue: houseRules },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianHouseRules);
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, houseRules };
  }

  function ruleCard(compiled: HTMLElement, id: string): HTMLElement {
    return compiled.querySelector(`[data-rule-id="${id}"]`)!;
  }

  function button(root: HTMLElement, text: string): HTMLButtonElement {
    return [...root.querySelectorAll('button')].find((b) => b.textContent?.trim() === text)!;
  }

  it('opens the first household and renders its rules as markdown', async () => {
    const { compiled, houseRules } = await setup();

    expect(houseRules.listRules).toHaveBeenCalledWith({ kind: 'Group', id: 'home' });
    expect(
      [...compiled.querySelectorAll('[data-rule-id] h3')].map((h) => h.textContent?.trim()),
    ).toEqual(['Dinner', 'Bedtime']);
    expect(ruleCard(compiled, 'r1').querySelector('app-markdown-view strong')?.textContent).toBe(
      'Phones',
    );
  });

  it('lists households and children as separate groups in the scope picker', async () => {
    const { compiled } = await setup();

    const groups = [...compiled.querySelectorAll('optgroup')];
    expect(groups.map((g) => g.label)).toEqual(['Households', 'Children']);
    expect([...groups[1].querySelectorAll('option')].map((o) => o.textContent?.trim())).toEqual([
      'Emil',
      'Ida',
    ]);
  });

  it('switches to a child’s personal rules', async () => {
    const { fixture, compiled, houseRules } = await setup();

    const select = compiled.querySelector<HTMLSelectElement>('#houseRulesScope')!;
    select.value = 'Child:ida';
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(houseRules.listRules).toHaveBeenLastCalledWith({ kind: 'Child', id: 'ida' });
  });

  it('shows each child’s status by name', async () => {
    const { compiled } = await setup();

    const chips = [...ruleCard(compiled, 'r1').querySelectorAll('[data-status]')];
    expect(chips.map((c) => [c.getAttribute('data-status'), c.textContent?.trim()])).toEqual([
      ['upToDate', 'Emil has read it'],
      ['new', 'Ida hasn’t read it yet'],
      ['changed', 'Noah hasn’t read the change yet'],
    ]);
  });

  it('offers "read it with" only for the guardian’s own children who haven’t read it', async () => {
    const { fixture, compiled, houseRules } = await setup();

    const card = ruleCard(compiled, 'r1');
    expect(button(card, 'Read it with Emil')).toBeUndefined();
    expect(button(card, 'Read it with Noah')).toBeUndefined();

    button(card, 'Read it with Ida').click();
    await settle(fixture);

    expect(houseRules.acknowledge).toHaveBeenCalledWith(
      { kind: 'Group', id: 'home' },
      'r1',
      1,
      'ida',
    );
    expect(houseRules.listRules).toHaveBeenCalledTimes(2);
  });

  it('adds a rule through the editor', async () => {
    const { fixture, compiled, houseRules } = await setup();

    button(compiled, 'Add rule').click();
    await settle(fixture);

    const dialog = compiled.querySelector<HTMLElement>('[role="dialog"]')!;
    expect(dialog.textContent).toContain('New rule');
    const title = dialog.querySelector<HTMLInputElement>('#rule-editor-title-input')!;
    title.value = '  Shoes  ';
    title.dispatchEvent(new Event('input'));
    const body = dialog.querySelector<HTMLTextAreaElement>('#rule-editor-body')!;
    body.value = 'Off at the door';
    body.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    button(dialog, 'Save draft').click();
    await settle(fixture);

    expect(houseRules.addRule).toHaveBeenCalledWith(
      { kind: 'Group', id: 'home' },
      { title: 'Shoes', body: 'Off at the door' },
    );
    expect(compiled.querySelector('[role="dialog"]')).toBeNull();
    expect(compiled.querySelectorAll('[data-rule-id]')).toHaveLength(3);
  });

  it('edits a rule as a small fix that keeps the acknowledgements', async () => {
    const { fixture, compiled, houseRules } = await setup();

    button(ruleCard(compiled, 'r1'), 'Edit').click();
    await settle(fixture);

    const dialog = compiled.querySelector<HTMLElement>('[role="dialog"]')!;
    expect(dialog.querySelector<HTMLInputElement>('#rule-editor-title-input')!.value).toBe(
      'Dinner',
    );
    dialog.querySelector<HTMLButtonElement>('button[role="switch"]')!.click();
    fixture.detectChanges();
    button(dialog, 'Save draft').click();
    await settle(fixture);

    expect(houseRules.editRule).toHaveBeenCalledWith(
      { kind: 'Group', id: 'home' },
      'r1',
      { title: 'Dinner', body: '- **Phones** in the basket' },
      false,
    );
    expect(ruleCard(compiled, 'r1').querySelector('h3')?.textContent?.trim()).toBe('Dinner!');
  });

  it('keeps the editor open and shows the error when saving fails', async () => {
    const { fixture, compiled } = await setup({
      houseRules: { editRule: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });

    button(ruleCard(compiled, 'r1'), 'Edit').click();
    await settle(fixture);
    button(compiled.querySelector<HTMLElement>('[role="dialog"]')!, 'Save draft').click();
    await settle(fixture);

    const dialog = compiled.querySelector<HTMLElement>('[role="dialog"]')!;
    expect(dialog.querySelector('[role="alert"]')?.textContent).toContain(
      'Unable to save the rule.',
    );
  });

  it('removes a rule after confirming', async () => {
    const { fixture, compiled, houseRules } = await setup();

    button(ruleCard(compiled, 'r1'), 'Remove').click();
    await settle(fixture);
    expect(ruleCard(compiled, 'r1').textContent).toContain('Remove “Dinner”?');

    button(ruleCard(compiled, 'r1'), 'Yes, remove').click();
    await settle(fixture);

    expect(houseRules.removeRule).toHaveBeenCalledWith({ kind: 'Group', id: 'home' }, 'r1');
    expect(compiled.querySelector('[data-rule-id="r1"]')).toBeNull();
  });

  it('moves a rule down with the full new order', async () => {
    const { fixture, compiled, houseRules } = await setup();

    const down = ruleCard(compiled, 'r1').querySelector<HTMLButtonElement>(
      '[aria-label="Move “Dinner” down"]',
    )!;
    const up = ruleCard(compiled, 'r1').querySelector<HTMLButtonElement>(
      '[aria-label="Move “Dinner” up"]',
    )!;
    expect(up.disabled).toBe(true);

    down.click();
    await settle(fixture);

    expect(houseRules.reorderRules).toHaveBeenCalledWith({ kind: 'Group', id: 'home' }, [
      'r2',
      'r1',
    ]);
    expect(
      [...compiled.querySelectorAll('[data-rule-id] h3')].map((h) => h.textContent?.trim()),
    ).toEqual(['Bedtime', 'Dinner']);
  });

  it('shows a read-only book without editing controls', async () => {
    const { compiled } = await setup({
      houseRules: { listRules: vi.fn(async () => book({ access: 'View' })) },
    });

    expect(compiled.textContent).toContain('Only the group’s owner and admins can change them.');
    expect(button(compiled, 'Add rule')).toBeUndefined();
    expect(button(ruleCard(compiled, 'r1'), 'Edit')).toBeUndefined();
    expect(button(ruleCard(compiled, 'r1'), 'Read it with Ida')).toBeUndefined();
  });

  it('links the print button to the household printout', async () => {
    const { compiled } = await setup();

    const print = [...compiled.querySelectorAll('a')].find(
      (a) => a.textContent?.trim() === 'Print',
    );
    expect(print?.getAttribute('href')).toBe('/guardian/house-rules/print/groups/home');
  });

  it('shows an empty book', async () => {
    const { compiled } = await setup({
      houseRules: { listRules: vi.fn(async () => book({ rules: [] })) },
    });

    expect(compiled.textContent).toContain('No rules here yet.');
  });

  it('asks for a group or a child when there is neither', async () => {
    const { compiled, houseRules } = await setup({ groups: [], children: [] });

    expect(compiled.textContent).toContain('Create a group or link a child');
    expect(houseRules.listRules).not.toHaveBeenCalled();
  });

  it('shows a load error', async () => {
    const { compiled } = await setup({
      houseRules: { listRules: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });

    expect(compiled.textContent).toContain('Unable to load the house rules.');
  });

  it('shows a failed write under its rule', async () => {
    const { fixture, compiled } = await setup({
      houseRules: { removeRule: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });

    button(ruleCard(compiled, 'r1'), 'Remove').click();
    await settle(fixture);
    button(ruleCard(compiled, 'r1'), 'Yes, remove').click();
    await settle(fixture);

    expect(ruleCard(compiled, 'r1').querySelector('[role="alert"]')?.textContent?.trim()).toBe(
      'Unable to remove the rule.',
    );
  });

  it('links a child’s print button to the child printout', async () => {
    const { compiled } = await setup({ groups: [] });

    const print = [...compiled.querySelectorAll('a')].find(
      (a) => a.textContent?.trim() === 'Print',
    );
    expect(print?.getAttribute('href')).toBe('/guardian/house-rules/print/children/emil');
  });

  it('names a child it can’t look up generically', async () => {
    const { compiled } = await setup({ children: [child('emil', 'Emil')], groups: [] });

    // Ida and Noah are in the book but neither the guardian's children nor (child scope) members.
    expect(ruleCard(compiled, 'r1').textContent).toContain('A child hasn’t read it yet');
  });

  it('marks a draft and a rule with unpublished changes, and offers to publish all', async () => {
    const { compiled } = await setup({
      houseRules: {
        listRules: vi.fn(async () =>
          book({
            rules: [
              rule('r1', 'Dinner'),
              changedRule('r2', 'Bedtime'),
              draftRule('r3', 'Sleepovers'),
            ],
          }),
        ),
      },
    });

    expect(
      [...compiled.querySelectorAll('[data-rule-id]')].map((c) =>
        c.getAttribute('data-publish-state'),
      ),
    ).toEqual(['published', 'changed', 'draft']);
    expect(ruleCard(compiled, 'r3').querySelector('h3')?.textContent?.trim()).toBe('Sleepovers');
    expect(ruleCard(compiled, 'r3').textContent).toContain(
      'Draft: the children can’t see this yet',
    );
    expect(ruleCard(compiled, 'r3').querySelector('[data-status]')).toBeNull();
    expect(button(ruleCard(compiled, 'r3'), 'Discard changes')).toBeUndefined();

    const changed = ruleCard(compiled, 'r2');
    expect(changed.textContent).toContain('Unpublished changes');
    expect(changed.querySelector('app-markdown-view strong')?.textContent).toBe('Tablets');
    expect(changed.querySelector('details')?.textContent).toContain('What the children see now');
    expect(changed.querySelector('details app-markdown-view strong')?.textContent).toBe('Phones');

    expect(button(ruleCard(compiled, 'r1'), 'Publish')).toBeUndefined();
    expect(button(compiled, 'Publish all (2)')).toBeDefined();
  });

  it('publishes one rule', async () => {
    const { fixture, compiled, houseRules } = await setup({
      houseRules: {
        listRules: vi.fn(async () =>
          book({ rules: [rule('r1', 'Dinner'), draftRule('r2', 'Sleepovers')] }),
        ),
        publishRule: vi.fn(async () =>
          book({ rules: [rule('r1', 'Dinner'), rule('r2', 'Sleepovers')] }),
        ),
      },
    });

    ruleCard(compiled, 'r2')
      .querySelector<HTMLButtonElement>('[aria-label="Publish “Sleepovers”"]')!
      .click();
    await settle(fixture);

    expect(houseRules.publishRule).toHaveBeenCalledWith({ kind: 'Group', id: 'home' }, 'r2');
    expect(ruleCard(compiled, 'r2').getAttribute('data-publish-state')).toBe('published');
    expect(button(compiled, 'Publish all (1)')).toBeUndefined();
  });

  it('publishes every draft at once', async () => {
    const { fixture, compiled, houseRules } = await setup({
      houseRules: {
        listRules: vi.fn(async () =>
          book({ rules: [changedRule('r1', 'Dinner'), draftRule('r2', 'Sleepovers')] }),
        ),
      },
    });

    button(compiled, 'Publish all (2)').click();
    await settle(fixture);

    expect(houseRules.publishAll).toHaveBeenCalledWith({ kind: 'Group', id: 'home' });
    expect(compiled.querySelectorAll('[data-publish-state="published"]')).toHaveLength(2);
  });

  it('shows a failed publish-all above the rules', async () => {
    const { fixture, compiled } = await setup({
      houseRules: {
        listRules: vi.fn(async () => book({ rules: [draftRule('r1', 'Sleepovers')] })),
        publishAll: vi.fn(async () => Promise.reject(new Error('boom'))),
      },
    });

    button(compiled, 'Publish all (1)').click();
    await settle(fixture);

    expect(compiled.querySelector('[role="alert"]')?.textContent?.trim()).toBe(
      'Unable to publish the rule.',
    );
  });

  it('discards a published rule’s changes', async () => {
    const { fixture, compiled, houseRules } = await setup({
      houseRules: { listRules: vi.fn(async () => book({ rules: [changedRule('r1', 'Dinner')] })) },
    });

    button(ruleCard(compiled, 'r1'), 'Discard changes').click();
    await settle(fixture);

    expect(houseRules.discardDraft).toHaveBeenCalledWith({ kind: 'Group', id: 'home' }, 'r1');
    expect(ruleCard(compiled, 'r1').getAttribute('data-publish-state')).toBe('published');
    expect(ruleCard(compiled, 'r1').querySelector('app-markdown-view strong')?.textContent).toBe(
      'Phones',
    );
  });

  it('saves a new rule and publishes it from the editor', async () => {
    const { fixture, compiled, houseRules } = await setup();

    button(compiled, 'Add rule').click();
    await settle(fixture);
    const dialog = compiled.querySelector<HTMLElement>('[role="dialog"]')!;
    const title = dialog.querySelector<HTMLInputElement>('#rule-editor-title-input')!;
    title.value = 'Shoes';
    title.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    button(dialog, 'Save and publish').click();
    await settle(fixture);

    expect(houseRules.addRule).toHaveBeenCalled();
    expect(houseRules.publishRule).toHaveBeenCalledWith({ kind: 'Group', id: 'home' }, 'r3');
    expect(compiled.querySelector('[role="dialog"]')).toBeNull();
  });

  it('shows a failed publish after saving under the rule, with the editor closed', async () => {
    const { fixture, compiled, houseRules } = await setup({
      houseRules: { publishRule: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });

    button(ruleCard(compiled, 'r1'), 'Edit').click();
    await settle(fixture);
    button(compiled.querySelector<HTMLElement>('[role="dialog"]')!, 'Save and publish').click();
    await settle(fixture);

    expect(houseRules.editRule).toHaveBeenCalled();
    expect(compiled.querySelector('[role="dialog"]')).toBeNull();
    expect(ruleCard(compiled, 'r1').querySelector('[role="alert"]')?.textContent?.trim()).toBe(
      'Unable to publish the rule.',
    );
  });

  it('gives focus back to the button that opened the editor', async () => {
    const { fixture, compiled } = await setup();
    const add = button(compiled, 'Add rule');

    add.click();
    await settle(fixture);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    await settle(fixture);

    expect(compiled.querySelector('[role="dialog"]')).toBeNull();
    expect(document.activeElement).toBe(add);
  });
});
