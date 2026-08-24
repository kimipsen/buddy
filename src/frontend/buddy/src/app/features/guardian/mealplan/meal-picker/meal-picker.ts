import { Component, ElementRef, computed, inject, input, output, signal } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { Meal } from '../../../../core/mealplans.service';

@Component({
  selector: 'app-meal-picker',
  imports: [TranslatePipe],
  templateUrl: './meal-picker.html'
})
export class MealPicker {
  readonly meals = input.required<Meal[]>();
  readonly mealId = input('');
  readonly disabled = input(false);

  readonly mealIdChange = output<string>();

  private readonly elementRef = inject(ElementRef<HTMLElement>);

  protected readonly open = signal(false);
  protected readonly query = signal('');
  protected readonly dropdownStyle = signal<Record<string, string>>({});

  protected readonly selectedMeal = computed(() => this.meals().find((meal) => meal.id === this.mealId()) ?? null);

  protected readonly displayValue = computed(() => {
    if (this.open()) {
      return this.query();
    }

    const meal = this.selectedMeal();
    return meal ? `${meal.icon} ${meal.name}` : '';
  });

  protected readonly filteredMeals = computed(() => {
    const query = this.query().trim().toLowerCase();

    if (!query) {
      return this.meals();
    }

    return this.meals().filter((meal) => meal.name.toLowerCase().includes(query));
  });

  protected openDropdown(): void {
    if (this.disabled()) {
      return;
    }

    this.query.set('');
    this.open.set(true);

    const rect = this.elementRef.nativeElement.getBoundingClientRect();
    this.dropdownStyle.set({
      position: 'fixed',
      top: `${rect.bottom + 4}px`,
      left: `${rect.left}px`,
      width: `${rect.width}px`
    });
  }

  protected closeDropdown(): void {
    this.open.set(false);
    this.query.set('');
  }

  protected onQueryInput(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }

  protected selectMeal(meal: Meal | null): void {
    this.closeDropdown();

    if ((meal?.id ?? '') !== this.mealId()) {
      this.mealIdChange.emit(meal?.id ?? '');
    }
  }

  protected selectFirstMatch(): void {
    const [first] = this.filteredMeals();

    if (first) {
      this.selectMeal(first);
    }
  }
}
