import { Component, computed, input } from '@angular/core';

import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { EATING_OUT_NONE, MealEatingOut } from '../../core/mealplans.service';

export const EATING_OUT_KIND_KEYS: Record<MealEatingOut, string> = {
  None: 'mealplan.eatingOut.none',
  FriendsAndFamily: 'mealplan.eatingOut.friendsAndFamily',
  Restaurant: 'mealplan.eatingOut.restaurant',
};

// Amber "Eating out · Restaurant" pill for a meal eaten away from home; renders nothing for None.
@Component({
  selector: 'app-eating-out-badge',
  imports: [TranslatePipe],
  templateUrl: './eating-out-badge.html',
})
export class EatingOutBadge {
  readonly kind = input.required<MealEatingOut>();

  protected readonly kindKey = computed(() => EATING_OUT_KIND_KEYS[this.kind()]);
  protected readonly visible = computed(() => this.kind() !== EATING_OUT_NONE);
}
