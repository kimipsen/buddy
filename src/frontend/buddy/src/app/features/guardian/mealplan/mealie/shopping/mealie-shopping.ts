import { Component, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslatePipe } from '../../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../../core/i18n/translation.service';
import {
  AddedToShoppingList,
  MealieService,
  MealieShoppingList,
  mealieErrorKey,
} from '../../../../../core/mealie.service';
import { MealSlot } from '../../../../../core/mealplans.service';
import { createAction } from '../../../../../shared/action-state/action-state';
import { readLastShoppingListId, writeLastShoppingListId } from './last-shopping-list-storage';

// Every slot: what goes on the list is decided by which meals have a recipe, and eating-out meals
// are left out by the backend.
const ALL_SLOTS: MealSlot[] = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];

// "Add to shopping list" for the week shown in the plan: the linked recipes of that week go to a
// Mealie shopping list in one call (docs/backend/analysis/mealie-integration.md, Question 6).
@Component({
  selector: 'app-mealie-shopping',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './mealie-shopping.html',
})
export class MealieShopping {
  private readonly mealie = inject(MealieService);
  private readonly translation = inject(TranslationService);

  readonly childId = input.required<string>();
  readonly from = input.required<string>();
  readonly to = input.required<string>();

  protected readonly open = signal(false);
  protected readonly lists = signal<MealieShoppingList[] | null>(null);
  protected readonly listsError = signal<string | null>(null);
  // Stryker disable next-line StringLiteral: set from the loaded lists before the list picker shows
  protected readonly listId = signal('');
  protected readonly adding = createAction();
  private readonly lastResult = signal<{
    list: string;
    from: string;
    to: string;
    added: AddedToShoppingList;
  } | null>(null);
  // Shown only while the plan still shows the week it was added for.
  protected readonly result = computed(() => {
    const last = this.lastResult();
    return last && last.from === this.from() && last.to === this.to() ? last : null;
  });

  // Only the latest opening may write the lists, so a close-and-reopen can't be overtaken by the
  // first, slower load.
  private listsRequest = 0;

  // "Lasagne al forno ×2, Chicken tacos ×1".
  protected addedText(added: AddedToShoppingList): string {
    return added.added
      .map((recipe) =>
        this.translation.translate('mealie.shopping.recipeTimes', {
          recipe: recipe.recipeName,
          times: recipe.times,
        }),
      )
      .join(', ');
  }

  protected async toggle(): Promise<void> {
    if (this.open()) {
      this.open.set(false);
      return;
    }

    // Stryker disable next-line UpdateOperator: counting down gives every opening a unique number too
    const request = ++this.listsRequest;
    this.open.set(true);
    this.lastResult.set(null);
    this.adding.reset();
    this.listsError.set(null);
    this.lists.set(null);

    try {
      const lists = await this.mealie.listShoppingLists(this.childId());

      if (request !== this.listsRequest) {
        return;
      }

      this.lists.set(lists);
      const remembered = readLastShoppingListId();
      // Stryker disable next-line StringLiteral: with no lists the picker isn't shown, so the id is never read
      this.listId.set(lists.find((l) => l.id === remembered)?.id ?? lists[0]?.id ?? '');
    } catch (error) {
      if (request === this.listsRequest) {
        this.listsError.set(mealieErrorKey(error, 'mealie.errors.error'));
      }
    }
  }

  protected async add(): Promise<void> {
    const listId = this.listId();
    const list = this.lists()?.find((l) => l.id === listId);

    if (!list) {
      return;
    }

    const from = this.from();
    const to = this.to();

    await this.adding.run(
      true,
      async () => {
        const added = await this.mealie.addPlanToShoppingList(
          this.childId(),
          listId,
          from,
          to,
          ALL_SLOTS,
        );
        writeLastShoppingListId(listId);
        this.lastResult.set({ list: list.name, from, to, added });
        this.open.set(false);
      },
      (error) => mealieErrorKey(error, 'mealie.shopping.error'),
    );
  }
}
