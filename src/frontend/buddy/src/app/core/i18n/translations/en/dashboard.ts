export const dashboard = {
  eyebrow: 'Today',
  greeting: 'Good to see you.',
  children: {
    title: 'Children',
    loading: 'Loading children…',
    loadError: 'Unable to load children.',
    empty: 'No children linked yet. Add one from Settings.',
    linkedBadge: 'Linked'
  },
  tasks: {
    title: 'Today’s tasks',
    loading: 'Loading tasks…',
    loadError: 'Unable to load today’s tasks.',
    taskUpdateError: 'Unable to update this task.',
    empty: 'No tasks due today.',
    overdue: 'Overdue',
    dueToday: 'Due today'
  },
  events: {
    title: 'Today’s events',
    loading: 'Loading events…',
    loadError: 'Unable to load today’s events.',
    empty: 'Nothing else on the calendar today.'
  },
  mealplan: {
    title: 'Today’s meal plan',
    planLink: 'Plan meals →',
    loading: 'Loading meal plan…',
    loadError: 'Unable to load today’s meal plan.',
    noChildren: 'Link a child from Settings to see their meal plan.',
    notPlanned: 'Not planned',
    slots: {
      breakfast: 'Breakfast',
      lunch: 'Lunch',
      dinner: 'Dinner',
      snack: 'Snack'
    }
  },
  doses: {
    title: 'Today’s medicine',
    manageLink: 'Manage medicine →',
    loading: 'Loading today’s doses…',
    loadError: 'Unable to load today’s medicine doses.',
    updateError: 'Unable to update this dose.',
    noChildren: 'Link a child from Settings to track their medicine.',
    empty: 'No medicine scheduled for today.',
    markTaken: 'Mark taken',
    skip: 'Skip',
    taken: 'Taken',
    skipped: 'Skipped',
    undo: 'Undo'
  },
  pickup: {
    title: 'Today’s pickup & drop-off',
    manageLink: 'Plan pickups →',
    loading: 'Loading today’s schedule…',
    loadError: 'Unable to load today’s pickup schedule.',
    noChildren: 'Link a child from Settings to plan their pickups.',
    empty: 'Nothing planned for today.',
    slots: {
      dropOff: 'Drop-off',
      pickUp: 'Pickup'
    },
    kind: {
      guardian: 'A guardian',
      selfEscort: 'Goes alone',
      sibling: 'A sibling'
    }
  }
};
