import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { FeaturesService } from '../../core/features.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ChildrenOverview } from './children-overview/children-overview';
import { DosesToday } from './doses-today/doses-today';
import { EventsToday } from './events-today/events-today';
import { MealplanToday } from './mealplan-today/mealplan-today';
import { OnboardingResumeCard } from './onboarding/resume-card/resume-card';
import { PickupToday } from './pickup-today/pickup-today';
import { TasksToday } from './tasks-today/tasks-today';
import { Page } from '../../shared/page/page';

@Component({
  selector: 'app-guardian-dashboard',
  imports: [
    ChildrenOverview,
    OnboardingResumeCard,
    MealplanToday,
    TasksToday,
    EventsToday,
    DosesToday,
    PickupToday,
    RouterLink,
    TranslatePipe,
    Page,
  ],
  templateUrl: './dashboard.html',
})
export class GuardianDashboard {
  protected readonly features = inject(FeaturesService);
}
