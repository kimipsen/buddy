import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { MealieConnectionView, MealieService } from '../../../../core/mealie.service';
import {
  MealPlanImportPreview,
  MealPlanImportPreviewGroup,
  MealPlanImportPreviewLine,
  MealPlanImportResult,
  MealPlanImportSummary,
  MealplansService,
} from '../../../../core/mealplans.service';
import { MealplanImport } from './mealplan-import';
import { disableFeatures } from '../../../../../testing/features-fixture';

describe('MealplanImport', () => {
  const child: ChildSummary = {
    showVisualTimers: true,
    homeworkChunks: { focusMinutes: 15, breakMinutes: 5 },
    id: 'child-1',
    name: { givenName: 'Alex', familyName: 'Doe' },
    guardianLinkId: 'link-1',
    kind: 'Parent',
    language: 'en',
    timeZoneId: 'UTC',
  };

  function line(overrides: Partial<MealPlanImportPreviewLine>): MealPlanImportPreviewLine {
    return {
      lineNumber: 1,
      date: '2024-01-14',
      slot: 'Dinner',
      rawText: '',
      kind: 'Meal',
      mealName: '',
      notes: '',
      key: '',
      occupied: false,
      ...overrides,
    };
  }

  function group(overrides: Partial<MealPlanImportPreviewGroup>): MealPlanImportPreviewGroup {
    return {
      key: '',
      name: '',
      kind: 'Meal',
      count: 1,
      firstDate: '2024-01-14',
      lastDate: '2024-01-14',
      defaultAction: 'New',
      matchedMealId: null,
      matchedMealName: '',
      suggestedMealId: null,
      suggestedGroupKey: '',
      suggestedName: '',
      ...overrides,
    };
  }

  // Lasagne matches an existing meal, "Hotdog" is a spelling variant of the more frequent
  // "Hotdogs", "Rester" is leftovers (skipped by default) and Pizza's day is already planned.
  const preview: MealPlanImportPreview = {
    format: 'weekly-note',
    lines: [
      line({ date: '2024-01-14', key: 'lasagne', mealName: 'Lasagne', notes: 'Mor ikke hjemme' }),
      line({ date: '2024-01-15', key: 'hotdogs', mealName: 'Hotdogs' }),
      line({ date: '2024-01-16', key: 'hotdogs', mealName: 'Hotdogs' }),
      line({ date: '2024-01-17', key: 'hotdog', mealName: 'Hotdog' }),
      line({ date: '2024-01-18', key: 'rester', mealName: 'Rester', kind: 'Leftovers' }),
      line({ date: '2024-01-19', key: 'pizza', mealName: 'Pizza', occupied: true }),
    ],
    groups: [
      group({ key: 'hotdogs', name: 'Hotdogs', count: 2 }),
      group({
        key: 'lasagne',
        name: 'Lasagne',
        defaultAction: 'Existing',
        matchedMealId: 'meal-lasagne',
        matchedMealName: 'Lasagne',
      }),
      group({
        key: 'hotdog',
        name: 'Hotdog',
        suggestedGroupKey: 'hotdogs',
        suggestedName: 'Hotdogs',
      }),
      group({ key: 'rester', name: 'Rester', kind: 'Leftovers', defaultAction: 'Skip' }),
      group({ key: 'pizza', name: 'Pizza' }),
    ],
    warnings: [{ lineNumber: 7, code: 'week_number_corrected', message: '' }],
    emptyDays: 0,
  };

  const result: MealPlanImportResult = {
    importId: 'import-1',
    imported: 4,
    createdMeals: 1,
    archivedMeals: 0,
    skipped: [],
  };

  const summary: MealPlanImportSummary = {
    importId: 'import-0',
    format: 'weekly-note',
    from: '2023-01-01',
    to: '2023-02-01',
    entryCount: 20,
    createdMealCount: 5,
    importedBy: 'guardian-1',
    importedAt: '2026-10-01T00:00:00Z',
    reverted: false,
  };

  async function setup(
    stubs: {
      guardians?: Partial<GuardiansService>;
      mealplans?: Partial<MealplansService>;
      mealie?: Partial<MealieService>;
      source?: string;
    } = {},
  ) {
    const mealieStub: Partial<MealieService> = {
      getConnection: vi.fn(async (): Promise<MealieConnectionView> => ({ kind: 0 })),
      ...stubs.mealie,
    };
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => [child]),
      ...stubs.guardians,
    };
    const mealplansStub: Partial<MealplansService> = {
      listImports: vi.fn(async () => [summary]),
      previewImport: vi.fn(async () => preview),
      commitImport: vi.fn(async () => result),
      revertImport: vi.fn(async () => undefined),
      ...stubs.mealplans,
    };

    await TestBed.configureTestingModule({
      imports: [MealplanImport],
      providers: [
        provideRouter([]),
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: MealplansService, useValue: mealplansStub },
        { provide: MealieService, useValue: mealieStub },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap(stubs.source ? { source: stubs.source } : {}),
            },
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(MealplanImport);
    return { fixture, mealplans: mealplansStub, mealie: mealieStub };
  }

  async function settle(fixture: {
    detectChanges: () => void;
    whenStable: () => Promise<boolean>;
  }) {
    fixture.detectChanges();

    for (let i = 0; i < 10; i++) {
      await fixture.whenStable();
      fixture.detectChanges();
    }
  }

  function button(compiled: HTMLElement, text: string): HTMLButtonElement {
    const found = Array.from(compiled.querySelectorAll('button')).find((b) =>
      b.textContent?.trim().startsWith(text),
    );

    if (!found) {
      throw new Error(`No button "${text}"`);
    }

    return found;
  }

  function type(element: HTMLInputElement | HTMLTextAreaElement, value: string): void {
    element.value = value;
    element.dispatchEvent(new Event('input'));
  }

  function choose(select: HTMLSelectElement, value: string): void {
    select.value = value;
    select.dispatchEvent(new Event('change'));
  }

  async function previewed() {
    const context = await setup();
    await settle(context.fixture);
    const compiled: HTMLElement = context.fixture.nativeElement;

    type(compiled.querySelector('textarea')!, 'Madplan 2024\nU3\nSø: Lasagne');
    await settle(context.fixture);
    button(compiled, 'Preview import').click();
    await settle(context.fixture);

    return { ...context, compiled };
  }

  it('asks for a child first when the guardian has none', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => []) } });
    await settle(fixture);

    expect(fixture.nativeElement.textContent).toContain(
      'Link a child from Settings before importing meal plans.',
    );
  });

  it('sends the pasted text with the default options to preview', async () => {
    const { mealplans, compiled } = await previewed();

    expect(mealplans.previewImport).toHaveBeenCalledWith(
      { kind: 'family', childId: 'child-1' },
      {
        text: 'Madplan 2024\nU3\nSø: Lasagne',
        format: 'auto',
        weekStart: 'Sunday',
        slot: 'Dinner',
      },
    );
    expect(compiled.textContent).toContain('6 days from 2024-01-14 to 2024-01-19.');
    expect(compiled.textContent).toContain(
      'Line 7: the week number looked wrong and was corrected.',
    );
    expect(compiled.querySelectorAll('[data-testid="import-group"]').length).toBe(5);
  });

  it('counts what the default decisions import: matched, new, skipped leftovers, kept planned days', async () => {
    const { compiled } = await previewed();

    expect(compiled.querySelector('[data-testid="count-importing"]')?.textContent?.trim()).toBe(
      '4',
    );
    // Hotdogs and Hotdog are separate new meals until the guardian merges them.
    expect(compiled.querySelector('[data-testid="count-new-meals"]')?.textContent?.trim()).toBe(
      '2',
    );
    expect(compiled.textContent).toContain('Did you mean “Hotdogs”?');
  });

  it('commits the reviewed entries, following a merge into the suggested group', async () => {
    const { fixture, mealplans, compiled } = await previewed();

    choose(compiled.querySelector('#import-action-2') as HTMLSelectElement, 'merge:hotdogs');
    await settle(fixture);
    button(compiled, 'Import 4 days').click();
    await settle(fixture);

    expect(mealplans.commitImport).toHaveBeenCalledWith(
      { kind: 'family', childId: 'child-1' },
      {
        format: 'weekly-note',
        archiveSingleUse: true,
        entries: [
          { date: '2024-01-14', slot: 'Dinner', mealId: 'meal-lasagne', notes: 'Mor ikke hjemme' },
          { date: '2024-01-15', slot: 'Dinner', newMealName: 'Hotdogs', notes: '' },
          { date: '2024-01-16', slot: 'Dinner', newMealName: 'Hotdogs', notes: '' },
          { date: '2024-01-17', slot: 'Dinner', newMealName: 'Hotdogs', notes: '' },
        ],
      },
    );
    expect(compiled.textContent).toContain('Import complete');
    expect(compiled.textContent).toContain('4 days added to the meal plan.');
  });

  it('imports all leftovers as one meal, and skips a group the guardian skips', async () => {
    const { fixture, mealplans, compiled } = await previewed();

    button(compiled, 'Import leftovers as one meal').click();
    choose(compiled.querySelector('#import-action-1') as HTMLSelectElement, 'skip');
    await settle(fixture);
    compiled.querySelector<HTMLButtonElement>('[role="switch"]')!.click();
    await settle(fixture);
    button(compiled, 'Import 4 days').click();
    await settle(fixture);

    const request = vi.mocked(mealplans.commitImport!).mock.calls[0][1];
    expect(request.archiveSingleUse).toBe(false);
    expect(request.entries.map((e) => e.date)).toEqual([
      '2024-01-15',
      '2024-01-16',
      '2024-01-17',
      '2024-01-18',
    ]);
    expect(request.entries[3]).toEqual({
      date: '2024-01-18',
      slot: 'Dinner',
      newMealName: 'Leftovers',
      notes: '',
    });
  });

  it('renames a new meal before importing', async () => {
    const { fixture, mealplans, compiled } = await previewed();

    type(compiled.querySelector('#import-name-0') as HTMLInputElement, 'Hot dogs');
    await settle(fixture);
    button(compiled, 'Import 4 days').click();
    await settle(fixture);

    const request = vi.mocked(mealplans.commitImport!).mock.calls[0][1];
    expect(request.entries.filter((e) => e.newMealName === 'Hot dogs').length).toBe(2);
  });

  it('falls back to the parsed name when a new meal name is cleared', async () => {
    const { fixture, mealplans, compiled } = await previewed();

    type(compiled.querySelector('#import-name-0') as HTMLInputElement, '   ');
    await settle(fixture);
    button(compiled, 'Import 4 days').click();
    await settle(fixture);

    const request = vi.mocked(mealplans.commitImport!).mock.calls[0][1];
    expect(request.entries.filter((e) => e.newMealName === 'Hotdogs').length).toBe(2);
  });

  it('counts new meals the way the backend groups them', async () => {
    const { fixture, compiled } = await previewed();

    type(compiled.querySelector('#import-name-2') as HTMLInputElement, 'hotdogs.');
    await settle(fixture);

    expect(compiled.querySelector('[data-testid="count-new-meals"]')?.textContent?.trim()).toBe(
      '1',
    );
  });

  it('shows the backend reason when the text cannot be read', async () => {
    const { fixture, compiled } = await (async () => {
      const context = await setup({
        mealplans: {
          previewImport: vi.fn(async () =>
            Promise.reject(
              new HttpErrorResponse({
                status: 400,
                error: { code: 'validation_error', details: { '': ['Add a year line.'] } },
              }),
            ),
          ),
        },
      });
      await settle(context.fixture);
      return { ...context, compiled: context.fixture.nativeElement as HTMLElement };
    })();

    type(compiled.querySelector('textarea')!, 'U2\nMa: Burger');
    await settle(fixture);
    button(compiled, 'Preview import').click();
    await settle(fixture);

    expect(compiled.textContent).toContain('The text could not be read as a meal plan.');
    expect(compiled.textContent).toContain('Add a year line.');
  });

  it('lists earlier imports and undoes one after confirming', async () => {
    const { fixture, mealplans } = await setup();
    await settle(fixture);
    const compiled: HTMLElement = fixture.nativeElement;

    expect(compiled.textContent).toContain('2023-01-01 – 2023-02-01 · 20 days');

    button(compiled, 'Undo').click();
    await settle(fixture);
    expect(mealplans.revertImport).not.toHaveBeenCalled();

    button(compiled, 'Undo import').click();
    await settle(fixture);

    expect(mealplans.revertImport).toHaveBeenCalledWith(
      { kind: 'family', childId: 'child-1' },
      'import-0',
    );
    expect(mealplans.listImports).toHaveBeenCalledTimes(2);
  });

  describe('from Mealie', () => {
    const connected: MealieConnectionView = {
      kind: 1,
      baseUrl: 'https://mealie.holm.example',
      householdName: 'Holm',
      mealieUserName: 'buddy-sync',
      mealieVersion: 'v3.29.0',
      tokenLast4: 'b7f2',
      connectedBy: 'guardian-1',
      connectedAt: '2026-10-19T10:00:00Z',
    };
    const lasagneRecipe = { recipeId: 'recipe-1', slug: 'lasagne', name: 'Lasagne al forno' };
    const tacosRecipe = { recipeId: 'recipe-2', slug: 'tacos', name: 'Chicken tacos' };

    // "Tacos" is new and came from a recipe, "Lasagne" matches an existing meal (its recipe isn't
    // sent), "Toast" is new without a recipe.
    const mealiePreview: MealPlanImportPreview = {
      format: 'mealie',
      lines: [
        line({ lineNumber: 1, date: '2026-10-26', key: 'lasagne', mealName: 'Lasagne' }),
        line({
          lineNumber: 2,
          date: '2026-10-27',
          key: 'chicken tacos',
          mealName: 'Chicken tacos',
          notes: '+ side: Salad',
        }),
        line({ lineNumber: 3, date: '2026-10-28', key: 'toast', mealName: 'Toast' }),
      ],
      groups: [
        group({
          key: 'lasagne',
          name: 'Lasagne',
          defaultAction: 'Existing',
          matchedMealId: 'meal-lasagne',
          matchedMealName: 'Lasagne',
        }),
        group({ key: 'chicken tacos', name: 'Chicken tacos' }),
        group({ key: 'toast', name: 'Toast' }),
      ],
      warnings: [{ lineNumber: 2, code: 'mealie_entry_merged', message: '' }],
      emptyDays: 0,
    };

    async function mealieSetup(source?: string, previewImport?: MealieService['previewImport']) {
      const setupResult = await setup({
        source,
        mealie: {
          getConnection: vi.fn(async () => connected),
          previewImport:
            previewImport ??
            vi.fn(async () => ({
              preview: mealiePreview,
              recipes: [
                { key: 'lasagne', recipe: lasagneRecipe },
                { key: 'chicken tacos', recipe: tacosRecipe },
              ],
            })),
        },
      });
      await settle(setupResult.fixture);
      return { ...setupResult, compiled: setupResult.fixture.nativeElement as HTMLElement };
    }

    it('offers no source choice while the family isn’t connected, even when asked for Mealie', async () => {
      const { fixture } = await setup({ source: 'mealie' });
      await settle(fixture);
      const compiled: HTMLElement = fixture.nativeElement;

      expect(compiled.querySelector('[role="radiogroup"]')).toBeNull();
      expect(compiled.querySelector('textarea')).not.toBeNull();
    });

    it('starts on the text source unless the link asks for Mealie', async () => {
      const { compiled } = await mealieSetup();

      expect(compiled.querySelector('[role="radiogroup"]')?.textContent).toContain('Mealie');
      expect(compiled.querySelector('textarea')).not.toBeNull();
      expect(compiled.querySelector('#mealieImportFrom')).toBeNull();
    });

    it('defaults to next week, Monday to Sunday', async () => {
      const { compiled } = await mealieSetup('mealie');

      const from = compiled.querySelector<HTMLInputElement>('#mealieImportFrom')!.value;
      const to = compiled.querySelector<HTMLInputElement>('#mealieImportTo')!.value;
      const fromDate = new Date(`${from}T00:00:00`);
      const toDate = new Date(`${to}T00:00:00`);
      const today = new Date();
      today.setHours(0, 0, 0, 0);

      expect(fromDate.getDay()).toBe(1);
      expect(fromDate > today).toBe(true);
      expect((fromDate.getTime() - today.getTime()) / 86_400_000).toBeLessThanOrEqual(7);
      expect(Math.round((toDate.getTime() - fromDate.getTime()) / 86_400_000)).toBe(6);
      expect(compiled.querySelector('textarea')).toBeNull();
    });

    it('switches between the sources', async () => {
      const { fixture, compiled } = await mealieSetup('mealie');

      const textOption = Array.from(
        compiled.querySelectorAll<HTMLButtonElement>('[role="radio"]'),
      ).find((b) => b.textContent?.trim() === 'Text or file')!;
      textOption.click();
      await settle(fixture);

      expect(compiled.querySelector('textarea')).not.toBeNull();
      expect(compiled.querySelector('#mealieImportFrom')).toBeNull();
    });

    it('previews the range and commits new meals linked to their Mealie recipe', async () => {
      const { fixture, compiled, mealie, mealplans } = await mealieSetup('mealie');
      type(compiled.querySelector<HTMLInputElement>('#mealieImportFrom')!, '2026-10-26');
      type(compiled.querySelector<HTMLInputElement>('#mealieImportTo')!, '2026-11-01');
      await settle(fixture);

      button(compiled, 'Preview Mealie plan').click();
      await settle(fixture);

      expect(mealie.previewImport).toHaveBeenCalledWith('child-1', '2026-10-26', '2026-11-01');
      expect(compiled.textContent).toContain(
        'Entry 2: a side dish, drink or dessert was added to that day’s dinner notes.',
      );

      button(compiled, 'Import 3 days').click();
      await settle(fixture);

      expect(mealplans.commitImport).toHaveBeenCalledWith(
        { kind: 'family', childId: 'child-1' },
        {
          format: 'mealie',
          archiveSingleUse: true,
          entries: [
            { date: '2026-10-26', slot: 'Dinner', mealId: 'meal-lasagne', notes: '' },
            {
              date: '2026-10-27',
              slot: 'Dinner',
              newMealName: 'Chicken tacos',
              notes: '+ side: Salad',
              mealieRecipe: tacosRecipe,
            },
            { date: '2026-10-28', slot: 'Dinner', newMealName: 'Toast', notes: '' },
          ],
        },
      );
    });

    it('forgets the Mealie recipes when a text preview follows', async () => {
      const { fixture, compiled, mealplans } = await mealieSetup('mealie');
      button(compiled, 'Preview Mealie plan').click();
      await settle(fixture);
      button(compiled, 'Change dates').click();
      await settle(fixture);

      Array.from(compiled.querySelectorAll<HTMLButtonElement>('[role="radio"]'))
        .find((b) => b.textContent?.trim() === 'Text or file')!
        .click();
      await settle(fixture);
      type(compiled.querySelector('textarea')!, 'Madplan 2024\nU2\nSø: Lasagne');
      await settle(fixture);
      button(compiled, 'Preview import').click();
      await settle(fixture);
      button(compiled, 'Import 4 days').click();
      await settle(fixture);

      const sent = vi.mocked(mealplans.commitImport!).mock.calls[0][1];
      expect(sent.entries.some((entry) => 'mealieRecipe' in entry)).toBe(false);
    });

    it('names every Mealie warning', async () => {
      const preview = {
        ...mealiePreview,
        warnings: [
          { lineNumber: 4, code: 'mealie_entry_skipped', message: '' },
          { lineNumber: 5, code: 'duplicate_slot', message: '' },
          { lineNumber: 0, code: 'mealie_range_empty', message: '' },
        ],
      };
      const { fixture, compiled } = await mealieSetup(
        'mealie',
        vi.fn(async () => ({ preview, recipes: [] })),
      );

      button(compiled, 'Preview Mealie plan').click();
      await settle(fixture);
      compiled.querySelector('details')!.open = true;

      expect(compiled.textContent).toContain('Entry 4: skipped (no recipe or title');
      expect(compiled.textContent).toContain('Entry 5: a second meal in the same slot');
      expect(compiled.textContent).toContain('Mealie has no meal plan entries in this range.');
    });

    it('offers no Mealie source while the installation has Mealie turned off', async () => {
      disableFeatures('mealplanMealie');
      const getConnection = vi.fn(async () => connected);
      const { fixture } = await setup({ source: 'mealie', mealie: { getConnection } });
      await settle(fixture);

      expect(getConnection).not.toHaveBeenCalled();
      expect(
        (fixture.nativeElement as HTMLElement).querySelector('[role="radiogroup"]'),
      ).toBeNull();
    });

    it('shows the backend’s reason for a rejected range, and clears it on the next attempt and on switching source', async () => {
      const previewImport = vi
        .fn()
        .mockRejectedValueOnce(
          new HttpErrorResponse({
            status: 400,
            error: { details: { to: ['A Mealie import covers at most 92 days.'] } },
          }),
        )
        .mockRejectedValueOnce(new HttpErrorResponse({ status: 500 }));
      const { fixture, compiled } = await mealieSetup('mealie', previewImport);

      button(compiled, 'Preview Mealie plan').click();
      await settle(fixture);
      expect(compiled.textContent).toContain('Couldn’t read Mealie’s meal plan.');
      expect(compiled.textContent).toContain('A Mealie import covers at most 92 days.');

      button(compiled, 'Preview Mealie plan').click();
      await settle(fixture);
      expect(compiled.textContent).toContain('Couldn’t read Mealie’s meal plan.');
      expect(compiled.textContent).not.toContain('at most 92 days');

      Array.from(compiled.querySelectorAll<HTMLButtonElement>('[role="radio"]'))
        .find((b) => b.textContent?.trim() === 'Text or file')!
        .click();
      await settle(fixture);
      expect(compiled.textContent).not.toContain('Couldn’t read Mealie’s meal plan.');
    });

    it('shows why Mealie’s plan couldn’t be read', async () => {
      const error = new HttpErrorResponse({
        status: 502,
        error: { code: 'mealie_token_rejected' },
      });
      const { fixture, compiled } = await mealieSetup(
        'mealie',
        vi.fn(async () => Promise.reject(error)),
      );

      button(compiled, 'Preview Mealie plan').click();
      await settle(fixture);

      expect(compiled.textContent).toContain('Your Mealie rejected Buddy’s API token.');
    });
  });
});
