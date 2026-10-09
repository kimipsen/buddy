import { Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';

import { GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  ImportFormat,
  ImportLineKind,
  ImportWeekStart,
  MealPlanImportEntry,
  MealPlanImportPreview,
  MealPlanImportPreviewGroup,
  MealPlanImportResult,
  MealSlot,
  MealplanScope,
  MealplansService,
} from '../../../../core/mealplans.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { Toggle } from '../../../../shared/toggle/toggle';
import { Card } from '../../../../shared/card/card';
import { Page } from '../../../../shared/page/page';

// Rows of the meal table shown at a time; "Show more" adds another page.
const PAGE_SIZE = 50;

const KIND_LABEL_KEYS: Record<ImportLineKind, string> = {
  Meal: 'mealplan.import.review.kind.meal',
  Alternatives: 'mealplan.import.review.kind.alternatives',
  Leftovers: 'mealplan.import.review.kind.leftovers',
  Away: 'mealplan.import.review.kind.away',
};

const SLOT_LABEL_KEYS: Record<MealSlot, string> = {
  Breakfast: 'mealplan.slots.breakfast',
  Lunch: 'mealplan.slots.lunch',
  Dinner: 'mealplan.slots.dinner',
  Snack: 'mealplan.slots.snack',
};

const WARNING_KEYS: Record<string, string> = {
  week_number_corrected: 'mealplan.import.review.warnings.weekNumberCorrected',
  week_number_inferred: 'mealplan.import.review.warnings.weekNumberInferred',
  day_inferred_from_position: 'mealplan.import.review.warnings.dayInferredFromPosition',
  duplicate_day: 'mealplan.import.review.warnings.duplicateDay',
  unrecognized_line: 'mealplan.import.review.warnings.unrecognizedLine',
};

// What the guardian decided for one group of same-named lines.
export type ImportDecision =
  | { kind: 'existing'; mealId: string; name: string }
  | { kind: 'new'; name: string }
  | { kind: 'skip' }
  // Same meal as another group (a spelling variant), whatever that group resolves to.
  | { kind: 'merge'; key: string; name: string };

type ResolvedDecision = Exclude<ImportDecision, { kind: 'merge' }>;

function defaultDecision(group: MealPlanImportPreviewGroup): ImportDecision {
  if (group.defaultAction === 'Existing' && group.matchedMealId) {
    return { kind: 'existing', mealId: group.matchedMealId, name: group.matchedMealName };
  }

  return group.defaultAction === 'Skip' ? { kind: 'skip' } : { kind: 'new', name: group.name };
}

function encodeDecision(decision: ImportDecision): string {
  switch (decision.kind) {
    case 'existing':
      return `existing:${decision.mealId}`;
    case 'merge':
      return `merge:${decision.key}`;
    default:
      return decision.kind;
  }
}

// Mirrors the backend's ImportLineClassifier.NormalizeKey closely enough to count new meals the
// way the commit will create them: case, punctuation and "m"/"m."/"med" don't make a new meal.
function normalizeKey(name: string): string {
  return name
    .toLowerCase()
    .replace(/[^\p{L}\p{N}]+/gu, ' ')
    .trim()
    .split(' ')
    .filter((word) => word.length > 0)
    .map((word) => (word === 'med' ? 'm' : word))
    .join(' ');
}

// The first message of a backend validation envelope ({ details: { field: [message] } }), shown
// untranslated next to a translated summary.
function validationDetail(error: unknown): string {
  if (error instanceof HttpErrorResponse && error.status === 400) {
    const details = (error.error as { details?: Record<string, string[]> } | null)?.details;
    return Object.values(details ?? {}).flat()[0] ?? '';
  }

  return '';
}

@Component({
  selector: 'app-mealplan-import',
  imports: [RouterLink, FormsModule, TranslatePipe, Toggle, Card, Page],
  templateUrl: './mealplan-import.html',
})
export class MealplanImport {
  private readonly guardians = inject(GuardiansService);
  private readonly mealplans = inject(MealplansService);
  private readonly translation = inject(TranslationService);

  protected readonly kindLabelKeys = KIND_LABEL_KEYS;
  protected readonly slotLabelKeys = SLOT_LABEL_KEYS;
  protected readonly allSlots: readonly MealSlot[] = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];
  protected readonly encodeDecision = encodeDecision;

  // The guardian's first child's family scope, or null without children (same as the AI assistant).
  protected readonly scope = resource({ loader: () => this.loadScope() });
  protected readonly history = resource({
    params: () => (this.scope.hasValue() ? (this.scope.value() ?? undefined) : undefined),
    loader: ({ params: scope }) => this.mealplans.listImports(scope),
  });

  protected readonly text = signal('');
  protected readonly format = signal<ImportFormat>('auto');
  protected readonly weekStart = signal<ImportWeekStart>('Sunday');
  protected readonly slot = signal<MealSlot>('Dinner');
  protected readonly previewing = createAction();
  protected readonly previewDetail = signal('');

  protected readonly preview = signal<MealPlanImportPreview | null>(null);
  protected readonly decisions = signal<Record<string, ImportDecision>>({});
  protected readonly archiveSingleUse = signal(true);
  protected readonly search = signal('');
  protected readonly visibleCount = signal(PAGE_SIZE);
  protected readonly committing = createAction();
  protected readonly result = signal<MealPlanImportResult | null>(null);

  protected readonly reverting = createAction<string>();
  protected readonly confirmingRevertId = signal<string | null>(null);

  private readonly groupsByKey = computed(
    () => new Map((this.preview()?.groups ?? []).map((g) => [g.key, g])),
  );
  protected readonly filteredGroups = computed(() => {
    const groups = this.preview()?.groups ?? [];
    const term = this.search().trim().toLowerCase();
    return term ? groups.filter((g) => g.name.toLowerCase().includes(term)) : groups;
  });
  protected readonly visibleGroups = computed(() =>
    this.filteredGroups().slice(0, this.visibleCount()),
  );

  protected readonly entries = computed((): MealPlanImportEntry[] => {
    const preview = this.preview();

    if (!preview) {
      return [];
    }

    return preview.lines.flatMap((line): MealPlanImportEntry[] => {
      const decision = this.resolve(line.key);

      if (line.occupied || decision.kind === 'skip') {
        return [];
      }

      if (decision.kind === 'existing') {
        return [{ date: line.date, slot: line.slot, mealId: decision.mealId, notes: line.notes }];
      }

      // A name cleared in the review falls back to the parsed one rather than failing the commit.
      const newMealName = decision.name.trim() || line.mealName;
      return [{ date: line.date, slot: line.slot, newMealName, notes: line.notes }];
    });
  });

  protected readonly summary = computed(() => {
    const preview = this.preview();
    const entries = this.entries();
    const dates = preview?.lines.map((l) => l.date) ?? [];
    const newNames = new Set(
      entries.flatMap((e) => (e.newMealName ? [normalizeKey(e.newMealName)] : [])),
    );

    return {
      lines: dates.length,
      from: dates[0] ?? '',
      to: dates[dates.length - 1] ?? '',
      importing: entries.length,
      newMeals: newNames.size,
      matched: entries.filter((e) => e.mealId).length,
      occupied: preview?.lines.filter((l) => l.occupied).length ?? 0,
      skipped: (preview?.lines.length ?? 0) - entries.length,
      hasLeftovers: preview?.groups.some((g) => g.kind === 'Leftovers') ?? false,
    };
  });

  protected decisionFor(group: MealPlanImportPreviewGroup): ImportDecision {
    return this.decisions()[group.key] ?? defaultDecision(group);
  }

  protected setDecision(group: MealPlanImportPreviewGroup, encoded: string): void {
    const decision: ImportDecision = encoded.startsWith('existing:')
      ? {
          kind: 'existing',
          mealId: encoded.slice('existing:'.length),
          name:
            encoded.slice('existing:'.length) === group.matchedMealId
              ? group.matchedMealName
              : group.suggestedName,
        }
      : encoded.startsWith('merge:')
        ? { kind: 'merge', key: encoded.slice('merge:'.length), name: group.suggestedName }
        : encoded === 'skip'
          ? { kind: 'skip' }
          : { kind: 'new', name: group.name };

    this.decisions.update((current) => ({ ...current, [group.key]: decision }));
  }

  protected renameNewMeal(group: MealPlanImportPreviewGroup, name: string): void {
    this.decisions.update((current) => ({ ...current, [group.key]: { kind: 'new', name } }));
  }

  protected importLeftoversAsOneMeal(): void {
    const leftovers = (this.preview()?.groups ?? []).filter((g) => g.kind === 'Leftovers');
    const name = this.translation.translate('mealplan.import.review.leftoversMealName');

    this.decisions.update((current) => ({
      ...current,
      ...Object.fromEntries(leftovers.map((g) => [g.key, { kind: 'new', name } as ImportDecision])),
    }));
  }

  protected warningKey(code: string): string {
    return WARNING_KEYS[code] ?? 'mealplan.import.review.warnings.other';
  }

  protected showMore(): void {
    this.visibleCount.update((count) => count + PAGE_SIZE);
  }

  protected async readFile(event: Event): Promise<void> {
    const file = (event.target as HTMLInputElement).files?.[0];

    if (file) {
      this.text.set(await file.text());
    }
  }

  protected async runPreview(): Promise<void> {
    const scope = this.currentScope();
    const text = this.text().trim();

    if (!scope || !text) {
      return;
    }

    this.previewDetail.set('');
    await this.previewing.run(
      true,
      async () => {
        const preview = await this.mealplans.previewImport(scope, {
          text,
          format: this.format(),
          weekStart: this.weekStart(),
          slot: this.slot(),
        });
        this.preview.set(preview);
        this.decisions.set({});
        this.search.set('');
        this.visibleCount.set(PAGE_SIZE);
        this.result.set(null);
      },
      (error) => {
        this.previewDetail.set(validationDetail(error));
        return 'mealplan.import.input.previewError';
      },
    );
  }

  protected backToText(): void {
    this.preview.set(null);
    this.committing.reset();
  }

  protected async commit(): Promise<void> {
    const scope = this.currentScope();
    const preview = this.preview();
    const entries = this.entries();

    if (!scope || !preview || entries.length === 0) {
      return;
    }

    await this.committing.run(
      true,
      async () => {
        // One request, one transaction: the backend takes up to 5,000 entries, and splitting would
        // create each new meal once per part.
        const result = await this.mealplans.commitImport(scope, {
          format: preview.format,
          archiveSingleUse: this.archiveSingleUse(),
          entries,
        });

        this.result.set(result);
        this.preview.set(null);
        this.text.set('');
        this.history.reload();
      },
      'mealplan.import.review.importError',
    );
  }

  protected startOver(): void {
    this.result.set(null);
  }

  protected requestRevert(importId: string): void {
    this.reverting.reset();
    this.confirmingRevertId.set(importId);
  }

  protected cancelRevert(): void {
    this.confirmingRevertId.set(null);
  }

  protected async confirmRevert(importId: string): Promise<void> {
    const scope = this.currentScope();

    if (!scope) {
      return;
    }

    await this.reverting.run(
      importId,
      async () => {
        await this.mealplans.revertImport(scope, importId);
        this.confirmingRevertId.set(null);
        this.history.reload();
      },
      'mealplan.import.history.revertError',
    );
  }

  // Follows "same as" links to the decision that actually applies. Suggestions only ever point
  // at a more frequent group, so a chain ends quickly; the depth cap guards against a cycle.
  private resolve(key: string, depth = 0): ResolvedDecision {
    const group = this.groupsByKey().get(key);

    if (!group || depth > 5) {
      return { kind: 'skip' };
    }

    const decision = this.decisionFor(group);
    return decision.kind === 'merge' ? this.resolve(decision.key, depth + 1) : decision;
  }

  private currentScope(): MealplanScope | null {
    return this.scope.hasValue() ? this.scope.value() : null;
  }

  private async loadScope(): Promise<MealplanScope | null> {
    const [firstChild] = await this.guardians.listMyChildren();
    return firstChild ? { kind: 'family', childId: firstChild.id } : null;
  }
}
