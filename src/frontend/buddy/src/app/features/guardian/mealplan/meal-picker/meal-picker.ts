import {
  Component,
  ElementRef,
  HostListener,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { Meal } from '../../../../core/mealplans.service';
import { DropdownSize, dropdownPosition } from '../../../../core/dropdown-position';

// The list's height (max-h-56) and narrowest width, in px; see dropdownPosition.
const LIST_SIZE: DropdownSize = { maxHeight: 224, minWidth: 192 };

@Component({
  selector: 'app-meal-picker',
  imports: [TranslatePipe],
  templateUrl: './meal-picker.html',
})
export class MealPicker {
  readonly meals = input.required<Meal[]>();
  readonly mealId = input('');
  readonly disabled = input(false);

  readonly mealIdChange = output<string>();

  private readonly elementRef = inject(ElementRef<HTMLElement>);

  private readonly queryInput = viewChild.required<ElementRef<HTMLInputElement>>('queryInput');
  private readonly list = viewChild<ElementRef<HTMLElement>>('list');

  protected readonly open = signal(false);
  protected readonly query = signal('');
  protected readonly dropdownStyle = signal<Record<string, string>>({});

  protected readonly selectedMeal = computed(
    () => this.meals().find((meal) => meal.id === this.mealId()) ?? null,
  );

  protected readonly displayValue = computed(() => {
    if (this.open()) {
      return this.query();
    }

    const meal = this.selectedMeal();
    return meal ? `${meal.icon} ${meal.name}` : '';
  });

  protected readonly filteredMeals = computed((): Meal[] => {
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

    this.dropdownStyle.set(
      dropdownPosition(this.elementRef.nativeElement.getBoundingClientRect(), LIST_SIZE, {
        width: window.innerWidth,
        height: window.innerHeight,
      }),
    );
  }

  protected closeDropdown(): void {
    this.open.set(false);
    this.query.set('');
  }

  // Escape from the input or from an option inside the list. If focus is on an option it is about
  // to be removed, so hand it back to the input first; that refocus re-runs openDropdown(), which
  // is harmless because the dropdown is closed straight after.
  @HostListener('keydown.escape')
  protected onEscape(): void {
    if (!this.open()) {
      return;
    }

    if (this.list()?.nativeElement.contains(document.activeElement)) {
      this.queryInput().nativeElement.focus();
    }

    this.closeDropdown();
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
