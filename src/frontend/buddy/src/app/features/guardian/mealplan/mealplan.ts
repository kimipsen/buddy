import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AssignMealplan } from './assign-mealplan/assign-mealplan';
import { ManageMeals } from './manage-meals/manage-meals';

@Component({
  selector: 'app-guardian-mealplan',
  imports: [RouterLink, ManageMeals, AssignMealplan, TranslatePipe],
  templateUrl: './mealplan.html'
})
export class GuardianMealplan {}
