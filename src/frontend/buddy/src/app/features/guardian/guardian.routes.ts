import { Routes } from '@angular/router';

import { onboardingEntryGuard } from '../../core/onboarding.guard';
import { GuardianAdmin } from './admin/admin';
import { GuardianBabysitters } from './babysitters/babysitters';
import { GuardianCalendar } from './calendar/calendar';
import { GuardianDashboard } from './dashboard';
import { GuardianHelp } from './help/help-page';
import { MealplanAiAssistant } from './mealplan/ai-assistant/ai-assistant';
import { MealplanImport } from './mealplan/import/mealplan-import';
import { GuardianMealplan } from './mealplan/mealplan';
import { GuardianMedicine } from './medicine/medicine';
import { GuardianOnboarding } from './onboarding/onboarding';
import { GuardianPickup } from './pickup/pickup';
import { PrintTemplateEditor } from './print/editor/print-template-editor';
import { GuardianPrint } from './print/print';
import { WeekPlanPrintPage } from './print/sheet/week-plan-print-page';
import { GuardianProgress } from './progress/progress';
import { GuardianShell } from './shell/guardian-shell';
import { GuardianSleepDiary } from './sleep-diary/sleep-diary';
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
      // The home is where a guardian with nothing set up yet is sent into the guide.
      {
        path: '',
        component: GuardianDashboard,
        canActivate: [onboardingEntryGuard],
        data: { helpTopic: 'dashboard' },
      },
      { path: 'onboarding', component: GuardianOnboarding },
      {
        path: 'mealplan',
        component: GuardianMealplan,
        data: { helpTopic: 'mealPlans' },
      },
      {
        path: 'mealplan/ai-assistant',
        component: MealplanAiAssistant,
        data: { helpTopic: 'mealPlans' },
      },
      {
        path: 'mealplan/import',
        component: MealplanImport,
        data: { helpTopic: 'mealPlans' },
      },
      {
        path: 'medicine',
        component: GuardianMedicine,
        data: { helpTopic: 'medicine' },
      },
      {
        path: 'sleep-diary',
        component: GuardianSleepDiary,
        data: { helpTopic: 'sleepDiary' },
      },
      {
        path: 'progress',
        component: GuardianProgress,
        data: { helpTopic: 'progress' },
      },
      {
        path: 'pickup',
        component: GuardianPickup,
        data: { helpTopic: 'pickup' },
      },
      {
        path: 'babysitters',
        component: GuardianBabysitters,
        data: { helpTopic: 'babysitters' },
      },
      {
        path: 'work-locations',
        component: GuardianWorkLocations,
        data: { helpTopic: 'workLocations' },
      },
      {
        path: 'print',
        component: GuardianPrint,
        data: { helpTopic: 'print' },
      },
      {
        path: 'print/templates/:templateId',
        component: PrintTemplateEditor,
        data: { helpTopic: 'print' },
      },
      {
        path: 'calendar',
        component: GuardianCalendar,
        data: { helpTopic: 'calendar' },
      },
      {
        path: 'task-library',
        component: GuardianTaskLibrary,
        data: { helpTopic: 'taskLibrary' },
      },
      {
        path: 'admin',
        component: GuardianAdmin,
        data: { helpTopic: 'admin' },
      },
      // Every help topic on one page; it has no page help of its own.
      { path: 'help', component: GuardianHelp },
    ],
  },
];
