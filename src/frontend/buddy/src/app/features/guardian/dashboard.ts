import { Component } from '@angular/core';

import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ChildrenOverview } from './children-overview/children-overview';
import { DosesToday } from './doses-today/doses-today';
import { EventsToday } from './events-today/events-today';
import { MealplanToday } from './mealplan-today/mealplan-today';
import { TasksToday } from './tasks-today/tasks-today';

@Component({
  selector: 'app-guardian-dashboard',
  imports: [ChildrenOverview, MealplanToday, TasksToday, EventsToday, DosesToday, TranslatePipe],
  templateUrl: './dashboard.html'
})
export class GuardianDashboard {}
