import { HelpTopic } from './help-topic';

// Every help topic, in the order the help page lists them. A guardian route names its topic in
// `data.helpTopic`; src/app/help-coverage.spec.ts keeps routes, this list and the `help`
// translations in step.
export const HELP_TOPICS: readonly HelpTopic[] = [
  {
    id: 'dashboard',
    sections: [{ id: 'overview' }, { id: 'tasks' }, { id: 'medicine' }, { id: 'mealsAndPickups' }],
    related: ['calendar', 'medicine', 'mealPlans', 'pickup'],
    link: '/guardian/onboarding',
  },
  {
    id: 'calendar',
    sections: [
      { id: 'views' },
      { id: 'ownership' },
      { id: 'addItem', steps: 5 },
      { id: 'changeItems' },
    ],
    related: ['taskLibrary', 'groupsAndSharing', 'admin'],
  },
  {
    id: 'taskLibrary',
    sections: [
      { id: 'templates' },
      { id: 'buildTemplate', steps: 4 },
      { id: 'schedule', steps: 4 },
      { id: 'archive' },
    ],
    related: ['calendar'],
  },
  {
    id: 'mealPlans',
    sections: [
      { id: 'planWeek', steps: 3 },
      { id: 'aiAssistant', steps: 4 },
      { id: 'importPlans', steps: 4 },
      { id: 'calendarLink' },
    ],
    related: ['dashboard', 'groupsAndSharing', 'print'],
  },
  {
    id: 'medicine',
    sections: [{ id: 'addSchedule', steps: 4 }, { id: 'doseTimes' }, { id: 'doseStatus' }],
    related: ['dashboard', 'groupsAndSharing'],
  },
  {
    id: 'sleepDiary',
    sections: [
      { id: 'logNight', steps: 3 },
      { id: 'hygieneNotes' },
      { id: 'shareWithDoctor', steps: 3 },
    ],
    related: ['medicine'],
  },
  {
    id: 'progress',
    sections: [{ id: 'earnStars' }, { id: 'goalPosts', steps: 4 }, { id: 'milestones' }],
    related: ['calendar', 'taskLibrary'],
  },
  {
    id: 'pickup',
    sections: [
      { id: 'planSlot', steps: 4 },
      { id: 'whoTakesCare' },
      { id: 'changeOrClear' },
      { id: 'whereItShows' },
    ],
    related: ['babysitters', 'dashboard', 'print'],
  },
  {
    id: 'babysitters',
    sections: [{ id: 'addBabysitter', steps: 3 }, { id: 'useInPickups' }, { id: 'editOrRemove' }],
    related: ['pickup', 'print'],
  },
  {
    id: 'workLocations',
    sections: [
      { id: 'addLocations', steps: 4 },
      { id: 'weeklyPattern', steps: 4 },
      { id: 'exceptions' },
      { id: 'whyItMatters' },
    ],
    related: ['print'],
  },
  {
    id: 'print',
    sections: [
      { id: 'printWeekPlan', steps: 4 },
      { id: 'createTemplate', steps: 3 },
      { id: 'templateRows' },
      { id: 'colorsAndSaving' },
    ],
    related: [
      'workLocations',
      'babysitters',
      'pickup',
      'mealPlans',
      'calendar',
      'groupsAndSharing',
    ],
  },
  {
    id: 'groupsAndSharing',
    sections: [
      { id: 'groups' },
      { id: 'addMembers', steps: 4 },
      { id: 'permissions' },
      { id: 'guardianVsGroup' },
    ],
    related: ['calendar', 'admin', 'mealPlans', 'medicine'],
  },
  {
    id: 'admin',
    sections: [
      { id: 'account' },
      { id: 'children', steps: 4 },
      { id: 'groupsAndCalendars' },
      { id: 'aiAssistant', steps: 4 },
    ],
    related: ['groupsAndSharing', 'calendar', 'mealPlans'],
  },
];

export function findHelpTopic(id: unknown): HelpTopic | undefined {
  return HELP_TOPICS.find((topic) => topic.id === id);
}
