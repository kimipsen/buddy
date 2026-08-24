import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { MealplanAccessTier, MealplanScope, MealplansService } from '../../../../core/mealplans.service';

const DEFAULT_COLOR = '#10b981';
const PAGE_SIZE = 5;
const MANAGE: MealplanAccessTier = 2;

@Component({
  selector: 'app-manage-meals',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './manage-meals.html'
})
export class ManageMeals {
  private readonly mealplans = inject(MealplansService);

  readonly scope = input.required<MealplanScope>();

  // A group scope with a View (not Manage) tier is read-only -- the backend rejects every write
  // with 403 regardless, but the UI hides those controls rather than letting the user hit them.
  protected readonly readOnly = computed(() => {
    const scope = this.scope();
    return scope.kind === 'group' && scope.accessTier !== MANAGE;
  });

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  // Reads straight from the shared service state, so a meal created/archived from the mealplan
  // grid on the same page (or vice versa) shows up here without a manual refetch.
  protected readonly meals = computed(() => this.mealplans.meals().filter((meal) => !meal.isArchived));

  protected readonly currentPage = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.meals().length / PAGE_SIZE)));
  protected readonly page = computed(() => Math.min(this.currentPage(), this.totalPages() - 1));
  protected readonly pagedMeals = computed(() => {
    const start = this.page() * PAGE_SIZE;
    return this.meals().slice(start, start + PAGE_SIZE);
  });

  protected readonly newMealName = signal('');
  protected readonly newMealDescription = signal('');
  protected readonly newMealIcon = signal('🍽️');
  protected readonly newMealColor = signal(DEFAULT_COLOR);
  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);

  protected readonly archivingMealId = signal<string | null>(null);

  constructor() {
    effect(() => {
      void this.load(this.scope());
    });
  }

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

    this.creating.set(true);
    this.createError.set(null);

    try {
      await this.mealplans.createMeal(this.scope(), {
        name,
        description: this.newMealDescription().trim() || null,
        icon,
        color
      });
      this.newMealName.set('');
      this.newMealDescription.set('');
      this.newMealIcon.set('🍽️');
      this.newMealColor.set(DEFAULT_COLOR);
      // Jump to the last page so the newly created meal (appended at the end) is visible.
      this.currentPage.set(this.totalPages() - 1);
    } catch {
      this.createError.set('mealplan.manageMeals.createError');
    } finally {
      this.creating.set(false);
    }
  }

  protected async archiveMeal(mealId: string): Promise<void> {
    this.archivingMealId.set(mealId);
    this.error.set(null);

    try {
      await this.mealplans.archiveMeal(this.scope(), mealId);
    } catch {
      this.error.set('mealplan.manageMeals.archiveError');
    } finally {
      this.archivingMealId.set(null);
    }
  }

  private async load(scope: MealplanScope): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      await this.mealplans.listMeals(scope);
    } catch {
      this.error.set('mealplan.manageMeals.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}
