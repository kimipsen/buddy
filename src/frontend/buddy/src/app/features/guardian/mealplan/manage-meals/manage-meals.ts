import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { MealplansService } from '../../../../core/mealplans.service';

const DEFAULT_COLOR = '#10b981';
const PAGE_SIZE = 5;

@Component({
  selector: 'app-manage-meals',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './manage-meals.html'
})
export class ManageMeals implements OnInit {
  private readonly guardians = inject(GuardiansService);
  private readonly mealplans = inject(MealplansService);

  private childId: string | null = null;

  protected readonly hasChildren = signal(true);
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

  ngOnInit(): void {
    void this.load();
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

    if (!this.childId || !name || !icon || !color) {
      return;
    }

    this.creating.set(true);
    this.createError.set(null);

    try {
      await this.mealplans.createMeal(this.childId, {
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
    if (!this.childId) {
      return;
    }

    this.archivingMealId.set(mealId);
    this.error.set(null);

    try {
      await this.mealplans.archiveMeal(this.childId, mealId);
    } catch {
      this.error.set('mealplan.manageMeals.archiveError');
    } finally {
      this.archivingMealId.set(null);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const children = await this.guardians.listMyChildren();

      if (children.length === 0) {
        this.hasChildren.set(false);
        return;
      }

      this.hasChildren.set(true);
      this.childId = children[0].id;
      await this.mealplans.listMeals(this.childId);
    } catch {
      this.error.set('mealplan.manageMeals.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}
