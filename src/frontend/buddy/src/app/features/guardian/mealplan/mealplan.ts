import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AssignMealplan } from './assign-mealplan/assign-mealplan';
import { ManageMeals } from './manage-meals/manage-meals';

@Component({
  selector: 'app-guardian-mealplan',
  imports: [RouterLink, ManageMeals, AssignMealplan],
  templateUrl: './mealplan.html'
})
export class GuardianMealplan {}
