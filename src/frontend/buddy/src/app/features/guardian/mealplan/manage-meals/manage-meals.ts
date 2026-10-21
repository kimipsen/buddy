import { Component, computed, inject, input, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { iconText } from '../../../../core/pictograms';
import {
  EATING_OUT_NONE,
  Meal,
  MealEatingOut,
  MealplanAccessTier,
  MealplanScope,
  MealplansService,
} from '../../../../core/mealplans.service';
import { mealieErrorKey } from '../../../../core/mealie.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { MealieRecipePanel } from '../mealie/recipe-panel/mealie-recipe-panel';
import { MealieRecipePicker } from '../mealie/recipe-picker/mealie-recipe-picker';
import { ColorSwatchPicker } from '../../../../shared/color-swatch-picker/color-swatch-picker';
import { Card } from '../../../../shared/card/card';
import {
  EATING_OUT_KIND_KEYS,
  EatingOutBadge,
} from '../../../../shared/eating-out-badge/eating-out-badge';
import { Icon } from '../../../../shared/icon/icon';
import {
  SegmentedControl,
  SegmentedControlOption,
} from '../../../../shared/segmented-control/segmented-control';

const DEFAULT_COLOR = '#10b981';
const DEFAULT_ICON = '🍽️';

type MealiePanel =
  | { kind: 'view'; mealId: string; mealName: string; recipeId: string }
  | { kind: 'link'; mealId: string; mealName: string }
  | { kind: 'create' };
const PAGE_SIZE = 5;
const MANAGE: MealplanAccessTier = 'Manage';
const EATING_OUT_KINDS: MealEatingOut[] = ['None', 'FriendsAndFamily', 'Restaurant'];

@Component({
  selector: 'app-manage-meals',
  imports: [
    Icon,
    FormsModule,
    TranslatePipe,
    ColorSwatchPicker,
    Card,
    EatingOutBadge,
    SegmentedControl,
    MealieRecipePanel,
    MealieRecipePicker,
  ],
  templateUrl: './manage-meals.html',
})
export class ManageMeals {
  private readonly mealplans = inject(MealplansService);
  private readonly translation = inject(TranslationService);

  readonly scope = input.required<MealplanScope>();

  // A group scope with a View (not Manage) tier is read-only -- the backend rejects every write
  // with 403 regardless, but the UI hides those controls rather than letting the user hit them.
  protected readonly readOnly = computed(() => {
    const scope = this.scope();
    return scope.kind === 'group' && scope.accessTier !== MANAGE;
  });

  protected readonly isFamilyScope = computed(() => this.scope().kind === 'family');
  protected readonly groupName = computed(() => {
    const scope = this.scope();
    return scope.kind === 'group' ? scope.groupName : null;
  });

  // Refreshes the shared meal library for the scope; the list itself is read from meals below. A
  // scope change also drops an archive error left over from the previous scope.
  protected readonly library = resource({
    params: () => this.scope(),
    loader: ({ params: scope }) => {
      this.archiving.reset();
      this.updating.reset();
      this.mealiePanel.set(null);
      this.linking.reset();
      return this.mealplans.listMeals(scope);
    },
  });

  // Reads straight from the shared service state, so a meal created/archived from the mealplan
  // grid on the same page (or vice versa) shows up here without a manual refetch.
  protected readonly meals = computed(() =>
    this.mealplans.meals().filter((meal) => !meal.isArchived),
  );

  protected readonly currentPage = signal(0);
  protected readonly totalPages = computed((): number =>
    Math.max(1, Math.ceil(this.meals().length / PAGE_SIZE)),
  );
  protected readonly page = computed((): number =>
    Math.min(this.currentPage(), this.totalPages() - 1),
  );
  protected readonly pagedMeals = computed(() => {
    const start = this.page() * PAGE_SIZE;
    return this.meals().slice(start, start + PAGE_SIZE);
  });

  protected readonly newMealName = signal('');

  // Non-blocking hint shown under the create form so guardians notice a near-duplicate before
  // submitting -- matches both directions (existing name contains the query, or vice versa) so
  // it still fires once the user has typed past an exact existing name (e.g. "Pizza night").
  protected readonly similarMeals = computed((): Meal[] => {
    const query = this.newMealName().trim().toLowerCase();

    if (!query) {
      return [];
    }

    return this.meals().filter(
      (meal) => meal.name.toLowerCase().includes(query) || query.includes(meal.name.toLowerCase()),
    );
  });

  protected readonly similarMealsLabel = computed(() =>
    this.similarMeals()
      .map((meal) => [iconText(meal.icon), meal.name].filter(Boolean).join(' '))
      .join(', '),
  );

  protected readonly newMealDescription = signal('');
  protected readonly newMealIcon = signal('🍽️');
  protected readonly newMealColor = signal(DEFAULT_COLOR);
  protected readonly newMealEatingOut = signal<MealEatingOut>(EATING_OUT_NONE);
  protected readonly eatingOutKinds = EATING_OUT_KINDS;
  // The row select reads "At home" for None, where the create form's segmented control (labelled
  // "Eating out") reads "No".
  protected readonly eatingOutSelectKeys: Record<MealEatingOut, string> = {
    ...EATING_OUT_KIND_KEYS,
    None: 'mealplan.eatingOut.atHome',
  };
  protected readonly eatingOutOptions = computed((): SegmentedControlOption<MealEatingOut>[] =>
    EATING_OUT_KINDS.map((kind) => ({
      value: kind,
      label: this.translation.translate(EATING_OUT_KIND_KEYS[kind]),
    })),
  );
  protected readonly creating = createAction();
  protected readonly archiving = createAction<string>();

  // The family's child to call the Mealie routes with, set only while the family scope is shown
  // and the family is connected (docs/backend/analysis/mealie-integration.md): no Mealie control
  // shows otherwise.
  readonly mealieChildId = input<string | null>(null);

  // Below the meal list: one linked meal's recipe, the recipe picker for linking one meal, or the
  // picker for "Add from Mealie".
  protected readonly mealiePanel = signal<MealiePanel | null>(null);
  protected readonly linking = createAction();
  protected readonly updating = createAction<string>();

  protected previousPage(): void {
    this.currentPage.set(Math.max(this.page() - 1, 0));
  }

  protected nextPage(): void {
    this.currentPage.set(Math.min(this.page() + 1, this.totalPages() - 1));
  }

  protected async createMeal(): Promise<void> {
    const name = this.newMealName().trim();
    const icon = this.newMealIcon().trim();
    const color = this.newMealColor().trim();

    if (!name || !icon || !color) {
      return;
    }

    await this.creating.run(
      true,
      async () => {
        await this.mealplans.createMeal(this.scope(), {
          name,
          description: this.newMealDescription().trim(),
          icon,
          color,
          eatingOut: this.newMealEatingOut(),
        });
        this.newMealName.set('');
        this.newMealDescription.set('');
        this.newMealIcon.set('🍽️');
        this.newMealColor.set(DEFAULT_COLOR);
        this.newMealEatingOut.set(EATING_OUT_NONE);
        // Jump to the last page so the newly created meal (appended at the end) is visible.
        this.currentPage.set(this.totalPages() - 1);
      },
      'mealplan.manageMeals.createError',
    );
  }

  // The details PATCH is a full replace, so the meal's other details go along unchanged.
  protected async setEatingOut(meal: Meal, select: HTMLSelectElement): Promise<void> {
    const eatingOut = select.value as MealEatingOut;
    await this.updating.run(
      meal.id,
      async () => {
        await this.mealplans.updateMealDetails(this.scope(), meal.id, {
          name: meal.name,
          description: meal.description,
          icon: meal.icon,
          color: meal.color,
          eatingOut,
        });
      },
      'mealplan.eatingOut.updateError',
    );

    // On failure the meal is unchanged, so its [value] binding never fires again: put the select
    // back by hand rather than leave it showing a kind that wasn't saved.
    if (this.updating.state().status === 'error') {
      select.value = meal.eatingOut;
    }
  }

  protected isMealiePanelFor(mealId: string): boolean {
    const panel = this.mealiePanel();
    return panel !== null && panel.kind !== 'create' && panel.mealId === mealId;
  }

  // "View recipe" on a linked meal, "Link recipe" on one without; a second click closes it.
  protected toggleMealiePanel(meal: Meal): void {
    this.linking.reset();

    if (this.isMealiePanelFor(meal.id)) {
      this.mealiePanel.set(null);
      return;
    }

    this.mealiePanel.set(
      meal.recipe
        ? { kind: 'view', mealId: meal.id, mealName: meal.name, recipeId: meal.recipe.recipeId }
        : { kind: 'link', mealId: meal.id, mealName: meal.name },
    );
  }

  protected startLink(mealId: string, mealName: string): void {
    this.linking.reset();
    this.mealiePanel.set({ kind: 'link', mealId, mealName });
  }

  protected startCreateFromRecipe(): void {
    this.linking.reset();
    this.mealiePanel.set(this.mealiePanel()?.kind === 'create' ? null : { kind: 'create' });
  }

  protected closeMealiePanel(): void {
    this.mealiePanel.set(null);
  }

  protected async linkRecipe(mealId: string, recipeId: string): Promise<void> {
    const childId = this.mealieChildId();

    if (!childId) {
      return;
    }

    await this.linking.run(
      true,
      async () => {
        const meal = await this.mealplans.linkMealToRecipe(childId, mealId, recipeId);
        this.mealiePanel.set({
          kind: 'view',
          mealId,
          mealName: meal.name,
          recipeId,
        });
      },
      (error) => mealieErrorKey(error, 'mealie.recipes.linkError'),
    );
  }

  protected async unlinkRecipe(mealId: string): Promise<void> {
    const childId = this.mealieChildId();

    if (!childId) {
      return;
    }

    await this.linking.run(
      true,
      async () => {
        await this.mealplans.unlinkMealRecipe(childId, mealId);
        this.mealiePanel.set(null);
      },
      'mealie.recipes.panel.unlinkError',
    );
  }

  protected async createFromRecipe(recipeId: string): Promise<void> {
    const childId = this.mealieChildId();

    if (!childId) {
      return;
    }

    await this.linking.run(
      true,
      async () => {
        const meal = await this.mealplans.createMealFromRecipe(
          childId,
          recipeId,
          DEFAULT_ICON,
          DEFAULT_COLOR,
        );
        this.mealiePanel.set(null);
        // The library is sorted by name, so open the page the new meal landed on.
        const index = this.meals().findIndex((m) => m.id === meal.id);
        this.currentPage.set(Math.max(0, Math.floor(index / PAGE_SIZE)));
      },
      (error) => mealieErrorKey(error, 'mealie.recipes.createError'),
    );
  }

  protected async archiveMeal(mealId: string): Promise<void> {
    await this.archiving.run(
      mealId,
      () => this.mealplans.archiveMeal(this.scope(), mealId),
      'mealplan.manageMeals.archiveError',
    );
  }
}
