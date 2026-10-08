import {
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  resource,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';

import { todayIsoDate } from '../../../../core/date-utils';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { MealSlot, MealplanScope, MealplansService } from '../../../../core/mealplans.service';
import {
  MEAL_LOOKAHEAD_DAYS,
  OnboardingSetup,
  isWithinDaysAhead,
} from '../../../../core/onboarding.service';
import { createAction } from '../../../../shared/action-state/action-state';
import {
  ColorSwatchPicker,
  DEFAULT_COLOR_SWATCHES,
} from '../../../../shared/color-swatch-picker/color-swatch-picker';
import { DateSelect } from '../../../../shared/date-select/date-select';
import {
  SegmentedControl,
  SegmentedControlOption,
} from '../../../../shared/segmented-control/segmented-control';
import { Toggle } from '../../../../shared/toggle/toggle';

const NEW_MEAL = 'new';
const DEFAULT_ICON = '🍽️';
const DEFAULT_COLOR = DEFAULT_COLOR_SWATCHES[1];
const DINNER: MealSlot = 2;

const SLOT_LABELS: Record<MealSlot, string> = {
  0: 'mealplan.slots.breakfast',
  1: 'mealplan.slots.lunch',
  2: 'mealplan.slots.dinner',
  3: 'mealplan.slots.snack',
};

interface PlannedMeal {
  mealName: string;
  date: string;
  slot: MealSlot;
}

// Step 6: the family's one shared meal plan (reached through the first setup child, as every
// family-scope call is), at least one assignment. Sharing it with the group is opt-in and off
// until the guardian turns it on.
@Component({
  selector: 'app-onboarding-meal-step',
  imports: [FormsModule, TranslatePipe, ColorSwatchPicker, DateSelect, SegmentedControl, Toggle],
  templateUrl: './meal-step.html',
})
export class MealStep {
  private readonly mealplans = inject(MealplansService);
  private readonly i18n = inject(TranslationService);

  readonly setup = input.required<OnboardingSetup>();
  readonly changed = output<void>();

  protected readonly newMeal = NEW_MEAL;
  protected readonly slotLabels = SLOT_LABELS;

  private readonly childId = computed(() => this.setup().children[0]?.id ?? null);
  private readonly scope = computed<MealplanScope | null>(() => {
    const childId = this.childId();
    return childId === null ? null : { kind: 'family', childId };
  });

  protected readonly meals = resource({
    params: () => this.scope() ?? undefined,
    loader: ({ params }) => this.mealplans.listMeals(params),
  });
  protected readonly liveMeals = computed(() =>
    (this.meals.hasValue() ? this.meals.value() : []).filter((meal) => !meal.isArchived),
  );

  // A choice survives a reload of the meals; the default (the only meal, or a new one) applies
  // when there is no choice yet or the library was empty before.
  protected readonly mealChoice = linkedSignal<string[], string>({
    source: () => this.liveMeals().map((meal) => meal.id),
    computation: (ids, previous) => {
      const kept = previous?.value;
      if (
        kept !== undefined &&
        previous !== undefined &&
        previous.source.length > 0 &&
        (kept === NEW_MEAL || kept === '' || ids.includes(kept))
      ) {
        return kept;
      }
      if (ids.length === 0) {
        return NEW_MEAL;
      }
      return ids.length === 1 ? (ids[0] ?? '') : '';
    },
  });

  protected readonly name = signal('');
  protected readonly icon = signal(DEFAULT_ICON);
  protected readonly color = signal<string>(DEFAULT_COLOR);
  protected readonly date = signal(todayIsoDate());
  protected readonly slot = signal<MealSlot>(DINNER);
  // A meal created here whose assignment then failed: the retry assigns it, not a second meal.
  private readonly createdMeal = signal<{ id: string; name: string } | null>(null);
  protected readonly assigning = createAction();
  protected readonly planned = signal<PlannedMeal[]>([]);

  // The guide only finds a planned meal again within its look-ahead (OnboardingService.loadSetup).
  protected readonly dateInRange = computed(() =>
    isWithinDaysAhead(this.date(), MEAL_LOOKAHEAD_DAYS),
  );
  protected readonly canAssign = computed(() => {
    const choice = this.mealChoice();
    const mealOk = choice === NEW_MEAL ? this.name().trim().length > 0 : choice !== '';
    return mealOk && this.dateInRange();
  });

  protected readonly slotOptions = computed<SegmentedControlOption<MealSlot>[]>(() =>
    ([0, 1, 2, 3] as const).map((value) => ({
      value,
      label: this.i18n.translate(SLOT_LABELS[value]),
    })),
  );

  protected readonly sharedGroup = resource({
    params: () => this.childId() ?? undefined,
    loader: ({ params }) => this.mealplans.getSharedGroup(params),
  });
  protected readonly isShared = computed(
    () =>
      this.sharedGroup.hasValue() && this.sharedGroup.value()?.groupId === this.setup().group?.id,
  );
  protected readonly sharing = createAction();

  protected async assign(): Promise<void> {
    const scope = this.scope();

    if (scope === null || !this.canAssign()) {
      return;
    }

    await this.assigning.run(
      true,
      async () => {
        const meal = await this.resolveMeal(scope);
        await this.mealplans.assignMealToSlot(scope, this.date(), this.slot(), meal.id, '');
        this.planned.update((planned) => [
          ...planned,
          { mealName: meal.name, date: this.date(), slot: this.slot() },
        ]);
        this.createdMeal.set(null);
        this.name.set('');
        this.meals.reload();
        this.changed.emit();
      },
      'onboarding.meal.assignError',
    );
  }

  protected async setShared(shared: boolean): Promise<void> {
    const childId = this.childId();
    const groupId = this.setup().group?.id;

    if (childId === null || groupId === undefined) {
      return;
    }

    await this.sharing.run(
      true,
      async () => {
        await (shared
          ? this.mealplans.shareWithGroup(childId, groupId)
          : this.mealplans.unshareFromGroup(childId, groupId));
        this.sharedGroup.reload();
      },
      'onboarding.meal.shareError',
    );
  }

  private async resolveMeal(scope: MealplanScope): Promise<{ id: string; name: string }> {
    const choice = this.mealChoice();

    if (choice !== NEW_MEAL) {
      const existing = this.liveMeals().find((meal) => meal.id === choice);
      return { id: choice, name: existing?.name ?? '' };
    }

    const pending = this.createdMeal();
    if (pending !== null) {
      return pending;
    }

    const created = await this.mealplans.createMeal(scope, {
      name: this.name().trim(),
      description: '',
      icon: this.icon().trim() || DEFAULT_ICON,
      color: this.color(),
    });
    const meal = { id: created.id, name: created.name };
    this.createdMeal.set(meal);
    return meal;
  }
}
