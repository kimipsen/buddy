import { Routes } from '@angular/router';

import { GuardianAdmin } from './admin/admin';
import { GuardianCalendar } from './calendar/calendar';
import { GuardianDashboard } from './dashboard';
import { MealplanAiAssistant } from './mealplan/ai-assistant/ai-assistant';
import { GuardianMealplan } from './mealplan/mealplan';
import { GuardianMedicine } from './medicine/medicine';
import { GuardianPickup } from './pickup/pickup';
import { PrintTemplateEditor } from './print/editor/print-template-editor';
import { GuardianPrint } from './print/print';
import { WeekPlanPrintPage } from './print/sheet/week-plan-print-page';
import { GuardianProgress } from './progress/progress';
import { GuardianShell } from './shell/guardian-shell';
import { GuardianTaskLibrary } from './task-library/task-library';
import { GuardianWorkLocations } from './work-locations/work-locations';

export const GUARDIAN_ROUTES: Routes = [
  // Outside GuardianShell on purpose: no navigation, header or padding may end up on paper. Listed
  // before the shell route so its '' + children can't claim the path first.
  { path: 'print/sheet/:templateId', component: WeekPlanPrintPage },
  {
    path: '',
    component: GuardianShell,
    children: [
      { path: '', component: GuardianDashboard },
      { path: 'mealplan', component: GuardianMealplan },
      { path: 'mealplan/ai-assistant', component: MealplanAiAssistant },
      { path: 'medicine', component: GuardianMedicine },
      { path: 'progress', component: GuardianProgress },
      { path: 'pickup', component: GuardianPickup },
      { path: 'work-locations', component: GuardianWorkLocations },
      { path: 'print', component: GuardianPrint },
      { path: 'print/templates/:templateId', component: PrintTemplateEditor },
      { path: 'calendar', component: GuardianCalendar },
      { path: 'task-library', component: GuardianTaskLibrary },
      { path: 'admin', component: GuardianAdmin },
    ],
  },
];
